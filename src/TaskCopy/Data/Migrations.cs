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
    public const int CurrentVersion = 1;

    public static void Apply(SqliteConnection conn)
    {
        var version = GetUserVersion(conn);
        if (version < 1) ApplyV1(conn);
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
}
