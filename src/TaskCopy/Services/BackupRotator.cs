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
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch { /* best-effort */ }
    }
}
