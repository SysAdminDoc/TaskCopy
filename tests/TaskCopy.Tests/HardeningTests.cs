using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TaskCopy.Data;
using TaskCopy.Services;
using Xunit;

namespace TaskCopy.Tests;

public sealed class HardeningTests
{
    [Fact]
    public void Localization_UsesEnglishBaselineAndSpanishProofCulture()
    {
        var prior = LocalizationService.CurrentCulture;
        try
        {
            Assert.True(LocalizationService.TrySetCulture("en-US"));
            Assert.Equal("TaskCopy — Settings", LocalizationService.Get("SettingsTitle"));

            Assert.True(LocalizationService.TrySetCulture("es-ES"));
            Assert.Equal("TaskCopy — Configuración", LocalizationService.Get("SettingsTitle"));
            Assert.Equal("Eliminar", LocalizationService.Get("DeleteButton"));
            Assert.Equal("missing.key", LocalizationService.Get("missing.key"));

            Assert.False(LocalizationService.TrySetCulture("fr-FR"));
            Assert.Equal("es-ES", LocalizationService.CurrentCulture.Name);
        }
        finally
        {
            LocalizationService.SetCulture(prior);
        }
    }

    [Fact]
    public void SnippetEditorService_PreviewsTemplatesAndRecordsHistory()
    {
        using var temp = TempWorkspace.Create();
        var db = temp.CreateDatabase();
        var id = db.Insert("Title", "old body");
        var snippet = Assert.Single(db.GetAll());
        snippet.Body = "Hello {{clipboard}}";

        var editor = new SnippetEditorService(db);
        Assert.Equal("Hello <clipboard>", editor.Preview(snippet.Body));

        editor.Save(snippet);

        Assert.Equal("Hello {{clipboard}}", db.GetAll().Single(s => s.Id == id).Body);
        Assert.Equal("Hello {{clipboard}}", Assert.Single(db.GetBodyHistory(id)).Body);
    }

    [Fact]
    public void AppGlob_MatchesWildcardsWithoutRegexSemantics()
    {
        Assert.True(AppGlob.Matches(null, null));
        Assert.False(AppGlob.Matches("outlook.exe", null));
        Assert.True(AppGlob.Matches("*code*.exe", "Code-Insiders.exe"));
        Assert.True(AppGlob.Matches("task.copy.exe", "task.copy.exe"));
        Assert.False(AppGlob.Matches("task?copy.exe", "task-copy.exe"));
    }

    [Fact]
    public void SnippetTemplating_DoesNotRunShellUnlessSnippetAllowsIt()
    {
        var ran = false;
        var result = SnippetTemplating.Expand("Before {{shell:echo nope}} after", new TemplatingContext
        {
            AllowShell = false,
            RunShellCommand = _ =>
            {
                ran = true;
                return "ran";
            },
        });

        Assert.False(ran);
        Assert.False(result.Cancelled);
        Assert.Equal("Before {{shell:echo nope}} after", result.Body);
    }

    [Fact]
    public void SnippetTemplating_CancelsWhenShellWarningIsDeclined()
    {
        var result = SnippetTemplating.Expand("{{shell:echo nope}}", new TemplatingContext
        {
            AllowShell = true,
            ConfirmShellExecution = _ => false,
            RunShellCommand = _ => "ran",
        });

        Assert.True(result.Cancelled);
    }

    [Fact]
    public void ExternalEditor_SplitsQuotedConfiguredCommand()
    {
        var (exe, args) = ExternalEditor.ResolveCommand(
            "\"C:\\Program Files\\Editor\\editor.exe\" --wait --reuse-window",
            "C:\\Temp\\taskcopy-edit.txt");

        Assert.Equal("C:\\Program Files\\Editor\\editor.exe", exe);
        Assert.Equal("--wait --reuse-window \"C:\\Temp\\taskcopy-edit.txt\"", args);
    }

    [Fact]
    public void SnippetDatabase_FtsSearchTracksUpdatesAndExcludesTrash()
    {
        using var temp = TempWorkspace.Create();
        var db = temp.CreateDatabase();
        var live = db.Insert("Alpha", "needle body");
        var trashed = db.Insert("Old needle", "needle body");
        db.Insert("Other", "unrelated");
        db.SoftDelete(trashed);

        Assert.True(db.TrySearchFtsIds("needle", out var initial));
        Assert.Contains(live, initial);
        Assert.DoesNotContain(trashed, initial);

        db.Update(live, "Alpha", "changed");
        Assert.True(db.TrySearchFtsIds("needle", out var afterUpdate));
        Assert.DoesNotContain(live, afterUpdate);

        Assert.True(db.TrySearchFtsIds("changed", out var changed));
        Assert.Contains(live, changed);
    }

    [Fact]
    public void SnippetIo_ImportSkipsUnsupportedContentAndClampsPasteMode()
    {
        using var temp = TempWorkspace.Create();
        var db = temp.CreateDatabase();
        var path = Path.Combine(temp.DirectoryPath, "import.json");
        var payload = new
        {
            version = 1,
            exportedAt = DateTimeOffset.UtcNow.ToString("o"),
            groups = new[] { new { name = " Work ", sortOrder = 0 } },
            snippets = new object[]
            {
                new
                {
                    title = " Valid ",
                    body = "Body",
                    sortOrder = 0,
                    createdAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    quickHotkey = (string?)null,
                    pinned = false,
                    isMonospace = false,
                    groupName = "Work",
                    pasteMode = 999,
                    targetAppGlob = (string?)null,
                    contentKind = 0,
                },
                new
                {
                    title = "Unsupported",
                    body = "Body",
                    sortOrder = 1,
                    createdAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    quickHotkey = (string?)null,
                    pinned = false,
                    isMonospace = false,
                    groupName = (string?)null,
                    pasteMode = 0,
                    targetAppGlob = (string?)null,
                    contentKind = 42,
                },
            },
            schemaVersion = 9,
        };
        File.WriteAllText(path, JsonSerializer.Serialize(payload));

        var result = SnippetIO.Import(db, path);
        var imported = Assert.Single(db.GetAll());

        Assert.Equal(1, result.Added);
        Assert.Equal(1, result.Skipped);
        Assert.Equal("Valid", imported.Title);
        Assert.Equal(0, imported.PasteMode);
        Assert.Equal("Work", Assert.Single(db.GetGroups()).Name);
    }

    [Fact]
    public void SettingsStore_ClampsRecentClipRetention()
    {
        using var temp = TempWorkspace.Create();
        var settings = new SettingsStore(temp.CreateDatabase());

        settings.RecentClipsMax = 100_000;
        Assert.Equal(500, settings.RecentClipsMax);

        settings.RecentClipsMax = -10;
        Assert.Equal(1, settings.RecentClipsMax);
    }

    [Fact]
    public void SingleInstanceServer_UsesPerUserPipeName()
    {
        Assert.StartsWith("TaskCopy-", SingleInstanceServer.PipeName);
        Assert.NotEqual("TaskCopy", SingleInstanceServer.PipeName);
    }

    [Fact]
    public void BackupRotator_EncryptedRotationPurgesNumberedPlaintextSlots()
    {
        using var temp = TempWorkspace.Create();
        var db = temp.CreateDatabase();
        db.Insert("Secret", "Sensitive snippet");

        BackupRotator.Rotate(db, keep: 2, encrypt: false);
        Assert.True(File.Exists(Path.Combine(temp.DirectoryPath, "snippets.bak.0.db")));

        BackupRotator.Rotate(db, keep: 2, encrypt: true, password: "correct horse battery staple");

        Assert.False(File.Exists(Path.Combine(temp.DirectoryPath, "snippets.bak.0.db")));
        Assert.True(File.Exists(Path.Combine(temp.DirectoryPath, "snippets.bak.0.enc")));

        var slot = Assert.Single(BackupRotator.ListAvailable(db, keep: 2));
        Assert.True(slot.IsEncrypted);
    }

    [Fact]
    public void SnippetDatabase_StoreEncryptionProtectsPayloadsAndRoundTrips()
    {
        using var temp = TempWorkspace.Create();
        var db = temp.CreateDatabase();
        var snippetId = db.Insert("Secret Title", "needle body secret");
        db.InsertImage("Secret Image", [0x89, 0x50, 0x4E, 0x47], 1, 1);
        db.RecordBodyHistory(snippetId, "history secret");
        db.InsertRecentClip("recent secret", maxKeep: 10);

        var token = StoreCrypto.MakePasswordToken("correct horse battery staple");
        Assert.True(StoreCrypto.TryDeriveKey(token, "correct horse battery staple", out var key));
        try
        {
            db.EnableStoreEncryption(token, key);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }

        Assert.True(new SettingsStore(db).StoreEncrypted);
        Assert.False(db.TrySearchFtsIds("needle", out _));
        Assert.Equal("Secret Title", db.GetAll().First(s => s.Id == snippetId).Title);
        Assert.Equal("history secret", Assert.Single(db.GetBodyHistory(snippetId)).Body);
        Assert.Equal("recent secret", Assert.Single(db.GetRecentClips(10)).Body);
        AssertRawStoreDoesNotContain(temp.DirectoryPath, "Secret Title");
        AssertRawStoreDoesNotContain(temp.DirectoryPath, "needle body secret");
        AssertRawStoreDoesNotContain(temp.DirectoryPath, "history secret");
        AssertRawStoreDoesNotContain(temp.DirectoryPath, "recent secret");

        db.ClearStoreEncryptionKey();
        var locked = temp.CreateDatabase();
        Assert.Throws<InvalidOperationException>(() => locked.GetAll());

        Assert.True(StoreCrypto.TryDeriveKey(new SettingsStore(locked).StorePasswordToken, "correct horse battery staple", out var unlockKey));
        try
        {
            locked.UnlockStoreEncryptionKey(unlockKey);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(unlockKey);
        }
        Assert.Equal("needle body secret", locked.GetAll().First(s => s.Id == snippetId).Body);
    }

    private sealed class TempWorkspace : IDisposable
    {
        public string DirectoryPath { get; }

        private TempWorkspace()
        {
            DirectoryPath = Path.Combine(Path.GetTempPath(), "TaskCopy.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(DirectoryPath);
        }

        public static TempWorkspace Create() => new();

        public SnippetDatabase CreateDatabase() => new(Path.Combine(DirectoryPath, "snippets.db"));

        public void Dispose()
        {
            try { Directory.Delete(DirectoryPath, recursive: true); } catch { }
        }
    }

    private static void AssertRawStoreDoesNotContain(string directory, string needle)
    {
        var needleBytes = Encoding.UTF8.GetBytes(needle);
        foreach (var path in Directory.EnumerateFiles(directory, "snippets.db*"))
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            var bytes = ms.ToArray();
            Assert.True(bytes.AsSpan().IndexOf(needleBytes) < 0, $"{Path.GetFileName(path)} still contains '{needle}'.");
        }
    }
}
