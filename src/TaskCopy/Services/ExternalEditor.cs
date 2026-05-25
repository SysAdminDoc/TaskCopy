using System.Diagnostics;
using System.IO;

namespace TaskCopy.Services;

/// <summary>
/// I40: spawn the user's preferred external editor on the current snippet body,
/// then read the file back when the editor closes. Resolution order for the
/// editor command:
///   1. SettingsStore.ExternalEditorCommand if non-empty.
///   2. $EDITOR environment variable.
///   3. `code` (VS Code if on PATH).
///   4. `notepad.exe` (always present on Windows).
///
/// We DO NOT poll the file mid-edit. The editor process exit signals "done"
/// — works for `notepad`, `notepad++ -multiInst -nosession`, `code --wait`,
/// etc. VS Code without --wait returns immediately; in that case the user
/// gets the file path on their clipboard as a manual reload hint.
/// </summary>
public static class ExternalEditor
{
    /// <summary>
    /// Open <paramref name="initialBody"/> in the external editor and return
    /// the post-edit body. Returns null if the editor couldn't be launched.
    /// </summary>
    public static string? EditBody(string initialBody, string? configuredCommand = null)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"taskcopy-edit-{Guid.NewGuid():N}.txt");
        try
        {
            File.WriteAllText(tempPath, initialBody);

            var (fileName, args) = ResolveCommand(configuredCommand, tempPath);
            using var proc = Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = false,
            });
            if (proc is null) return null;

            // 30-minute upper bound — editors that detach (`code` without --wait)
            // return almost immediately. Anything legitimately blocking should
            // finish well under this. Beyond 30 min the user probably abandoned
            // the edit.
            if (!proc.WaitForExit((int)TimeSpan.FromMinutes(30).TotalMilliseconds))
            {
                try { proc.Kill(); } catch { }
            }

            // Read back. If the file was deleted by the editor (some IDEs
            // do that on cancel), fall through to "edit was abandoned."
            if (!File.Exists(tempPath)) return null;
            return File.ReadAllText(tempPath);
        }
        catch (Exception ex)
        {
            CrashLog.Write("ExternalEditor.EditBody", ex);
            return null;
        }
        finally
        {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
        }
    }

    internal static (string fileName, string args) ResolveCommand(string? configured, string filePath)
    {
        var quoted = $"\"{filePath}\"";

        if (TrySplitCommand(configured, out var configuredExe, out var configuredArgs))
        {
            return (configuredExe, string.IsNullOrEmpty(configuredArgs) ? quoted : $"{configuredArgs} {quoted}");
        }

        var envEditor = Environment.GetEnvironmentVariable("EDITOR");
        if (TrySplitCommand(envEditor, out var envExe, out var envArgs))
        {
            return (envExe, string.IsNullOrEmpty(envArgs) ? quoted : $"{envArgs} {quoted}");
        }

        // VS Code preferred; --wait so the process blocks until the user closes the tab.
        if (HasExecutable("code"))
        {
            return ("code", $"--wait --new-window {quoted}");
        }

        return ("notepad.exe", quoted);
    }

    internal static bool TrySplitCommand(string? command, out string executable, out string args)
    {
        executable = string.Empty;
        args = string.Empty;

        if (string.IsNullOrWhiteSpace(command)) return false;
        var trimmed = command.Trim();
        if (trimmed.Length == 0) return false;

        if (trimmed[0] == '"')
        {
            var close = trimmed.IndexOf('"', startIndex: 1);
            if (close > 1)
            {
                executable = trimmed[1..close];
                args = trimmed[(close + 1)..].Trim();
                return executable.Length > 0;
            }
        }

        var firstSpace = trimmed.IndexOf(' ');
        if (firstSpace < 0)
        {
            executable = trimmed;
            return true;
        }

        executable = trimmed[..firstSpace];
        args = trimmed[(firstSpace + 1)..].Trim();
        return executable.Length > 0;
    }

    private static bool HasExecutable(string name)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo
            {
                FileName = "where",
                Arguments = name,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            });
            if (p is null) return false;
            if (!p.WaitForExit(1500))
            {
                try { p.Kill(); } catch { }
                return false;
            }
            return p.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
