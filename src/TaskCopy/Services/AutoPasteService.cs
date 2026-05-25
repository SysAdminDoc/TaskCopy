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

    public enum Result
    {
        /// <summary>AutoPaste setting is off; we intentionally did nothing.</summary>
        Skipped,
        /// <summary>Pasted Ctrl+V into the previous foreground window.</summary>
        Pasted,
        /// <summary>Couldn't restore previous foreground — most commonly because the
        /// target window is elevated and we're not. Text is on the clipboard;
        /// the user has to Ctrl+V manually.</summary>
        ForegroundRestoreFailed,
        /// <summary>Foreground restored but the synthesised keystroke didn't deliver.
        /// Rare — typically a SendInput driver-queue issue.</summary>
        SendInputFailed,
    }

    public AutoPasteService(ForegroundWindowCapture capture, SettingsStore settings)
    {
        _capture = capture;
        _settings = settings;
    }

    /// <summary>Backward-compatible boolean entry point; prefer TryAutoPasteDetailedAsync.</summary>
    public bool TryAutoPaste(int? cursorOffsetFromEnd = null)
        => TryAutoPasteDetailed(cursorOffsetFromEnd) == Result.Pasted;

    /// <summary>Legacy synchronous overload — keeps small Thread.Sleep delays for callers that aren't async-aware yet.</summary>
    public Result TryAutoPasteDetailed(int? cursorOffsetFromEnd = null, string? typedBody = null, int pasteMode = 0)
    {
        // Block on the async path on a background thread so any caller still
        // running on the dispatcher avoids the inline Thread.Sleep. Wait is
        // bounded by the inner Task.Delay's (≤ 50ms total).
        return Task.Run(() => TryAutoPasteDetailedAsync(cursorOffsetFromEnd, typedBody, pasteMode)).GetAwaiter().GetResult();
    }

    /// <summary>
    /// I22: async paste path so the dispatcher doesn't block on the 30+15ms
    /// settle delays. Callers on the dispatcher should await this directly.
    /// </summary>
    public async Task<Result> TryAutoPasteDetailedAsync(int? cursorOffsetFromEnd = null, string? typedBody = null, int pasteMode = 0)
    {
        if (!_settings.AutoPaste) return Result.Skipped;
        if (!_capture.TryRestore()) return Result.ForegroundRestoreFailed;

        // Tiny settle so the foreground swap actually completes before the
        // input gets routed; without this, the synthetic Ctrl+V can be
        // delivered to TaskCopy's own (already-closing) window.
        await Task.Delay(30).ConfigureAwait(false);

        // F24: type characters via INPUT_KEYBOARD/KEYEVENTF_UNICODE instead of
        // Ctrl+V for snippets bound to apps that swallow paste. The body is
        // already on the clipboard so the user can still paste manually if the
        // unicode typing fails partway.
        if (pasteMode == 1 && !string.IsNullOrEmpty(typedBody))
        {
            if (!SendAsUnicodeTyping(typedBody!)) return Result.SendInputFailed;
        }
        else
        {
            if (!SendCtrlV()) return Result.SendInputFailed;
        }

        if (cursorOffsetFromEnd is int n && n > 0)
        {
            // Brief settle so the paste actually lands before we move the caret.
            await Task.Delay(15).ConfigureAwait(false);
            SendLeftArrows(n);
        }
        return Result.Pasted;
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

    /// <summary>
    /// F24 — type the body character-by-character via INPUT_KEYBOARD with
    /// KEYEVENTF_UNICODE. wVk=0, wScan=codepoint. Non-BMP code points (e.g.
    /// emoji) are sent as their UTF-16 surrogate pair, each as a separate
    /// INPUT entry. Capped at 5,000 characters so a runaway snippet can't
    /// pin the keyboard.
    /// </summary>
    private static bool SendAsUnicodeTyping(string body)
    {
        const int Cap = 5000;
        if (body.Length > Cap) body = body[..Cap];

        // 2 INPUT entries per UTF-16 code unit (down + up).
        var inputs = new NativeMethods.INPUT[body.Length * 2];
        for (int i = 0; i < body.Length; i++)
        {
            ushort scan = body[i];

            inputs[i * 2].type = NativeMethods.INPUT_KEYBOARD;
            inputs[i * 2].u.ki.wVk = 0;
            inputs[i * 2].u.ki.wScan = scan;
            inputs[i * 2].u.ki.dwFlags = NativeMethods.KEYEVENTF_UNICODE;

            inputs[i * 2 + 1].type = NativeMethods.INPUT_KEYBOARD;
            inputs[i * 2 + 1].u.ki.wVk = 0;
            inputs[i * 2 + 1].u.ki.wScan = scan;
            inputs[i * 2 + 1].u.ki.dwFlags = NativeMethods.KEYEVENTF_UNICODE | NativeMethods.KEYEVENTF_KEYUP;
        }
        var sent = NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
        return sent == inputs.Length;
    }

    /// <summary>Fires when SendLeftArrows had to clamp the requested count (B14).</summary>
    public event EventHandler<int>? CursorOffsetClamped;

    private void SendLeftArrows(int count)
    {
        // Cap to a sane upper bound so a malformed snippet can't pin the
        // keyboard with thousands of arrow presses.
        const int Max = 5000;
        if (count > Max)
        {
            CursorOffsetClamped?.Invoke(this, count);
            count = Max;
        }
        var inputs = new NativeMethods.INPUT[count * 2];
        for (int i = 0; i < count; i++)
        {
            inputs[i * 2].type = NativeMethods.INPUT_KEYBOARD;
            inputs[i * 2].u.ki.wVk = NativeMethods.VK_LEFT;
            inputs[i * 2 + 1].type = NativeMethods.INPUT_KEYBOARD;
            inputs[i * 2 + 1].u.ki.wVk = NativeMethods.VK_LEFT;
            inputs[i * 2 + 1].u.ki.dwFlags = NativeMethods.KEYEVENTF_KEYUP;
        }
        NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
    }
}
