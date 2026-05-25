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
    public const int CurrentVersion = 3;

    public static void Apply(SqliteConnection conn)
    {
        var version = GetUserVersion(conn);
        if (version < 1) ApplyV1(conn);
        if (version < 2) ApplyV2(conn);
        if (version < 3) ApplyV3(conn);
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
