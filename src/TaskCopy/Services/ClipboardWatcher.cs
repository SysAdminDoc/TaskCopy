using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace TaskCopy.Services;

/// <summary>
/// Listens for WM_CLIPBOARDUPDATE on a hidden host window and reports
/// captured plain-text clipboard contents to the caller. Skips contents
/// flagged with Microsoft's clipboard-exclusion formats (used by password
/// managers and banking apps) and skips items larger than the configured
/// cap so casual copies don't fill the DB with massive payloads.
/// </summary>
public sealed class ClipboardWatcher : IDisposable
{
    private const int WM_CLIPBOARDUPDATE = 0x031D;
    private const int MaxBytesPerClip = 10 * 1024;
    private const string ExcludeFormat = "ExcludeClipboardContentFromMonitors";
    private const string CanIncludeFormat = "CanIncludeInClipboardHistory";

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AddClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

    private readonly Window _host;
    private HwndSource? _source;
    private IntPtr _hwnd = IntPtr.Zero;
    private bool _disposed;
    private string? _suppressBody;

    public event EventHandler<string>? Captured;

    public ClipboardWatcher(Window host)
    {
        _host = host;
    }

    public void Start()
    {
        if (_source is not null) return;
        var helper = new WindowInteropHelper(_host);
        helper.EnsureHandle();
        _hwnd = helper.Handle;
        _source = HwndSource.FromHwnd(_hwnd);
        if (_source is null) return;
        _source.AddHook(WndProc);
        AddClipboardFormatListener(_hwnd);
    }

    public void Stop()
    {
        if (_source is null) return;
        try { RemoveClipboardFormatListener(_hwnd); } catch { }
        _source.RemoveHook(WndProc);
        _source = null;
        _hwnd = IntPtr.Zero;
    }

    /// <summary>
    /// Mark the next clipboard write as one TaskCopy initiated so the watcher
    /// doesn't re-record it as a "recent clip" (which would loop on copy).
    /// </summary>
    public void SuppressNext(string body) => _suppressBody = body;

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WM_CLIPBOARDUPDATE) return IntPtr.Zero;
        try
        {
            ProcessClipboardUpdate();
        }
        catch (Exception ex)
        {
            CrashLog.Write("ClipboardWatcher.ProcessClipboardUpdate", ex);
        }
        return IntPtr.Zero;
    }

    private void ProcessClipboardUpdate()
    {
        if (Clipboard.ContainsData(ExcludeFormat)) return;
        if (Clipboard.ContainsData(CanIncludeFormat))
        {
            // Format present implies the source app set the preference;
            // we honor the explicit "do not include" signal by skipping.
            return;
        }
        if (!Clipboard.ContainsText()) return;

        string body;
        try { body = Clipboard.GetText() ?? string.Empty; }
        catch { return; }

        if (string.IsNullOrEmpty(body)) return;
        if (System.Text.Encoding.UTF8.GetByteCount(body) > MaxBytesPerClip) return;

        if (_suppressBody is { } pending && pending == body)
        {
            _suppressBody = null;
            return;
        }

        Captured?.Invoke(this, body);
    }

    public void Dispose()
    {
        if (_disposed) return;
        Stop();
        _disposed = true;
    }
}
