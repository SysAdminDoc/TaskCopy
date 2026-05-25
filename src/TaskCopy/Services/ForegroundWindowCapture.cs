using System.Diagnostics;

namespace TaskCopy.Services;

/// <summary>
/// Captures the foreground HWND just before TaskCopy shows its own flyout, so
/// auto-paste can restore focus to the right window. Ignores HWNDs owned by
/// the current TaskCopy process so the tray icon / flyout / settings window
/// can't be captured as the "previous foreground."
/// </summary>
public sealed class ForegroundWindowCapture
{
    private readonly uint _ownPid = (uint)Environment.ProcessId;

    public IntPtr LastForegroundWindow { get; private set; }

    public void Capture()
    {
        var hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return;
        NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);
        if (pid == _ownPid) return;
        LastForegroundWindow = hwnd;
    }

    public bool TryRestore()
    {
        return LastForegroundWindow != IntPtr.Zero
               && NativeMethods.SetForegroundWindow(LastForegroundWindow);
    }

    /// <summary>
    /// F48: resolve the captured HWND to a process name (without extension).
    /// Returns null when the HWND is gone, the PID can't be opened, or any
    /// step fails — paste-target tracking is best-effort and never blocking.
    /// </summary>
    public string? TryGetLastTargetProcessName()
    {
        if (LastForegroundWindow == IntPtr.Zero) return null;
        try
        {
            NativeMethods.GetWindowThreadProcessId(LastForegroundWindow, out var pid);
            if (pid == 0) return null;
            using var proc = Process.GetProcessById((int)pid);
            // ProcessName is exe-without-extension on Windows ("notepad" not "notepad.exe").
            return proc.ProcessName + ".exe";
        }
        catch
        {
            return null;
        }
    }
}
