using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskCopy.Data;
using TaskCopy.Models;
using TaskCopy.Services;

namespace TaskCopy.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly SnippetDatabase _db;
    private readonly SettingsStore _settings;
    private readonly StartupService _startup;
    private readonly HotkeyService _hotkeys;

    private readonly DispatcherTimer _saveTimer = new() { Interval = TimeSpan.FromMilliseconds(300) };
    private long? _pendingSaveId;

    public ObservableCollection<Snippet> Snippets { get; } = new();
    public ObservableCollection<SnippetGroup> Groups { get; } = new();

    /// <summary>Pseudo-group representing "no group" for the editor ComboBox.</summary>
    public static readonly SnippetGroup UngroupedSentinel = new() { Id = 0, Name = "(Ungrouped)" };

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeleteSnippetCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveUpCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveDownCommand))]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    [NotifyPropertyChangedFor(nameof(EditTitle))]
    [NotifyPropertyChangedFor(nameof(EditBody))]
    [NotifyPropertyChangedFor(nameof(EditIsMonospace))]
    [NotifyPropertyChangedFor(nameof(EditBodyFontFamily))]
    [NotifyPropertyChangedFor(nameof(EditGroup))]
    private Snippet? _selectedSnippet;

    partial void OnSelectedSnippetChanging(Snippet? value)
    {
        // Persist any pending edit before the editor swaps to a new snippet.
        FlushPendingSave();
    }

    public bool HasSelection => SelectedSnippet is not null;

    public string EditTitle
    {
        get => SelectedSnippet?.Title ?? string.Empty;
        set
        {
            if (SelectedSnippet is null) return;
            if (SelectedSnippet.Title == value) return;
            SelectedSnippet.Title = value;
            ScheduleSave(SelectedSnippet);
            OnPropertyChanged();
            DirtyChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public string EditBody
    {
        get => SelectedSnippet?.Body ?? string.Empty;
        set
        {
            if (SelectedSnippet is null) return;
            if (SelectedSnippet.Body == value) return;
            SelectedSnippet.Body = value;
            ScheduleSave(SelectedSnippet);
            OnPropertyChanged();
            DirtyChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool EditIsMonospace
    {
        get => SelectedSnippet?.IsMonospace ?? false;
        set
        {
            if (SelectedSnippet is null) return;
            if (SelectedSnippet.IsMonospace == value) return;
            SelectedSnippet.IsMonospace = value;
            try { _db.SetMonospace(SelectedSnippet.Id, value); } catch (Exception ex) { CrashLog.Write("SetMonospace", ex); }
            OnPropertyChanged();
            OnPropertyChanged(nameof(EditBodyFontFamily));
        }
    }

    public System.Windows.Media.FontFamily EditBodyFontFamily =>
        (SelectedSnippet?.IsMonospace ?? false)
            ? new System.Windows.Media.FontFamily("Cascadia Mono, Consolas, Courier New")
            : new System.Windows.Media.FontFamily("Segoe UI Variable Text, Segoe UI");

    public SnippetGroup? EditGroup
    {
        get
        {
            if (SelectedSnippet?.GroupId is not long gid) return UngroupedSentinel;
            return Groups.FirstOrDefault(g => g.Id == gid) ?? UngroupedSentinel;
        }
        set
        {
            if (SelectedSnippet is null) return;
            long? newId = (value is null || value.Id == 0) ? null : value.Id;
            if (SelectedSnippet.GroupId == newId) return;
            SelectedSnippet.GroupId = newId;
            try { _db.SetGroup(SelectedSnippet.Id, newId); } catch (Exception ex) { CrashLog.Write("SetGroup", ex); }
            OnPropertyChanged();
        }
    }

    private void ScheduleSave(Snippet snippet)
    {
        _pendingSaveId = snippet.Id;
        _saveTimer.Stop();
        _saveTimer.Tick -= OnSaveTimerTick;
        _saveTimer.Tick += OnSaveTimerTick;
        _saveTimer.Start();
    }

    private void OnSaveTimerTick(object? sender, EventArgs e) => FlushPendingSave();

    /// <summary>
    /// Persist any in-memory edits to disk now. Called on selection change,
    /// window close, and the 300 ms idle timer.
    /// </summary>
    public void FlushPendingSave()
    {
        _saveTimer.Stop();
        if (_pendingSaveId is not long id) return;
        var s = Snippets.FirstOrDefault(x => x.Id == id);
        _pendingSaveId = null;
        if (s is null) return;
        try
        {
            _db.Update(s.Id, s.Title, s.Body);
        }
        catch (Exception ex)
        {
            CrashLog.Write("SettingsViewModel.FlushPendingSave", ex);
        }
    }

    [ObservableProperty]
    private string _hotkeyDisplay = string.Empty;

    [ObservableProperty]
    private Key _hotkeyKey;

    [ObservableProperty]
    private ModifierKeys _hotkeyModifiers;

    [ObservableProperty]
    private bool _startWithWindows;

    [ObservableProperty]
    private bool _autoPaste;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public event EventHandler? DirtyChanged;
    public event EventHandler<(Key key, ModifierKeys modifiers)>? HotkeyRebindRequested;
    public event EventHandler? ManageGroupsRequested;

    [RelayCommand]
    private void ManageGroups() => ManageGroupsRequested?.Invoke(this, EventArgs.Empty);

    public SettingsViewModel(SnippetDatabase db, SettingsStore settings,
                              StartupService startup, HotkeyService hotkeys)
    {
        _db = db;
        _settings = settings;
        _startup = startup;
        _hotkeys = hotkeys;

        LoadFromStore();
    }

    public void LoadFromStore()
    {
        Snippets.Clear();
        foreach (var s in _db.GetAll()) Snippets.Add(s);
        ReloadGroups();

        HotkeyKey = _settings.HotkeyKey;
        HotkeyModifiers = _settings.HotkeyModifiers;
        HotkeyDisplay = HotkeyService.FormatHotkey(HotkeyKey, HotkeyModifiers);
        StartWithWindows = _startup.IsEnabled;
        AutoPaste = _settings.AutoPaste;
    }

    public void ReloadGroups()
    {
        Groups.Clear();
        Groups.Add(UngroupedSentinel);
        foreach (var g in _db.GetGroups()) Groups.Add(g);
        OnPropertyChanged(nameof(EditGroup));
    }

    [RelayCommand]
    private void AddSnippet()
    {
        var id = _db.Insert("New snippet", string.Empty);
        var fresh = _db.GetAll().FirstOrDefault(s => s.Id == id);
        if (fresh is null) return;
        Snippets.Add(fresh);
        SelectedSnippet = fresh;
        StatusMessage = "New snippet added.";
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void DeleteSnippet()
    {
        if (SelectedSnippet is null) return;
        var idx = Snippets.IndexOf(SelectedSnippet);
        _db.Delete(SelectedSnippet.Id);
        Snippets.RemoveAt(idx);
        SelectedSnippet = idx < Snippets.Count ? Snippets[idx]
                          : idx > 0 ? Snippets[idx - 1] : null;
        StatusMessage = "Snippet deleted.";
    }

    [RelayCommand(CanExecute = nameof(CanMoveUp))]
    private void MoveUp()
    {
        if (SelectedSnippet is null) return;
        var idx = Snippets.IndexOf(SelectedSnippet);
        if (idx <= 0) return;
        Snippets.Move(idx, idx - 1);
        PersistOrder();
    }

    [RelayCommand(CanExecute = nameof(CanMoveDown))]
    private void MoveDown()
    {
        if (SelectedSnippet is null) return;
        var idx = Snippets.IndexOf(SelectedSnippet);
        if (idx < 0 || idx >= Snippets.Count - 1) return;
        Snippets.Move(idx, idx + 1);
        PersistOrder();
    }

    private bool CanMoveUp() => SelectedSnippet is not null && Snippets.IndexOf(SelectedSnippet) > 0;
    private bool CanMoveDown() => SelectedSnippet is not null
                                  && Snippets.IndexOf(SelectedSnippet) >= 0
                                  && Snippets.IndexOf(SelectedSnippet) < Snippets.Count - 1;

    private void PersistOrder()
    {
        _db.Reorder(Snippets.Select(s => s.Id).ToList());
    }

    [RelayCommand]
    private void RebindHotkey()
    {
        HotkeyRebindRequested?.Invoke(this, (HotkeyKey, HotkeyModifiers));
    }

    [RelayCommand]
    private void OpenLogFolder()
    {
        CrashLog.OpenFolder();
        StatusMessage = $"Opened {CrashLog.LogDirectory}.";
    }

    [RelayCommand]
    private void ExportSnippets()
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            FileName = $"taskcopy-snippets-{DateTime.Now:yyyyMMdd}.json",
            Filter = "JSON (*.json)|*.json",
            DefaultExt = ".json",
            AddExtension = true,
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            var n = SnippetIO.Export(_db, dlg.FileName);
            StatusMessage = $"Exported {n} snippet{(n == 1 ? "" : "s")} to {dlg.FileName}.";
        }
        catch (Exception ex)
        {
            CrashLog.Write("ExportSnippets", ex);
            StatusMessage = $"Export failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ImportSnippets()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "JSON (*.json)|*.json",
            CheckFileExists = true,
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            var r = SnippetIO.Import(_db, dlg.FileName);
            // Reload so the new snippets appear in Settings immediately.
            LoadFromStore();
            StatusMessage = $"Imported {r.Added} snippet{(r.Added == 1 ? "" : "s")}"
                + (r.Skipped > 0 ? $", skipped {r.Skipped} duplicate{(r.Skipped == 1 ? "" : "s")}" : "")
                + (r.GroupsCreated > 0 ? $", created {r.GroupsCreated} group{(r.GroupsCreated == 1 ? "" : "s")}" : "")
                + ".";
        }
        catch (Exception ex)
        {
            CrashLog.Write("ImportSnippets", ex);
            StatusMessage = $"Import failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private void OpenDataFolder()
    {
        var dir = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TaskCopy");
        try
        {
            System.IO.Directory.CreateDirectory(dir);
            Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true });
            StatusMessage = $"Opened {dir}.";
        }
        catch (Exception ex)
        {
            CrashLog.Write("OpenDataFolder", ex);
        }
    }

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
        _startup.SetEnabled(value);
        _settings.StartWithWindows = value;
        StatusMessage = value ? "TaskCopy will start with Windows." : "TaskCopy will not start with Windows.";
    }

    partial void OnAutoPasteChanged(bool value)
    {
        _settings.AutoPaste = value;
        StatusMessage = value ? "Auto-paste enabled." : "Auto-paste disabled.";
    }
}
