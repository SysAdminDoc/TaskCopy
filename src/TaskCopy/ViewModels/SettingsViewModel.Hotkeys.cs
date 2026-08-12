using System.Windows.Input;
using TaskCopy.Services;

namespace TaskCopy.ViewModels;

/// <summary>
/// Primary hotkey registration and startup/preference side effects. The UI
/// still binds to the generated properties declared by SettingsViewModel, but
/// the native registration policy is isolated from editor and diagnostics.
/// </summary>
public partial class SettingsViewModel
{
    public void SetHotkey(Key key, ModifierKeys modifiers)
    {
        var previousKey = HotkeyKey;
        var previousModifiers = HotkeyModifiers;

        if (_hotkeys.TryRegister(key, modifiers))
        {
            HotkeyKey = key;
            HotkeyModifiers = modifiers;
            _settings.HotkeyKey = key;
            _settings.HotkeyModifiers = modifiers;
            HotkeyDisplay = HotkeyService.FormatHotkey(key, modifiers);
            StatusMessage = $"Hotkey set to {HotkeyDisplay}.";
            return;
        }

        // Registration failed — keep the previous combo working and persisted.
        _hotkeys.TryRegister(previousKey, previousModifiers);
        var attempted = HotkeyService.FormatHotkey(key, modifiers);
        StatusMessage = $"Hotkey {attempted} could not be registered — kept {HotkeyDisplay}. Try another combo.";
    }

    partial void OnStartWithWindowsChanged(bool value)
    {
        // B15: the registry is authoritative for next-launch behavior. Keep
        // the SettingsStore mirror in sync for diagnostics and exporters.
        _startup.SetEnabled(value);
        _settings.StartWithWindows = _startup.IsEnabled;
        StatusMessage = value ? "TaskCopy will start with Windows." : "TaskCopy will not start with Windows.";
    }

    partial void OnAutoPasteChanged(bool value)
    {
        _settings.AutoPaste = value;
        StatusMessage = value ? "Auto-paste enabled." : "Auto-paste disabled.";
    }

    partial void OnRecentClipsEnabledChanged(bool value)
    {
        ToggleRecentClipsRequested?.Invoke(this, value);
        StatusMessage = value
            ? "Recent clipboard auto-capture is ON. Items flagged 'do not include' are still excluded."
            : "Recent clipboard auto-capture is OFF.";
    }

    public event EventHandler<bool>? ToggleRecentClipsRequested;
}
