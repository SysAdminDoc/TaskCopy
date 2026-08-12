using System.Diagnostics;
using CommunityToolkit.Mvvm.Input;
using TaskCopy.Data;
using TaskCopy.Services;

namespace TaskCopy.ViewModels;

/// <summary>
/// Diagnostics, import/export, and data-folder commands for SettingsViewModel.
/// Keeping these commands in a partial isolates the operational surface from
/// the snippet editor and preference state without changing the binding API.
/// </summary>
public partial class SettingsViewModel
{
    [RelayCommand]
    private void OpenLogFolder()
    {
        CrashLog.OpenFolder();
        StatusMessage = $"Opened {CrashLog.LogDirectory}.";
    }

    [RelayCommand]
    private void ExportSnippets()
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            FileName = $"taskcopy-snippets-{DateTime.Now:yyyyMMdd}.json",
            Filter = "JSON (*.json)|*.json",
            DefaultExt = ".json",
            AddExtension = true,
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            var n = SnippetIO.Export(_db, dlg.FileName);
            StatusMessage = $"Exported {n} snippet{(n == 1 ? "" : "s")} to {dlg.FileName}.";
        }
        catch (Exception ex)
        {
            CrashLog.Write("ExportSnippets", ex);
            StatusMessage = $"Export failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ImportSnippets()
    {
        // F44: .taskpack is the same JSON format with a curated extension so
        // community snippet packs can register a file association and ship
        // with a recognizable name. See README "Snippet packs" section.
        // F38: .yml / .yaml routes to the Espanso importer instead so users
        // can bring an existing Espanso match library over without rewriting.
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "TaskCopy pack / snippets / Espanso YAML (*.taskpack;*.json;*.yml;*.yaml)|*.taskpack;*.json;*.yml;*.yaml"
                   + "|JSON only (*.json)|*.json"
                   + "|TaskCopy pack only (*.taskpack)|*.taskpack"
                   + "|Espanso YAML (*.yml;*.yaml)|*.yml;*.yaml",
            CheckFileExists = true,
        };
        if (dlg.ShowDialog() != true) return;

        var ext = System.IO.Path.GetExtension(dlg.FileName).ToLowerInvariant();
        if (ext is ".yml" or ".yaml")
        {
            try
            {
                var packName = System.IO.Path.GetFileNameWithoutExtension(dlg.FileName);
                var r = EspansoImport.Import(_db, dlg.FileName, packName);
                LoadFromStore();
                StatusMessage = $"Imported {r.Added} Espanso match{(r.Added == 1 ? "" : "es")}" 
                    + (r.Skipped > 0 ? $", skipped {r.Skipped} unsupported or duplicate" : "")
                    + (r.GroupsCreated > 0 ? $", created group \"{packName}\"" : "")
                    + ".";
            }
            catch (Exception ex)
            {
                CrashLog.Write("EspansoImport", ex);
                StatusMessage = $"Espanso import failed: {ex.Message}";
            }
            return;
        }
        try
        {
            var r = SnippetIO.Import(_db, dlg.FileName);
            LoadFromStore();
            StatusMessage = $"Imported {r.Added} snippet{(r.Added == 1 ? "" : "s")}" 
                + (r.Skipped > 0 ? $", skipped {r.Skipped} duplicate{(r.Skipped == 1 ? "" : "s")}" : "")
                + (r.GroupsCreated > 0 ? $", created {r.GroupsCreated} group{(r.GroupsCreated == 1 ? "" : "s")}" : "")
                + ".";
        }
        catch (Exception ex)
        {
            CrashLog.Write("ImportSnippets", ex);
            StatusMessage = $"Import failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private void OpenDataFolder()
    {
        var dir = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TaskCopy");
        try
        {
            System.IO.Directory.CreateDirectory(dir);
            Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true });
            StatusMessage = $"Opened {dir}.";
        }
        catch (Exception ex)
        {
            CrashLog.Write("OpenDataFolder", ex);
        }
    }

    /// <summary>App-level handler picks a backup slot via dialog + swaps it in.</summary>
    public event EventHandler? RestoreBackupRequested;

    [RelayCommand]
    private void RestoreBackup() => RestoreBackupRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// F52: clear the settings KV table back to defaults. Snippets/groups/trash
    /// are preserved. The relaunch returns the user to a clean Settings state.
    /// </summary>
    public event EventHandler? ResetToDefaultsRequested;

    [RelayCommand]
    private void ResetToDefaults() => ResetToDefaultsRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void CopyDiagnostics()
    {
        try
        {
            var bundle = BuildDiagnosticsMarkdown();
            System.Windows.Clipboard.SetDataObject(bundle, copy: true);
            StatusMessage = "Diagnostics bundle copied to clipboard — paste into a GitHub issue.";
        }
        catch (Exception ex)
        {
            CrashLog.Write("CopyDiagnostics", ex);
            StatusMessage = $"Couldn't build diagnostics: {ex.Message}";
        }
    }

    /// <summary>
    /// F45: short-circuits "Copy diagnostics → open browser → paste into
    /// Issues" into one click when the user has `gh` CLI on PATH.
    /// </summary>
    [RelayCommand]
    private async Task FileIssue()
    {
        StatusMessage = "Checking for gh CLI…";
        var ok = await Task.Run(() => GhCli.IsAvailable());
        if (!ok)
        {
            StatusMessage = "gh CLI not found on PATH. Diagnostics copied to clipboard — paste into a new GitHub issue.";
            CopyDiagnostics();
            return;
        }

        var bundle = BuildDiagnosticsMarkdown();
        StatusMessage = "Opening gh issue create…";
        var (success, output) = await Task.Run(() =>
        {
            var s = GhCli.TryCreateIssue("SysAdminDoc/TaskCopy", "TaskCopy bug report", bundle, out var o);
            return (s, o);
        });

        if (success)
        {
            StatusMessage = $"Issue filed: {output}";
        }
        else
        {
            StatusMessage = $"gh issue create failed ({output}). Diagnostics copied to clipboard instead.";
            CopyDiagnostics();
        }
    }

    private string BuildDiagnosticsMarkdown()
    {
        var sb = new System.Text.StringBuilder();
        var version = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown";
        var os = Environment.OSVersion.VersionString;
        var schema = Migrations.CurrentVersion;
        var snippetCount = Snippets.Count;
        var groupCount = Math.Max(0, Groups.Count - 1);
        var lastBackup = _settings.LastBackupAt == 0
            ? "(none)"
            : DateTimeOffset.FromUnixTimeSeconds(_settings.LastBackupAt).ToLocalTime().ToString("yyyy-MM-dd HH:mm");

        sb.AppendLine("```");
        sb.AppendLine($"TaskCopy diagnostics — {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}");
        sb.AppendLine($"version    : {version}");
        sb.AppendLine($"schema     : {schema}");
        sb.AppendLine($"os         : {os}");
        sb.AppendLine($"snippets   : {snippetCount}");
        sb.AppendLine($"groups     : {groupCount}");
        sb.AppendLine($"hotkey     : {HotkeyDisplay} ({(HotkeyIsRegistered ? "active" : "not registered")})");
        sb.AppendLine($"lastBackup : {lastBackup}");
        sb.AppendLine($"theme      : {_settings.Theme}");
        sb.AppendLine($"autoPaste  : {_settings.AutoPaste}");
        sb.AppendLine($"recentClips: {_settings.RecentClipsEnabled}");
        sb.AppendLine("```");

        try
        {
            if (System.IO.File.Exists(CrashLog.LogPath))
            {
                var allLines = System.IO.File.ReadAllLines(CrashLog.LogPath);
                var tail = allLines.Length > 200 ? allLines[^200..] : allLines;
                sb.AppendLine();
                sb.AppendLine("<details><summary>crash.log (last 200 lines)</summary>");
                sb.AppendLine();
                sb.AppendLine("```");
                foreach (var line in tail) sb.AppendLine(line);
                sb.AppendLine("```");
                sb.AppendLine();
                sb.AppendLine("</details>");
            }
        }
        catch { /* best-effort */ }

        return sb.ToString();
    }
}
