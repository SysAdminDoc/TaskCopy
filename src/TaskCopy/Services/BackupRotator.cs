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
    public static void Rotate(SnippetDatabase db, int keep = 3,
                                bool encrypt = false, string? password = null)
    {
        if (keep < 1) return;
        var dir = Path.GetDirectoryName(db.DbPath);
        if (string.IsNullOrEmpty(dir)) return;

        var name = Path.GetFileNameWithoutExtension(db.DbPath);
        var ext = Path.GetExtension(db.DbPath);

        // F49: when encryption is on, the suffix changes to .enc so the file
        // type is obvious + a plain SQLite tool won't accidentally try to open
        // a ciphertext blob as a database. The rotator handles either suffix.
        string Suffix() => encrypt ? ".enc" : ext;
        string Slot(int i) => Path.Combine(dir, $"{name}.bak.{i}{Suffix()}");

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

        // Fresh snapshot in slot 0. When encryption is on, we VACUUM INTO a
        // temp plaintext file first, run quick_check against it, then encrypt
        // to the final slot — same verify discipline as the plaintext path.
        var fresh = Slot(0);
        if (File.Exists(fresh)) TryDelete(fresh);

        if (encrypt && !string.IsNullOrEmpty(password))
        {
            var plaintextTemp = Path.Combine(dir, $"{name}.bak.tmp{ext}");
            try
            {
                if (File.Exists(plaintextTemp)) TryDelete(plaintextTemp);
                db.BackupTo(plaintextTemp);

                // Verify the plaintext snapshot before encrypting.
                var status = SnippetDatabase.IntegrityCheck(plaintextTemp);
                if (!string.Equals(status, "ok", StringComparison.OrdinalIgnoreCase))
                {
                    CrashLog.Write("BackupRotator.RotateEncrypted.Verify",
                        new Exception($"quick_check on temp plaintext failed: {status}"));
                    return;
                }

                BackupCrypto.EncryptFile(plaintextTemp, fresh, password);
            }
            finally
            {
                if (File.Exists(plaintextTemp)) TryDelete(plaintextTemp);
            }
            return;
        }

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
            // F49: both .db and .enc shapes are accepted. Encrypted slots show
            // -1 for snippet count (we can't open them without the password)
            // and the BackupSlot.IsEncrypted flag flags them to the restore UI.
            var dbPath = Path.Combine(dir, $"{name}.bak.{i}{ext}");
            var encPath = Path.Combine(dir, $"{name}.bak.{i}.enc");
            if (File.Exists(dbPath))
            {
                var stamp = File.GetLastWriteTimeUtc(dbPath);
                var count = SnippetDatabase.TryCountSnippets(dbPath);
                list.Add(new BackupSlot(i, dbPath, stamp, count, IsEncrypted: false));
            }
            else if (File.Exists(encPath))
            {
                var stamp = File.GetLastWriteTimeUtc(encPath);
                list.Add(new BackupSlot(i, encPath, stamp, -1, IsEncrypted: true));
            }
        }
        return list;
    }

    /// <summary>
    /// Replace the live snippets.db with the named backup file. Takes a
    /// pre-restore snapshot first ({name}.bak.preRestore{ext}) so the
    /// operation is itself reversible.
    ///
    /// F49: when the source is encrypted (BackupCrypto.IsEncryptedBackup),
    /// the caller must supply <paramref name="password"/>. Decrypts to a
    /// temp file first, verifies via quick_check, then swaps in.
    ///
    /// Caller is responsible for ensuring no SqliteConnection is open on the
    /// live DB when this runs.
    /// </summary>
    public static void RestoreFrom(SnippetDatabase db, string sourceBackupPath, string? password = null)
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

        // F49: if the source is encrypted, decrypt to a temp before swap.
        // Verify quick_check on the decrypted file; bail (without touching
        // the live DB) when the password is wrong or the ciphertext is bad.
        if (BackupCrypto.IsEncryptedBackup(sourceBackupPath))
        {
            if (string.IsNullOrEmpty(password))
                throw new InvalidOperationException("This backup is encrypted; a password is required.");

            var decryptedTemp = Path.Combine(dir, $"{name}.bak.decrypt-tmp{ext}");
            try
            {
                if (File.Exists(decryptedTemp)) TryDelete(decryptedTemp);
                if (!BackupCrypto.TryDecryptFile(sourceBackupPath, decryptedTemp, password!))
                    throw new InvalidOperationException("Decryption failed — wrong password or corrupt backup.");

                var status = SnippetDatabase.IntegrityCheck(decryptedTemp);
                if (!string.Equals(status, "ok", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"Decrypted backup failed integrity check: {status}");

                TryDelete(db.DbPath + "-wal");
                TryDelete(db.DbPath + "-shm");
                TryDelete(db.DbPath);
                File.Copy(decryptedTemp, db.DbPath, overwrite: true);
            }
            finally
            {
                if (File.Exists(decryptedTemp)) TryDelete(decryptedTemp);
            }
            return;
        }

        // Plaintext path — original v0.4.3 behavior.
        TryDelete(db.DbPath + "-wal");
        TryDelete(db.DbPath + "-shm");
        TryDelete(db.DbPath);
        File.Copy(sourceBackupPath, db.DbPath, overwrite: true);
    }
}

/// <summary>One row in the "Restore from backup" picker.</summary>
public sealed record BackupSlot(int Index, string Path, DateTime LastWriteUtc, int SnippetCount, bool IsEncrypted = false)
{
    public string DisplayLabel
    {
        get
        {
            var stamp = $"Backup #{Index} — {LastWriteUtc.ToLocalTime():yyyy-MM-dd HH:mm}";
            if (IsEncrypted) return $"{stamp} · encrypted (password required)";
            return SnippetCount >= 0
                ? $"{stamp} · {SnippetCount} snippet{(SnippetCount == 1 ? "" : "s")}"
                : $"{stamp} · (unreadable)";
        }
    }
}
