using System.IO;
using Microsoft.Data.Sqlite;
using TaskCopy.Models;

namespace TaskCopy.Data;

public sealed class SnippetDatabase
{
    private readonly string _connectionString;

    public SnippetDatabase(string dbPath)
    {
        var dir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();
        EnsureSchema();
    }

    private SqliteConnection Open()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        return conn;
    }

    private void EnsureSchema()
    {
        using var conn = Open();
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
    }

    public List<Snippet> GetAll()
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, title, body, sort_order, created_at FROM snippets ORDER BY sort_order, id;";
        using var reader = cmd.ExecuteReader();
        var list = new List<Snippet>();
        while (reader.Read())
        {
            list.Add(new Snippet
            {
                Id = reader.GetInt64(0),
                Title = reader.GetString(1),
                Body = reader.GetString(2),
                SortOrder = reader.GetInt32(3),
                CreatedAt = reader.GetInt64(4)
            });
        }
        return list;
    }

    public long Insert(string title, string body)
    {
        using var conn = Open();
        var nextOrder = GetMaxSortOrder(conn) + 1;
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO snippets (title, body, sort_order, created_at)
            VALUES ($t, $b, $o, $c);
            SELECT last_insert_rowid();
            """;
        cmd.Parameters.AddWithValue("$t", title);
        cmd.Parameters.AddWithValue("$b", body);
        cmd.Parameters.AddWithValue("$o", nextOrder);
        cmd.Parameters.AddWithValue("$c", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        return (long)(cmd.ExecuteScalar() ?? 0L);
    }

    public void Update(long id, string title, string body)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE snippets SET title = $t, body = $b WHERE id = $id;";
        cmd.Parameters.AddWithValue("$t", title);
        cmd.Parameters.AddWithValue("$b", body);
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    public void Delete(long id)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM snippets WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    public void Reorder(IReadOnlyList<long> orderedIds)
    {
        using var conn = Open();
        using var tx = conn.BeginTransaction();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE snippets SET sort_order = $o WHERE id = $id;";
        var pOrder = cmd.Parameters.Add("$o", SqliteType.Integer);
        var pId = cmd.Parameters.Add("$id", SqliteType.Integer);
        for (var i = 0; i < orderedIds.Count; i++)
        {
            pOrder.Value = i;
            pId.Value = orderedIds[i];
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }

    private static int GetMaxSortOrder(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COALESCE(MAX(sort_order), -1) FROM snippets;";
        return Convert.ToInt32(cmd.ExecuteScalar() ?? -1);
    }

    public string? GetSetting(string key)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT value FROM settings WHERE key = $k;";
        cmd.Parameters.AddWithValue("$k", key);
        return cmd.ExecuteScalar() as string;
    }

    public void SetSetting(string key, string value)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO settings (key, value) VALUES ($k, $v)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value;
            """;
        cmd.Parameters.AddWithValue("$k", key);
        cmd.Parameters.AddWithValue("$v", value);
        cmd.ExecuteNonQuery();
    }
}
