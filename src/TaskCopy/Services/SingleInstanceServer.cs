using System.IO;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;

namespace TaskCopy.Services;

/// <summary>
/// Listens on a named pipe so a second-launch of TaskCopy.exe can ask the
/// existing instance to do something (open settings, open the flyout) instead
/// of silently exiting after losing the single-instance mutex race. Also
/// provides the IPC primitive the planned v0.4 Windhawk taskbar mod will use.
/// </summary>
public sealed class SingleInstanceServer
{
    public static string PipeName { get; } = BuildPipeName();

    public const string MsgOpenSettings = "open-settings";
    public const string MsgOpenFlyout = "open-flyout";
    // F29: scripting / external-launcher hooks.
    // copy:<id-or-title>   → place that snippet on the clipboard, no auto-paste.
    // paste:<id-or-title>  → copy + auto-paste into the foreground window.
    // list                 → first instance writes id\ttitle pairs to %LOCALAPPDATA%\TaskCopy\snippets.list and that path is the response.
    public const string MsgCopyPrefix = "copy:";
    public const string MsgPastePrefix = "paste:";
    public const string MsgList = "list";

    private readonly Action<string> _onMessage;
    private CancellationTokenSource? _cts;

    public SingleInstanceServer(Action<string> onMessage)
    {
        _onMessage = onMessage;
    }

    public void Start()
    {
        _cts = new CancellationTokenSource();
        _ = Task.Run(() => RunAsync(_cts.Token));
    }

    public void Stop()
    {
        try { _cts?.Cancel(); } catch { }
        _cts?.Dispose();
        _cts = null;
    }

    private async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(
                    PipeName,
                    PipeDirection.In,
                    maxNumberOfServerInstances: 1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await server.WaitForConnectionAsync(ct).ConfigureAwait(false);
                using var reader = new StreamReader(server, Encoding.UTF8);
                var line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(line))
                {
                    try { _onMessage(line.Trim()); } catch (Exception ex) { CrashLog.Write("SingleInstanceServer.OnMessage", ex); }
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                CrashLog.Write("SingleInstanceServer.Run", ex);
                await Task.Delay(250, CancellationToken.None).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Best-effort signal to the first instance. Returns true if the message
    /// was written; false if no listener answered within the timeout.
    /// </summary>
    public static bool TrySend(string message, int timeoutMs = 500)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(timeoutMs);
            using var writer = new StreamWriter(client, Encoding.UTF8);
            writer.WriteLine(message);
            writer.Flush();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Map CLI arguments to the message a second-launch should send.
    /// Default (no args) → open Settings, which is the most common reason
    /// someone double-launches the .exe.
    /// </summary>
    public static string ParseCliMessage(string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            var v = args[i].Trim();
            var vLower = v.ToLowerInvariant();
            if (vLower is "--flyout" or "-flyout" or "/flyout") return MsgOpenFlyout;
            if (vLower is "--settings" or "-settings" or "/settings") return MsgOpenSettings;
            if (vLower is "--list" or "-list" or "/list") return MsgList;
            if (vLower is "--copy" or "-copy" or "/copy")
            {
                var arg = i + 1 < args.Length ? args[i + 1] : string.Empty;
                return MsgCopyPrefix + arg;
            }
            if (vLower is "--paste" or "-paste" or "/paste")
            {
                var arg = i + 1 < args.Length ? args[i + 1] : string.Empty;
                return MsgPastePrefix + arg;
            }
        }
        return MsgOpenSettings;
    }

    private static string BuildPipeName()
    {
        string? identity = null;
        try { identity = WindowsIdentity.GetCurrent().User?.Value; } catch { }
        identity ??= Environment.UserName ?? "unknown";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(identity));
        return "TaskCopy-" + Convert.ToHexString(hash.AsSpan(0, 8)).ToLowerInvariant();
    }
}
