using System.IO;
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

    // Per https://learn.microsoft.com/en-us/windows/win32/dataxchg/clipboard-formats#cloud-clipboard-and-clipboard-history-formats:
    //   ExcludeClipboardContentFromMonitors  — presence alone means "exclude" (no payload).
    //   CanIncludeInClipboardHistory         — 4-byte DWORD payload; 0 = exclude, 1 = include.
    //   CanUploadToCloudClipboard            — 4-byte DWORD payload; 0 = no, 1 = yes.
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
        // CanIncludeInClipboardHistory carries a 4-byte value: 0 = exclude, 1 = include.
        // Older code treated *any* presence as "exclude" and silently dropped clips
        // from apps that explicitly opt IN. Read the payload and skip only on 0.
        if (TryReadDwordFormat(CanIncludeFormat, out var canInclude) && canInclude == 0) return;
        if (!Clipboard.ContainsText()) return;

        string body;
        try { body = Clipboard.GetText() ?? string.Empty; }
        catch { return; }

        if (string.IsNullOrEmpty(body)) return;
        if (System.Text.Encoding.UTF8.GetByteCount(body) > MaxBytesPerClip) return;

        if (_suppressBody is { } pending)
        {
            _suppressBody = null;
            if (pending == body) return;
        }

        Captured?.Invoke(this, body);
    }

    /// <summary>
    /// Reads a clipboard format whose payload is a single 4-byte little-endian
    /// DWORD (the convention used by CanIncludeInClipboardHistory and
    /// CanUploadToCloudClipboard). Returns true and the value on success;
    /// false if the format isn't present or the payload doesn't look like a DWORD.
    /// </summary>
    private static bool TryReadDwordFormat(string formatName, out int value)
    {
        value = 0;
        if (!Clipboard.ContainsData(formatName)) return false;
        try
        {
            var data = Clipboard.GetData(formatName);
            byte[]? bytes = data switch
            {
                byte[] arr => arr,
                MemoryStream ms => ms.ToArray(),
                _ => null,
            };
            if (bytes is null || bytes.Length < 4) return false;
            value = BitConverter.ToInt32(bytes, 0);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        Stop();
        _disposed = true;
    }
}
