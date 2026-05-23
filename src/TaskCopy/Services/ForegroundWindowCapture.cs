namespace TaskCopy.Services;

/// <summary>
/// Captures the foreground HWND just before TaskCopy shows its own flyout, so
/// v0.2's auto-paste can restore focus to the right window.
/// </summary>
public sealed class ForegroundWindowCapture
{
    public IntPtr LastForegroundWindow { get; private set; }

    public void Capture()
    {
        var hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd != IntPtr.Zero) LastForegroundWindow = hwnd;
    }

    public bool TryRestore()
    {
        return LastForegroundWindow != IntPtr.Zero
               && NativeMethods.SetForegroundWindow(LastForegroundWindow);
    }
}
