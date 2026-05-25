using System.Windows.Input;

namespace TaskCopy.Data;

public sealed class SettingsStore
{
    private const string KeyHotkeyKey = "hotkey.key";
    private const string KeyHotkeyModifiers = "hotkey.modifiers";
    private const string KeyStartWithWindows = "startup.enabled";
    private const string KeyAutoPaste = "behavior.autopaste";
    private const string KeyFirstRunComplete = "firstrun.complete";
    private const string KeyFlyoutSortMode = "flyout.sort_mode";
    private const string KeyRecentClipsEnabled = "recent_clips.enabled";
    private const string KeyRecentClipsMax = "recent_clips.max";
    private const string KeyTheme = "theme";
    private const string KeyLastBackupAt = "backup.last_at";
    private const string KeyFlyoutLastGroupId = "flyout.last_group_id";
    private const string KeyFlyoutPosition = "flyout.position";
    private const string KeyDeleteSkipConfirm = "delete.skip_confirm";

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

    public bool IsFirstRunComplete
        => string.Equals(_db.GetSetting(KeyFirstRunComplete), "1", StringComparison.Ordinal);

    public void MarkFirstRunComplete() => _db.SetSetting(KeyFirstRunComplete, "1");

    public FlyoutSortMode FlyoutSortMode
    {
        get
        {
            var v = _db.GetSetting(KeyFlyoutSortMode);
            return Enum.TryParse<FlyoutSortMode>(v, out var m) ? m : FlyoutSortMode.Manual;
        }
        set => _db.SetSetting(KeyFlyoutSortMode, value.ToString());
    }

    public bool RecentClipsEnabled
    {
        // Off by default — opt-in (privacy + name-says-snippets posture).
        get => string.Equals(_db.GetSetting(KeyRecentClipsEnabled), "1", StringComparison.Ordinal);
        set => _db.SetSetting(KeyRecentClipsEnabled, value ? "1" : "0");
    }

    public int RecentClipsMax
    {
        get
        {
            var v = _db.GetSetting(KeyRecentClipsMax);
            return int.TryParse(v, out var n) && n > 0 ? n : 50;
        }
        set => _db.SetSetting(KeyRecentClipsMax, value.ToString());
    }

    public Theme Theme
    {
        get
        {
            var v = _db.GetSetting(KeyTheme);
            return Enum.TryParse<Theme>(v, ignoreCase: true, out var t) ? t : Theme.Mocha;
        }
        set => _db.SetSetting(KeyTheme, value.ToString());
    }

    /// <summary>
    /// Unix-seconds timestamp of the last successful BackupRotator.Rotate.
    /// Zero means "no backup yet" (or pre-throttle install).
    /// </summary>
    public long LastBackupAt
    {
        get => long.TryParse(_db.GetSetting(KeyLastBackupAt), out var v) ? v : 0L;
        set => _db.SetSetting(KeyLastBackupAt, value.ToString());
    }

    /// <summary>
    /// Last selected group filter in the flyout. 0 = "All", -1 = "Ungrouped",
    /// positive = group id. Persists across flyout opens so muscle memory holds.
    /// </summary>
    public long FlyoutLastGroupId
    {
        get => long.TryParse(_db.GetSetting(KeyFlyoutLastGroupId), out var v) ? v : 0L;
        set => _db.SetSetting(KeyFlyoutLastGroupId, value.ToString());
    }

    /// <summary>
    /// Where the flyout opens. Cursor = default (above-and-left of pointer);
    /// MonitorCenter = horizontally centered on the cursor's active monitor.
    /// </summary>
    public FlyoutPosition FlyoutPosition
    {
        get
        {
            var v = _db.GetSetting(KeyFlyoutPosition);
            return Enum.TryParse<FlyoutPosition>(v, ignoreCase: true, out var p) ? p : FlyoutPosition.Cursor;
        }
        set => _db.SetSetting(KeyFlyoutPosition, value.ToString());
    }

    /// <summary>
    /// F47: when true, skip the delete-confirm modal. Set via the
    /// "Don't ask again" checkbox in the confirm dialog itself. Resettable
    /// by F52 "Reset to defaults" or by deleting this row directly.
    /// </summary>
    public bool DeleteSkipConfirm
    {
        get => string.Equals(_db.GetSetting(KeyDeleteSkipConfirm), "1", StringComparison.Ordinal);
        set => _db.SetSetting(KeyDeleteSkipConfirm, value ? "1" : "0");
    }
}

public enum FlyoutPosition
{
    Cursor = 0,
    MonitorCenter = 1,
}

public enum Theme
{
    Mocha = 0,
    Latte = 1,
    Auto = 2,
    /// <summary>F42: delegates every brush to SystemColors so Windows HC themes drive the look.</summary>
    HighContrast = 3,
}

public enum FlyoutSortMode
{
    Manual = 0,
    MostUsed = 1,
    RecentlyUsed = 2,
}
