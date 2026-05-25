using System.IO;
using Microsoft.Data.Sqlite;
using TaskCopy.Models;

namespace TaskCopy.Data;

public sealed class SnippetDatabase
{
    private readonly string _connectionString;

    public string DbPath { get; }

    public SnippetDatabase(string dbPath)
    {
        DbPath = dbPath;
        var dir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();
        InitializeDatabase();
    }

    private SqliteConnection Open()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var pragma = conn.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = ON;";
        pragma.ExecuteNonQuery();
        return conn;
    }

    private void InitializeDatabase()
    {
        using var conn = Open();
        using (var wal = conn.CreateCommand())
        {
            // WAL persists once set, so this is effectively a one-time write.
            // Better durability + concurrent reads as future async paths land.
            wal.CommandText = "PRAGMA journal_mode = WAL;";
            wal.ExecuteNonQuery();
        }
        Migrations.Apply(conn);
    }

    // -----------------------------------------------------------------------
    // Snippets (live — excludes soft-deleted)
    // -----------------------------------------------------------------------

    private const string SnippetSelectAll = """
        SELECT id, title, body, sort_order, created_at,
               quick_hotkey, used_count, last_used_at, pinned, is_monospace,
               group_id, deleted_at, paste_mode,
               last_target_process_name, last_target_at, target_app_glob
        FROM snippets
        """;

    public List<Snippet> GetAll()
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = SnippetSelectAll + " WHERE deleted_at IS NULL ORDER BY sort_order, id;";
        return Read(cmd);
    }

    public List<Snippet> GetByGroup(long? groupId)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        if (groupId is null)
        {
            cmd.CommandText = SnippetSelectAll + " WHERE deleted_at IS NULL AND group_id IS NULL ORDER BY sort_order, id;";
        }
        else
        {
            cmd.CommandText = SnippetSelectAll + " WHERE deleted_at IS NULL AND group_id = $g ORDER BY sort_order, id;";
            cmd.Parameters.AddWithValue("$g", groupId.Value);
        }
        return Read(cmd);
    }

    public List<Snippet> GetTrashed()
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = SnippetSelectAll + " WHERE deleted_at IS NOT NULL ORDER BY deleted_at DESC;";
        return Read(cmd);
    }

    private static List<Snippet> Read(SqliteCommand cmd)
    {
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
                CreatedAt = reader.GetInt64(4),
                QuickHotkey = reader.IsDBNull(5) ? null : reader.GetString(5),
                UsedCount = reader.GetInt64(6),
                LastUsedAt = reader.IsDBNull(7) ? null : reader.GetInt64(7),
                Pinned = reader.GetInt64(8) != 0,
                IsMonospace = reader.GetInt64(9) != 0,
                GroupId = reader.IsDBNull(10) ? null : reader.GetInt64(10),
                DeletedAt = reader.IsDBNull(11) ? null : reader.GetInt64(11),
                PasteMode = reader.IsDBNull(12) ? 0 : (int)reader.GetInt64(12),
                LastTargetProcessName = reader.IsDBNull(13) ? null : reader.GetString(13),
                LastTargetAt = reader.IsDBNull(14) ? null : reader.GetInt64(14),
                TargetAppGlob = reader.IsDBNull(15) ? null : reader.GetString(15),
            });
        }
        return list;
    }

    public long Insert(string title, string body, long? groupId = null)
    {
        using var conn = Open();
        using var tx = conn.BeginTransaction();
        var nextOrder = GetMaxSortOrder(conn) + 1;
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO snippets (title, body, sort_order, created_at, group_id)
            VALUES ($t, $b, $o, $c, $g)
            RETURNING id;
            """;
        cmd.Parameters.AddWithValue("$t", title);
        cmd.Parameters.AddWithValue("$b", body);
        cmd.Parameters.AddWithValue("$o", nextOrder);
        cmd.Parameters.AddWithValue("$c", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        cmd.Parameters.AddWithValue("$g", (object?)groupId ?? DBNull.Value);
        var id = (long)(cmd.ExecuteScalar() ?? 0L);
        tx.Commit();
        return id;
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

    public void SoftDelete(long id)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE snippets SET deleted_at = $now WHERE id = $id;";
        cmd.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    public void Restore(long id)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE snippets SET deleted_at = NULL WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    /// <summary>Purges snippets whose deleted_at is older than the cutoff (Unix seconds).</summary>
    public int PurgeDeletedOlderThan(long cutoffEpochSeconds)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM snippets WHERE deleted_at IS NOT NULL AND deleted_at < $c;";
        cmd.Parameters.AddWithValue("$c", cutoffEpochSeconds);
        return cmd.ExecuteNonQuery();
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

    public void SetQuickHotkey(long id, string? hotkey)
    {
        using var conn = Open();
        // Clear any other snippet that currently holds the same slot.
        if (!string.IsNullOrEmpty(hotkey))
        {
            using var clear = conn.CreateCommand();
            clear.CommandText = "UPDATE snippets SET quick_hotkey = NULL WHERE quick_hotkey = $h AND id <> $id;";
            clear.Parameters.AddWithValue("$h", hotkey);
            clear.Parameters.AddWithValue("$id", id);
            clear.ExecuteNonQuery();
        }
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE snippets SET quick_hotkey = $h WHERE id = $id;";
        cmd.Parameters.AddWithValue("$h", (object?)hotkey ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    public void SetPinned(long id, bool pinned)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE snippets SET pinned = $p WHERE id = $id;";
        cmd.Parameters.AddWithValue("$p", pinned ? 1 : 0);
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    public void SetMonospace(long id, bool monospace)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE snippets SET is_monospace = $m WHERE id = $id;";
        cmd.Parameters.AddWithValue("$m", monospace ? 1 : 0);
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    public void SetPasteMode(long id, int mode)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE snippets SET paste_mode = $p WHERE id = $id;";
        cmd.Parameters.AddWithValue("$p", mode);
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    public void SetGroup(long id, long? groupId)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE snippets SET group_id = $g WHERE id = $id;";
        cmd.Parameters.AddWithValue("$g", (object?)groupId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    public void RecordUse(long id)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE snippets SET used_count = used_count + 1, last_used_at = $now WHERE id = $id;";
        cmd.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    /// <summary>F35: set the comma-separated process-name glob list (NULL clears).</summary>
    public void SetTargetAppGlob(long id, string? glob)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE snippets SET target_app_glob = $g WHERE id = $id;";
        cmd.Parameters.AddWithValue("$g", (object?)glob ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    /// <summary>F48: record which app received the snippet's last successful auto-paste.</summary>
    public void SetLastTarget(long snippetId, string processName)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE snippets SET last_target_process_name = $p, last_target_at = $now WHERE id = $id;";
        cmd.Parameters.AddWithValue("$p", processName);
        cmd.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        cmd.Parameters.AddWithValue("$id", snippetId);
        cmd.ExecuteNonQuery();
    }

    // -----------------------------------------------------------------------
    // F46: snippet body history (10 newest versions per snippet, FK CASCADE)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Record one body version after a debounced save. Trims to keep at most
    /// <paramref name="keep"/> rows per snippet (default 10).
    /// </summary>
    public void RecordBodyHistory(long snippetId, string body, int keep = 10)
    {
        using var conn = Open();
        using var tx = conn.BeginTransaction();

        using (var ins = conn.CreateCommand())
        {
            ins.Transaction = tx;
            ins.CommandText = "INSERT INTO snippet_body_history (snippet_id, body, saved_at) VALUES ($s, $b, $now);";
            ins.Parameters.AddWithValue("$s", snippetId);
            ins.Parameters.AddWithValue("$b", body);
            ins.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            ins.ExecuteNonQuery();
        }

        // Trim: keep N most recent per snippet.
        using (var trim = conn.CreateCommand())
        {
            trim.Transaction = tx;
            trim.CommandText = """
                DELETE FROM snippet_body_history
                WHERE snippet_id = $s
                AND id NOT IN (
                    SELECT id FROM snippet_body_history
                    WHERE snippet_id = $s
                    ORDER BY saved_at DESC LIMIT $n
                );
                """;
            trim.Parameters.AddWithValue("$s", snippetId);
            trim.Parameters.AddWithValue("$n", keep);
            trim.ExecuteNonQuery();
        }

        tx.Commit();
    }

    public List<BodyHistoryEntry> GetBodyHistory(long snippetId)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, body, saved_at FROM snippet_body_history WHERE snippet_id = $s ORDER BY saved_at DESC;";
        cmd.Parameters.AddWithValue("$s", snippetId);
        using var reader = cmd.ExecuteReader();
        var list = new List<BodyHistoryEntry>();
        while (reader.Read())
        {
            list.Add(new BodyHistoryEntry
            {
                Id = reader.GetInt64(0),
                Body = reader.GetString(1),
                SavedAt = reader.GetInt64(2),
            });
        }
        return list;
    }

    public void DeleteBodyHistoryEntry(long id)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM snippet_body_history WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    // -----------------------------------------------------------------------
    // Groups
    // -----------------------------------------------------------------------

    public List<SnippetGroup> GetGroups()
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, name, sort_order FROM groups ORDER BY sort_order, id;";
        using var reader = cmd.ExecuteReader();
        var list = new List<SnippetGroup>();
        while (reader.Read())
        {
            list.Add(new SnippetGroup
            {
                Id = reader.GetInt64(0),
                Name = reader.GetString(1),
                SortOrder = reader.GetInt32(2),
            });
        }
        return list;
    }

    public long InsertGroup(string name)
    {
        using var conn = Open();
        using var tx = conn.BeginTransaction();
        int nextOrder;
        using (var maxCmd = conn.CreateCommand())
        {
            maxCmd.CommandText = "SELECT COALESCE(MAX(sort_order), -1) + 1 FROM groups;";
            nextOrder = Convert.ToInt32(maxCmd.ExecuteScalar() ?? 0);
        }
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO groups (name, sort_order) VALUES ($n, $o) RETURNING id;";
        cmd.Parameters.AddWithValue("$n", name);
        cmd.Parameters.AddWithValue("$o", nextOrder);
        var id = (long)(cmd.ExecuteScalar() ?? 0L);
        tx.Commit();
        return id;
    }

    public void RenameGroup(long id, string name)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE groups SET name = $n WHERE id = $id;";
        cmd.Parameters.AddWithValue("$n", name);
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    public void DeleteGroup(long id)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        // FK is ON DELETE SET NULL — snippets get ungrouped automatically.
        cmd.CommandText = "DELETE FROM groups WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    public void ReorderGroups(IReadOnlyList<long> orderedIds)
    {
        using var conn = Open();
        using var tx = conn.BeginTransaction();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE groups SET sort_order = $o WHERE id = $id;";
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

    // -----------------------------------------------------------------------
    // Recent clipboard captures
    // -----------------------------------------------------------------------

    public void InsertRecentClip(string body, int maxKeep)
    {
        using var conn = Open();
        using var tx = conn.BeginTransaction();

        // Deduplicate: if the most recent clip is identical, just bump its timestamp.
        long? dupId = null;
        using (var dup = conn.CreateCommand())
        {
            dup.CommandText = "SELECT id FROM recent_clips ORDER BY copied_at DESC LIMIT 1;";
            var v = dup.ExecuteScalar();
            if (v is long last)
            {
                using var bodyCheck = conn.CreateCommand();
                bodyCheck.CommandText = "SELECT body FROM recent_clips WHERE id = $id;";
                bodyCheck.Parameters.AddWithValue("$id", last);
                if (bodyCheck.ExecuteScalar() is string prev && prev == body)
                {
                    dupId = last;
                }
            }
        }

        if (dupId is long id)
        {
            using var bump = conn.CreateCommand();
            bump.CommandText = "UPDATE recent_clips SET copied_at = $now WHERE id = $id;";
            bump.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            bump.Parameters.AddWithValue("$id", id);
            bump.ExecuteNonQuery();
        }
        else
        {
            using var ins = conn.CreateCommand();
            ins.CommandText = "INSERT INTO recent_clips (body, copied_at) VALUES ($b, $now);";
            ins.Parameters.AddWithValue("$b", body);
            ins.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            ins.ExecuteNonQuery();
        }

        // Trim to maxKeep most recent.
        using (var trim = conn.CreateCommand())
        {
            trim.CommandText = """
                DELETE FROM recent_clips
                WHERE id NOT IN (
                    SELECT id FROM recent_clips ORDER BY copied_at DESC LIMIT $n
                );
                """;
            trim.Parameters.AddWithValue("$n", maxKeep);
            trim.ExecuteNonQuery();
        }

        tx.Commit();
    }

    public List<RecentClip> GetRecentClips(int max)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, body, copied_at FROM recent_clips ORDER BY copied_at DESC LIMIT $n;";
        cmd.Parameters.AddWithValue("$n", max);
        using var reader = cmd.ExecuteReader();
        var list = new List<RecentClip>();
        while (reader.Read())
        {
            list.Add(new RecentClip
            {
                Id = reader.GetInt64(0),
                Body = reader.GetString(1),
                CopiedAt = reader.GetInt64(2),
            });
        }
        return list;
    }

    public void ClearRecentClips()
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM recent_clips;";
        cmd.ExecuteNonQuery();
    }

    // -----------------------------------------------------------------------
    // Backup / integrity
    // -----------------------------------------------------------------------

    /// <summary>
    /// SQLite-correct snapshot under live writes — equivalent of
    /// .backup or VACUUM INTO. Caller picks the target path.
    /// </summary>
    public void BackupTo(string targetPath)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"VACUUM INTO '{targetPath.Replace("'", "''")}';";
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Runs PRAGMA quick_check (cheaper than integrity_check) and returns
    /// the result. SQLite returns "ok" on success or one-or-more error lines.
    /// </summary>
    public string IntegrityCheck()
    {
        try
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA quick_check;";
            var result = cmd.ExecuteScalar() as string;
            return string.IsNullOrEmpty(result) ? "ok" : result;
        }
        catch (Exception ex)
        {
            return $"check failed: {ex.Message}";
        }
    }

    /// <summary>
    /// F41: run PRAGMA quick_check against an arbitrary SQLite file (read-only
    /// open, separate connection). Used by BackupRotator to verify the snapshot
    /// it just wrote before declaring success. Returns "ok" on success.
    /// </summary>
    public static string IntegrityCheck(string explicitPath)
    {
        try
        {
            var cs = new SqliteConnectionStringBuilder
            {
                DataSource = explicitPath,
                Mode = SqliteOpenMode.ReadOnly,
            }.ToString();
            using var conn = new SqliteConnection(cs);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA quick_check;";
            var result = cmd.ExecuteScalar() as string;
            return string.IsNullOrEmpty(result) ? "ok" : result;
        }
        catch (Exception ex)
        {
            return $"check failed: {ex.Message}";
        }
    }

    /// <summary>
    /// Best-effort snippet count for a backup file. Opens the file read-only
    /// in a separate connection so the live DB is untouched.
    /// </summary>
    public static int TryCountSnippets(string dbPath)
    {
        try
        {
            var cs = new SqliteConnectionStringBuilder
            {
                DataSource = dbPath,
                Mode = SqliteOpenMode.ReadOnly,
            }.ToString();
            using var conn = new SqliteConnection(cs);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM snippets WHERE deleted_at IS NULL;";
            return Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
        }
        catch
        {
            return -1;
        }
    }

    // -----------------------------------------------------------------------
    // Settings KV
    // -----------------------------------------------------------------------

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

    /// <summary>
    /// F52: wipe every row in the settings KV table. Snippets, groups,
    /// recent_clips, and trashed snippets are NOT touched — user content
    /// stays. Caller is responsible for surfacing the "you'll need to
    /// relaunch" UX since the in-memory hotkey/theme/etc. caches are now
    /// disconnected from the (empty) persisted store.
    /// </summary>
    public void ClearAllSettings()
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM settings;";
        cmd.ExecuteNonQuery();
    }
}
