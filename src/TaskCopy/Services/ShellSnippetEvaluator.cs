using System.Diagnostics;
using System.IO;
using System.Text;

namespace TaskCopy.Services;

public static class ShellSnippetEvaluator
{
    private const int TimeoutMs = 2_000;
    private const int MaxOutputChars = 4_096;
    private const int MaxCommandChars = 8_192;

    public static string Run(string command)
    {
        if (string.IsNullOrWhiteSpace(command)) return string.Empty;
        if (command.Length > MaxCommandChars) return "[shell command too long]";

        var scriptPath = Path.Combine(Path.GetTempPath(), $"TaskCopy-shell-{Guid.NewGuid():N}.cmd");
        try
        {
            File.WriteAllText(scriptPath, "@echo off\r\n" + command + "\r\n", Encoding.UTF8);

            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            process.StartInfo.ArgumentList.Add("/d");
            process.StartInfo.ArgumentList.Add("/q");
            process.StartInfo.ArgumentList.Add("/c");
            process.StartInfo.ArgumentList.Add(scriptPath);

            if (!process.Start()) return string.Empty;

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(TimeoutMs))
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                try { process.WaitForExit(500); } catch { }
                return "[shell timed out]";
            }

            Task.WaitAll(new Task[] { stdoutTask, stderrTask }, millisecondsTimeout: 500);
            var stdout = stdoutTask.IsCompletedSuccessfully ? stdoutTask.Result : string.Empty;
            var stderr = stderrTask.IsCompletedSuccessfully ? stderrTask.Result : string.Empty;
            var combined = process.ExitCode == 0 || string.IsNullOrWhiteSpace(stderr)
                ? stdout
                : string.IsNullOrWhiteSpace(stdout)
                    ? stderr
                    : stdout.TrimEnd() + Environment.NewLine + stderr;
            return TrimOutput(combined);
        }
        catch (Exception ex)
        {
            CrashLog.Write("ShellSnippetEvaluator.Run", ex);
            return $"[shell failed: {ex.Message}]";
        }
        finally
        {
            try { if (File.Exists(scriptPath)) File.Delete(scriptPath); } catch { }
        }
    }

    private static string TrimOutput(string output)
    {
        output = output.Replace("\r\n", "\n").Replace('\r', '\n').TrimEnd('\n');
        return output.Length <= MaxOutputChars
            ? output
            : output[..MaxOutputChars] + "\n[shell output truncated]";
    }
}
