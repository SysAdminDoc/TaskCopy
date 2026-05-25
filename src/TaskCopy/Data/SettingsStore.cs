using System.Windows.Input;

namespace TaskCopy.Data;

public sealed class SettingsStore
{
    private const string KeyHotkeyKey = "hotkey.key";
    private const string KeyHotkeyModifiers = "hotkey.modifiers";
    private const string KeyStartWithWindows = "startup.enabled";
    private const string KeyAutoPaste = "behavior.autopaste";

    private readonly SnippetDatabase _db;

    public SettingsStore(SnippetDatabase db) => _db = db;

    public Key HotkeyKey
    {
        get => Enum.TryParse<Key>(_db.GetSetting(KeyHotkeyKey), out var k) ? k : Key.V;
        set => _db.SetSetting(KeyHotkeyKey, value.ToString());
    }

    public ModifierKeys HotkeyModifiers
    {
        get => Enum.TryParse<ModifierKeys>(_db.GetSetting(KeyHotkeyModifiers), out var m)
            ? m
            : (ModifierKeys.Control | ModifierKeys.Alt);
        set => _db.SetSetting(KeyHotkeyModifiers, value.ToString());
    }

    public bool StartWithWindows
    {
        get => string.Equals(_db.GetSetting(KeyStartWithWindows), "1", StringComparison.Ordinal);
        set => _db.SetSetting(KeyStartWithWindows, value ? "1" : "0");
    }

    public bool AutoPaste
    {
        // Default ON for new installs (no setting written yet); existing
        // explicit "0" still respected.
        get
        {
            var v = _db.GetSetting(KeyAutoPaste);
            return v is null || string.Equals(v, "1", StringComparison.Ordinal);
        }
        set => _db.SetSetting(KeyAutoPaste, value ? "1" : "0");
    }
}
