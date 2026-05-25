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
    public const int CurrentVersion = 2;

    public static void Apply(SqliteConnection conn)
    {
        var version = GetUserVersion(conn);
        if (version < 1) ApplyV1(conn);
        if (version < 2) ApplyV2(conn);
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
        using var tx = conn.BeginTransaction();

        using (var cmd = conn.CreateCommand())
        {
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

        AddColumnIfMissing(conn, "snippets", "quick_hotkey", "TEXT");
        AddColumnIfMissing(conn, "snippets", "used_count", "INTEGER NOT NULL DEFAULT 0");
        AddColumnIfMissing(conn, "snippets", "last_used_at", "INTEGER");
        AddColumnIfMissing(conn, "snippets", "pinned", "INTEGER NOT NULL DEFAULT 0");
        AddColumnIfMissing(conn, "snippets", "is_monospace", "INTEGER NOT NULL DEFAULT 0");
        AddColumnIfMissing(conn, "snippets", "group_id", "INTEGER REFERENCES groups(id) ON DELETE SET NULL");
        AddColumnIfMissing(conn, "snippets", "deleted_at", "INTEGER");

        SetUserVersion(conn, 2);
        tx.Commit();
    }

    private static void AddColumnIfMissing(SqliteConnection conn, string table, string column, string definition)
    {
        if (ColumnExists(conn, table, column)) return;
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition};";
        cmd.ExecuteNonQuery();
    }

    private static bool ColumnExists(SqliteConnection conn, string table, string column)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info({table});";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }
}
