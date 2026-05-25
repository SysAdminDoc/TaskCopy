using System.Windows;
using System.Windows.Input;
using NHotkey;
using NHotkey.Wpf;

namespace TaskCopy.Services;

public sealed class HotkeyService
{
    private const string PrimaryHotkeyId = "TaskCopy.ShowSnippetMenu";
    private const string SnippetHotkeyPrefix = "TaskCopy.Snippet:";

    public event EventHandler? Triggered;
    public event EventHandler<long>? SnippetTriggered;
    public event EventHandler<string>? RegistrationFailed;
    /// <summary>Fires whenever the primary hotkey changes registration state.</summary>
    public event EventHandler<bool>? PrimaryRegistrationChanged;

    /// <summary>True if the primary global hotkey is currently registered.</summary>
    public bool IsPrimaryRegistered { get; private set; }

    public bool TryRegister(Key key, ModifierKeys modifiers)
    {
        try
        {
            HotkeyManager.Current.AddOrReplace(PrimaryHotkeyId, key, modifiers, OnHotkey);
            SetPrimaryRegistered(true);
            return true;
        }
        catch (HotkeyAlreadyRegisteredException ex)
        {
            SetPrimaryRegistered(false);
            RegistrationFailed?.Invoke(this, $"Hotkey already in use: {FormatHotkey(key, modifiers)} ({ex.Message})");
            return false;
        }
        catch (Exception ex)
        {
            SetPrimaryRegistered(false);
            RegistrationFailed?.Invoke(this, $"Could not register hotkey: {ex.Message}");
            return false;
        }
    }

    public void Unregister()
    {
        try { HotkeyManager.Current.Remove(PrimaryHotkeyId); }
        catch { /* nothing to do */ }
        SetPrimaryRegistered(false);
    }

    private void SetPrimaryRegistered(bool value)
    {
        if (IsPrimaryRegistered == value) return;
        IsPrimaryRegistered = value;
        PrimaryRegistrationChanged?.Invoke(this, value);
    }

    public bool TryRegisterSnippet(long snippetId, string hotkeyString)
    {
        if (!TryParseHotkey(hotkeyString, out var key, out var modifiers)) return false;
        var id = SnippetHotkeyPrefix + snippetId;
        try
        {
            HotkeyManager.Current.AddOrReplace(id, key, modifiers, (_, e) =>
            {
                SnippetTriggered?.Invoke(this, snippetId);
                e.Handled = true;
            });
            return true;
        }
        catch (Exception ex)
        {
            RegistrationFailed?.Invoke(this, $"Snippet hotkey {hotkeyString} could not be registered: {ex.Message}");
            return false;
        }
    }

    public void UnregisterSnippet(long snippetId)
    {
        try { HotkeyManager.Current.Remove(SnippetHotkeyPrefix + snippetId); }
        catch { /* tolerate absence */ }
    }

    /// <summary>
    /// Convenience: bulk-register every snippet whose quick_hotkey column is set.
    /// Skips entries that fail; failures are surfaced via RegistrationFailed.
    /// </summary>
    public void RegisterAllSnippets(IEnumerable<(long Id, string? Hotkey)> snippets)
    {
        foreach (var (id, hotkey) in snippets)
        {
            if (string.IsNullOrWhiteSpace(hotkey)) continue;
            TryRegisterSnippet(id, hotkey!);
        }
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

    /// <summary>
    /// Parse a hotkey string like "Ctrl+Alt+1" or "Ctrl + Shift + F12" back into
    /// (Key, ModifierKeys). Case-insensitive. Returns false if no key was found.
    /// </summary>
    public static bool TryParseHotkey(string s, out Key key, out ModifierKeys modifiers)
    {
        key = Key.None;
        modifiers = ModifierKeys.None;
        if (string.IsNullOrWhiteSpace(s)) return false;

        var parts = s.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        foreach (var raw in parts)
        {
            var p = raw.Trim();
            var lower = p.ToLowerInvariant();
            switch (lower)
            {
                case "ctrl":
                case "control":
                    modifiers |= ModifierKeys.Control; continue;
                case "alt":
                    modifiers |= ModifierKeys.Alt; continue;
                case "shift":
                    modifiers |= ModifierKeys.Shift; continue;
                case "win":
                case "windows":
                    modifiers |= ModifierKeys.Windows; continue;
            }

            // Number rows: 1..9 -> D1..D9
            if (p.Length == 1 && p[0] >= '0' && p[0] <= '9')
            {
                key = (Key)Enum.Parse(typeof(Key), "D" + p);
                continue;
            }

            if (Enum.TryParse<Key>(p, ignoreCase: true, out var k))
            {
                key = k;
                continue;
            }
            return false;
        }
        return key != Key.None;
    }
}
