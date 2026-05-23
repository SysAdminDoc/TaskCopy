using System.Windows;
using System.Windows.Input;
using NHotkey;
using NHotkey.Wpf;

namespace TaskCopy.Services;

public sealed class HotkeyService
{
    private const string HotkeyId = "TaskCopy.ShowSnippetMenu";

    public event EventHandler? Triggered;
    public event EventHandler<string>? RegistrationFailed;

    public bool TryRegister(Key key, ModifierKeys modifiers)
    {
        try
        {
            HotkeyManager.Current.AddOrReplace(HotkeyId, key, modifiers, OnHotkey);
            return true;
        }
        catch (HotkeyAlreadyRegisteredException ex)
        {
            RegistrationFailed?.Invoke(this, $"Hotkey already in use: {FormatHotkey(key, modifiers)} ({ex.Message})");
            return false;
        }
        catch (Exception ex)
        {
            RegistrationFailed?.Invoke(this, $"Could not register hotkey: {ex.Message}");
            return false;
        }
    }

    public void Unregister()
    {
        try { HotkeyManager.Current.Remove(HotkeyId); }
        catch { /* nothing to do */ }
    }

    private void OnHotkey(object? sender, HotkeyEventArgs e)
    {
        Triggered?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    public static string FormatHotkey(Key key, ModifierKeys modifiers)
    {
        var parts = new List<string>(4);
        if (modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
        parts.Add(key.ToString());
        return string.Join(" + ", parts);
    }
}
