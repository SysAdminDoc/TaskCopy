using System.Diagnostics;
using System.IO;

namespace TaskCopy.Services;

/// <summary>
/// F45: minimal integration with GitHub's `gh` CLI. Used by the "File issue"
/// button to short-circuit the "Copy diagnostics → open browser → paste"
/// flow into a single click when `gh` is on PATH and authenticated.
///
/// All checks are best-effort — when `gh` isn't available or auth fails,
/// the caller falls back to the existing clipboard-Markdown path.
/// </summary>
public static class GhCli
{
    /// <summary>True if the `gh` CLI is present on PATH.</summary>
    public static bool IsAvailable()
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo
            {
                FileName = "gh",
                Arguments = "--version",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            });
            if (p is null) return false;
            // Wait up to 2 s — `gh --version` is essentially instant; longer
            // means something is wrong with the PATH entry (maybe pointing at
            // a stub that hangs). Bail rather than block the dispatcher.
            if (!p.WaitForExit(2000))
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

    /// <summary>
    /// Create a GitHub issue against the given repo with the supplied
    /// title + body. Body lives in a temp file so it survives large
    /// payloads and quoting concerns. Returns true on success.
    ///
    /// Doesn't try to detect auth failures inline — `gh` prints those to
    /// stderr and exits non-zero, which we surface to the caller as `false`.
    /// </summary>
    public static bool TryCreateIssue(string repo, string title, string body, out string output)
    {
        output = string.Empty;
        var tempBodyPath = Path.Combine(Path.GetTempPath(), $"taskcopy-issue-{Guid.NewGuid():N}.md");
        try
        {
            File.WriteAllText(tempBodyPath, body);

            using var p = Process.Start(new ProcessStartInfo
            {
                FileName = "gh",
                Arguments = $"issue create --repo {repo} --title \"{EscapeForCli(title)}\" --body-file \"{tempBodyPath}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            });
            if (p is null) return false;

            if (!p.WaitForExit(30_000))
            {
                try { p.Kill(); } catch { }
                output = "gh issue create timed out after 30 s.";
                return false;
            }

            output = (p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd()).Trim();
            return p.ExitCode == 0;
        }
        catch (Exception ex)
        {
            output = ex.Message;
            return false;
        }
        finally
        {
            try { if (File.Exists(tempBodyPath)) File.Delete(tempBodyPath); } catch { }
        }
    }

    /// <summary>
    /// Cheap shell-escape: collapse embedded double quotes. The title is
    /// always under TaskCopy's control (we generate it) so this is defensive
    /// rather than load-bearing.
    /// </summary>
    private static string EscapeForCli(string s) => s.Replace("\"", "'");
}
