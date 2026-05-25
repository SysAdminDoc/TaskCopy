using System.Runtime.InteropServices;
using TaskCopy.Data;

namespace TaskCopy.Services;

/// <summary>
/// Restores the previously focused window (captured by ForegroundWindowCapture)
/// and synthesises a Ctrl+V keystroke via SendInput so the snippet just placed
/// on the clipboard pastes into the right place. No-ops if AutoPaste is off,
/// if no foreground HWND was captured, or if SetForegroundWindow refuses
/// (e.g. the target is elevated and we are not).
/// </summary>
public sealed class AutoPasteService
{
    private readonly ForegroundWindowCapture _capture;
    private readonly SettingsStore _settings;

    public AutoPasteService(ForegroundWindowCapture capture, SettingsStore settings)
    {
        _capture = capture;
        _settings = settings;
    }

    public bool TryAutoPaste()
    {
        if (!_settings.AutoPaste) return false;
        if (!_capture.TryRestore()) return false;

        // Tiny settle so the foreground swap actually completes before the
        // input gets routed; without this, the synthetic Ctrl+V can be
        // delivered to TaskCopy's own (already-closing) window.
        Thread.Sleep(30);

        return SendCtrlV();
    }

    private static bool SendCtrlV()
    {
        var inputs = new NativeMethods.INPUT[4];

        inputs[0].type = NativeMethods.INPUT_KEYBOARD;
        inputs[0].u.ki.wVk = NativeMethods.VK_CONTROL;

        inputs[1].type = NativeMethods.INPUT_KEYBOARD;
        inputs[1].u.ki.wVk = NativeMethods.VK_V;

        inputs[2].type = NativeMethods.INPUT_KEYBOARD;
        inputs[2].u.ki.wVk = NativeMethods.VK_V;
        inputs[2].u.ki.dwFlags = NativeMethods.KEYEVENTF_KEYUP;

        inputs[3].type = NativeMethods.INPUT_KEYBOARD;
        inputs[3].u.ki.wVk = NativeMethods.VK_CONTROL;
        inputs[3].u.ki.dwFlags = NativeMethods.KEYEVENTF_KEYUP;

        var sent = NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
        return sent == inputs.Length;
    }
}
