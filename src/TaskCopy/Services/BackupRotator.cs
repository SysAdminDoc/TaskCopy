using System.IO;
using TaskCopy.Data;

namespace TaskCopy.Services;

/// <summary>
/// Snapshots snippets.db to numbered .bak.N files via SQLite VACUUM INTO so
/// the snapshot is transactionally consistent under live writes. Rotates
/// .bak.(keep-1) -> dropped, .bak.0 -> .bak.1, fresh snapshot -> .bak.0.
/// </summary>
public static class BackupRotator
{
    public static void Rotate(SnippetDatabase db, int keep = 3)
    {
        if (keep < 1) return;
        var dir = Path.GetDirectoryName(db.DbPath);
        if (string.IsNullOrEmpty(dir)) return;

        var name = Path.GetFileNameWithoutExtension(db.DbPath);
        var ext = Path.GetExtension(db.DbPath);

        string Slot(int i) => Path.Combine(dir, $"{name}.bak.{i}{ext}");

        // Drop the oldest, then shift each one up by one slot.
        var oldest = Slot(keep - 1);
        if (File.Exists(oldest)) TryDelete(oldest);

        for (int i = keep - 2; i >= 0; i--)
        {
            var src = Slot(i);
            var dst = Slot(i + 1);
            if (!File.Exists(src)) continue;
            if (File.Exists(dst)) TryDelete(dst);
            try { File.Move(src, dst); } catch { /* best-effort */ }
        }

        // Fresh snapshot in slot 0.
        var fresh = Slot(0);
        if (File.Exists(fresh)) TryDelete(fresh);
        db.BackupTo(fresh);

        // B13: SQLite's VACUUM INTO is transactionally safe inside SQLite but
        // the resulting file still sits in the OS write-back cache. Force a
        // flush so a power loss between this point and the next sync doesn't
        // give us a torn backup file.
        try
        {
            using var fs = new FileStream(fresh, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            fs.Flush(flushToDisk: true);
        }
        catch { /* best-effort */ }

        // F41 / B20: verify the just-written backup is openable + uncorrupted.
        // PRAGMA quick_check on a freshly-VACUUMed file is ~µs and catches the
        // (rare) case where VACUUM INTO claimed success but the resulting file
        // is unusable (bad sector, half-flushed cache, etc.). On failure we
        // drop the broken file so the prior slot 0 (now at .bak.1) remains
        // the most recent good snapshot.
        try
        {
            var status = SnippetDatabase.IntegrityCheck(fresh);
            if (!string.Equals(status, "ok", StringComparison.OrdinalIgnoreCase))
            {
                CrashLog.Write("BackupRotator.Rotate.Verify",
                    new Exception($"PRAGMA quick_check on '{fresh}' returned: {status}. Deleting the broken backup; prior snapshots are intact."));
                TryDelete(fresh);
            }
        }
        catch (Exception ex)
        {
            CrashLog.Write("BackupRotator.Rotate.VerifyException", ex);
        }
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch { /* best-effort */ }
    }

    /// <summary>
    /// Lists existing backup slots in MRU order (.bak.0 first). Each slot
    /// carries its on-disk path, last-modified time, and a best-effort
    /// snippet count (-1 = unreadable).
    /// </summary>
    public static IReadOnlyList<BackupSlot> ListAvailable(SnippetDatabase db, int keep = 3)
    {
        var dir = Path.GetDirectoryName(db.DbPath);
        if (string.IsNullOrEmpty(dir)) return Array.Empty<BackupSlot>();

        var name = Path.GetFileNameWithoutExtension(db.DbPath);
        var ext = Path.GetExtension(db.DbPath);

        var list = new List<BackupSlot>(keep);
        for (int i = 0; i < keep; i++)
        {
            var path = Path.Combine(dir, $"{name}.bak.{i}{ext}");
            if (!File.Exists(path)) continue;
            var stamp = File.GetLastWriteTimeUtc(path);
            var count = SnippetDatabase.TryCountSnippets(path);
            list.Add(new BackupSlot(i, path, stamp, count));
        }
        return list;
    }

    /// <summary>
    /// Replace the live snippets.db with the named backup file. Takes a
    /// pre-restore snapshot first ({name}.bak.preRestore{ext}) so the
    /// operation is itself reversible.
    ///
    /// Caller is responsible for ensuring no SqliteConnection is open on the
    /// live DB when this runs.
    /// </summary>
    public static void RestoreFrom(SnippetDatabase db, string sourceBackupPath)
    {
        if (!File.Exists(sourceBackupPath))
            throw new FileNotFoundException("Backup file not found.", sourceBackupPath);

        var dir = Path.GetDirectoryName(db.DbPath) ?? ".";
        var name = Path.GetFileNameWithoutExtension(db.DbPath);
        var ext = Path.GetExtension(db.DbPath);
        var preRestore = Path.Combine(dir, $"{name}.bak.preRestore{ext}");

        // Snapshot the current live DB so a user who restored the wrong file
        // can put things back. VACUUM INTO is transactionally safe here.
        if (File.Exists(db.DbPath))
        {
            try
            {
                if (File.Exists(preRestore)) TryDelete(preRestore);
                db.BackupTo(preRestore);
            }
            catch
            {
                // If the live file is so corrupt we can't VACUUM, fall back
                // to a raw copy so the user still has something.
                try { File.Copy(db.DbPath, preRestore, overwrite: true); } catch { }
            }
        }

        // Drop the live file (+ WAL sidecars) and swap in the backup.
        TryDelete(db.DbPath + "-wal");
        TryDelete(db.DbPath + "-shm");
        TryDelete(db.DbPath);
        File.Copy(sourceBackupPath, db.DbPath, overwrite: true);
    }
}

/// <summary>One row in the "Restore from backup" picker.</summary>
public sealed record BackupSlot(int Index, string Path, DateTime LastWriteUtc, int SnippetCount)
{
    public string DisplayLabel =>
        $"Backup #{Index} — {LastWriteUtc.ToLocalTime():yyyy-MM-dd HH:mm}"
        + (SnippetCount >= 0 ? $" · {SnippetCount} snippet{(SnippetCount == 1 ? "" : "s")}" : " · (unreadable)");
}
