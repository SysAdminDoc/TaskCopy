using Microsoft.Data.Sqlite;

namespace TaskCopy.Data;

/// <summary>
/// Applies forward-only schema migrations to the snippets database, tracking
/// progress via PRAGMA user_version. Each ApplyVN method runs inside its own
/// transaction so a failure rolls back cleanly. Never edit a shipped
/// migration — append a new one.
/// </summary>
internal static class Migrations
{
    /// <summary>The schema version this build expects.</summary>
    public const int CurrentVersion = 6;

    public static void Apply(SqliteConnection conn)
    {
        var version = GetUserVersion(conn);
        if (version < 1) ApplyV1(conn);
        if (version < 2) ApplyV2(conn);
        if (version < 3) ApplyV3(conn);
        if (version < 4) ApplyV4(conn);
        if (version < 5) ApplyV5(conn);
        if (version < 6) ApplyV6(conn);
    }

    private static int GetUserVersion(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA user_version;";
        return Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
    }

    private static void SetUserVersion(SqliteConnection conn, int version)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA user_version = {version};";
        cmd.ExecuteNonQuery();
    }

    private static void ApplyV1(SqliteConnection conn)
    {
        using var tx = conn.BeginTransaction();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS snippets (
                id          INTEGER PRIMARY KEY AUTOINCREMENT,
                title       TEXT NOT NULL,
                body        TEXT NOT NULL,
                sort_order  INTEGER NOT NULL DEFAULT 0,
                created_at  INTEGER NOT NULL
            );
            CREATE TABLE IF NOT EXISTS settings (
                key   TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );
            """;
        cmd.ExecuteNonQuery();
        SetUserVersion(conn, 1);
        tx.Commit();
    }

    private static void ApplyV2(SqliteConnection conn)
    {
        // I29: every DDL — table create, index create, ALTER TABLE ADD COLUMN —
        // runs inside the same transaction so a partial failure rolls back
        // cleanly and leaves user_version at 1 so the next launch retries.
        using var tx = conn.BeginTransaction();

        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS groups (
                    id         INTEGER PRIMARY KEY AUTOINCREMENT,
                    name       TEXT NOT NULL,
                    sort_order INTEGER NOT NULL DEFAULT 0
                );
                CREATE TABLE IF NOT EXISTS recent_clips (
                    id        INTEGER PRIMARY KEY AUTOINCREMENT,
                    body      TEXT NOT NULL,
                    copied_at INTEGER NOT NULL
                );
                CREATE INDEX IF NOT EXISTS idx_recent_clips_copied_at
                    ON recent_clips(copied_at DESC);
                """;
            cmd.ExecuteNonQuery();
        }

        AddColumnIfMissing(conn, tx, "snippets", "quick_hotkey", "TEXT");
        AddColumnIfMissing(conn, tx, "snippets", "used_count", "INTEGER NOT NULL DEFAULT 0");
        AddColumnIfMissing(conn, tx, "snippets", "last_used_at", "INTEGER");
        AddColumnIfMissing(conn, tx, "snippets", "pinned", "INTEGER NOT NULL DEFAULT 0");
        AddColumnIfMissing(conn, tx, "snippets", "is_monospace", "INTEGER NOT NULL DEFAULT 0");
        AddColumnIfMissing(conn, tx, "snippets", "group_id", "INTEGER REFERENCES groups(id) ON DELETE SET NULL");
        AddColumnIfMissing(conn, tx, "snippets", "deleted_at", "INTEGER");

        SetUserVersion(conn, 2);
        tx.Commit();
    }

    /// <summary>
    /// v0.4 (F24): paste_mode column controls whether auto-paste sends Ctrl+V
    /// or types the body character-by-character via INPUT_KEYBOARD with
    /// KEYEVENTF_UNICODE. 0 = Auto (Ctrl+V, default), 1 = Type.
    /// </summary>
    private static void ApplyV3(SqliteConnection conn)
    {
        using var tx = conn.BeginTransaction();
        AddColumnIfMissing(conn, tx, "snippets", "paste_mode", "INTEGER NOT NULL DEFAULT 0");
        SetUserVersion(conn, 3);
        tx.Commit();
    }

    /// <summary>
    /// v0.5 schema:
    ///   F46: snippet_body_history table (one row per debounced flush, capped
    ///        at 10 per snippet by the repository layer). FK CASCADE so a hard
    ///        delete drops history with the snippet.
    ///   F48: snippets.last_target_process_name + last_target_at columns —
    ///        records which app received the most recent successful auto-paste
    ///        for the snippet (informational; foundation for future F35 per-app
    ///        rules).
    /// </summary>
    private static void ApplyV4(SqliteConnection conn)
    {
        using var tx = conn.BeginTransaction();

        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS snippet_body_history (
                    id          INTEGER PRIMARY KEY AUTOINCREMENT,
                    snippet_id  INTEGER NOT NULL REFERENCES snippets(id) ON DELETE CASCADE,
                    body        TEXT NOT NULL,
                    saved_at    INTEGER NOT NULL
                );
                CREATE INDEX IF NOT EXISTS idx_snippet_body_history_snippet
                    ON snippet_body_history(snippet_id, saved_at DESC);
                """;
            cmd.ExecuteNonQuery();
        }

        AddColumnIfMissing(conn, tx, "snippets", "last_target_process_name", "TEXT");
        AddColumnIfMissing(conn, tx, "snippets", "last_target_at", "INTEGER");

        SetUserVersion(conn, 4);
        tx.Commit();
    }

    /// <summary>
    /// F35: per-app rules. snippets.target_app_glob is a comma-separated list
    /// of `*`-wildcard process-name patterns (e.g. "outlook.exe,Outlook*.exe"
    /// or "code*.exe,*-insiders.exe"). When set, the snippet is only shown in
    /// the flyout when the captured foreground process name matches any
    /// pattern. Empty/NULL = universal (current behavior).
    /// </summary>
    private static void ApplyV5(SqliteConnection conn)
    {
        using var tx = conn.BeginTransaction();
        AddColumnIfMissing(conn, tx, "snippets", "target_app_glob", "TEXT");
        SetUserVersion(conn, 5);
        tx.Commit();
    }

    /// <summary>
    /// F33: image snippets. Text snippets keep content_kind=0 and body as the
    /// canonical payload. Image snippets set content_kind=1 and store a PNG
    /// encoding plus dimensions for thumbnails / labels. Clipboard writes
    /// decode the PNG back into a BitmapSource, which WPF places on the
    /// clipboard as an image format apps can paste.
    /// </summary>
    private static void ApplyV6(SqliteConnection conn)
    {
        using var tx = conn.BeginTransaction();
        AddColumnIfMissing(conn, tx, "snippets", "content_kind", "INTEGER NOT NULL DEFAULT 0");
        AddColumnIfMissing(conn, tx, "snippets", "image_png", "BLOB");
        AddColumnIfMissing(conn, tx, "snippets", "image_width", "INTEGER");
        AddColumnIfMissing(conn, tx, "snippets", "image_height", "INTEGER");
        SetUserVersion(conn, 6);
        tx.Commit();
    }

    private static void AddColumnIfMissing(SqliteConnection conn, SqliteTransaction tx, string table, string column, string definition)
    {
        if (ColumnExists(conn, tx, table, column)) return;
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition};";
        cmd.ExecuteNonQuery();
    }

    private static bool ColumnExists(SqliteConnection conn, SqliteTransaction tx, string table, string column)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = $"PRAGMA table_info({table});";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }
}
