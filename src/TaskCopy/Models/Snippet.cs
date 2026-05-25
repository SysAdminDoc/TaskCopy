using CommunityToolkit.Mvvm.ComponentModel;

namespace TaskCopy.Models;

public partial class Snippet : ObservableObject
{
    [ObservableProperty]
    private long _id;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Preview))]
    private string _title = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Preview))]
    private string _body = string.Empty;

    [ObservableProperty]
    private int _sortOrder;

    [ObservableProperty]
    private long _createdAt;

    [ObservableProperty]
    private string? _quickHotkey;

    [ObservableProperty]
    private long _usedCount;

    [ObservableProperty]
    private long? _lastUsedAt;

    [ObservableProperty]
    private bool _pinned;

    [ObservableProperty]
    private bool _isMonospace;

    [ObservableProperty]
    private long? _groupId;

    [ObservableProperty]
    private long? _deletedAt;

    /// <summary>0 = Auto (Ctrl+V), 1 = Type characters (Unicode SendInput). See F24.</summary>
    [ObservableProperty]
    private int _pasteMode;

    /// <summary>F48: most recent process this snippet was auto-pasted into (e.g. "outlook.exe"). Foundation for future F35 per-app rules.</summary>
    [ObservableProperty]
    private string? _lastTargetProcessName;

    /// <summary>F48: timestamp (Unix seconds, UTC) of the last successful auto-paste.</summary>
    [ObservableProperty]
    private long? _lastTargetAt;

    public string Preview
    {
        get
        {
            if (string.IsNullOrEmpty(Body)) return string.Empty;
            var idx = Body.IndexOfAny(['\r', '\n']);
            var line = (idx < 0 ? Body : Body[..idx]).Trim();
            return line.Length > 80 ? line[..80] + "…" : line;
        }
    }
}
