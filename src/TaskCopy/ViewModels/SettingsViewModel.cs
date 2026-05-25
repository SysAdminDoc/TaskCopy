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

    /// <summary>Right-aligned status-bar label like "12 snippets · 3 groups".</summary>
    public string SnippetCountStatus
    {
        get
        {
            var s = Snippets.Count;
            var g = Groups.Count - 1; // minus the (Ungrouped) sentinel
            if (g < 0) g = 0;
            var snippetWord = s == 1 ? "snippet" : "snippets";
            return g == 0
                ? $"{s} {snippetWord}"
                : $"{s} {snippetWord} · {g} group{(g == 1 ? "" : "s")}";
        }
    }

    /// <summary>Pseudo-group representing "no group" for the editor ComboBox.</summary>
    public static readonly SnippetGroup UngroupedSentinel = new() { Id = 0, Name = "(Ungrouped)" };

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeleteSnippetCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveUpCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveDownCommand))]
    [NotifyCanExecuteChangedFor(nameof(RebindQuickHotkeyCommand))]
    [NotifyCanExecuteChangedFor(nameof(ClearQuickHotkeyBindingCommand))]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    [NotifyPropertyChangedFor(nameof(EditTitle))]
    [NotifyPropertyChangedFor(nameof(EditBody))]
    [NotifyPropertyChangedFor(nameof(EditBodyPreview))]
    [NotifyPropertyChangedFor(nameof(EditIsMonospace))]
    [NotifyPropertyChangedFor(nameof(EditBodyFontFamily))]
    [NotifyPropertyChangedFor(nameof(EditGroup))]
    [NotifyPropertyChangedFor(nameof(EditPinned))]
    [NotifyPropertyChangedFor(nameof(EditQuickHotkeyDisplay))]
    [NotifyPropertyChangedFor(nameof(EditPasteMode))]
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
            OnPropertyChanged(nameof(EditBodyPreview));
            DirtyChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// F25: live render of SnippetTemplating.Expand on the current body so the
    /// user sees what placeholders will resolve to at paste time. Uses a sample
    /// clipboard value and stub Ask/Form responses so it stays pure.
    /// </summary>
    public string EditBodyPreview
    {
        get
        {
            var body = EditBody;
            if (string.IsNullOrEmpty(body)) return string.Empty;
            try
            {
                var ctx = new TemplatingContext
                {
                    PreviousClipboard = "<clipboard>",
                    PromptFor = field => $"<{field}>",
                    Now = DateTime.Now,
                };
                var result = SnippetTemplating.Expand(body, ctx);
                return result.Body;
            }
            catch
            {
                return body;
            }
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

    public bool EditPinned
    {
        get => SelectedSnippet?.Pinned ?? false;
        set
        {
            if (SelectedSnippet is null) return;
            if (SelectedSnippet.Pinned == value) return;
            SelectedSnippet.Pinned = value;
            try { _db.SetPinned(SelectedSnippet.Id, value); } catch (Exception ex) { CrashLog.Write("SetPinned", ex); }
            OnPropertyChanged();
        }
    }

    public IReadOnlyList<PasteModeOption> PasteModeOptions { get; } =
    [
        new(0, "Auto (Ctrl+V)"),
        new(1, "Type characters"),
    ];

    public PasteModeOption EditPasteMode
    {
        get
        {
            var v = SelectedSnippet?.PasteMode ?? 0;
            return PasteModeOptions.FirstOrDefault(o => o.Value == v) ?? PasteModeOptions[0];
        }
        set
        {
            if (SelectedSnippet is null || value is null) return;
            if (SelectedSnippet.PasteMode == value.Value) return;
            SelectedSnippet.PasteMode = value.Value;
            try { _db.SetPasteMode(SelectedSnippet.Id, value.Value); }
            catch (Exception ex) { CrashLog.Write("SetPasteMode", ex); }
            OnPropertyChanged();
        }
    }

    public sealed record PasteModeOption(int Value, string Label)
    {
        public override string ToString() => Label;
    }

    public const string QuickHotkeyNone = "(None)";

    /// <summary>Display label for the selected snippet's quick-hotkey, or "(None)".</summary>
    public string EditQuickHotkeyDisplay
    {
        get
        {
            var v = SelectedSnippet?.QuickHotkey;
            return string.IsNullOrEmpty(v) ? QuickHotkeyNone : v;
        }
    }

    /// <summary>
    /// Apply a freshly-captured quick-hotkey combo to the selected snippet.
    /// Refuses combos that clash with the primary hotkey or with the reserved
    /// set (Ctrl+C/V/X/Z/Y, Ctrl+Alt+Del). On failure the previous binding stays.
    /// </summary>
    public void SetQuickHotkey(Key key, ModifierKeys modifiers)
    {
        if (SelectedSnippet is null) return;
        var combo = HotkeyService.FormatHotkey(key, modifiers);

        // Reject if it would clash with the primary hotkey.
        if (key == HotkeyKey && modifiers == HotkeyModifiers)
        {
            StatusMessage = $"Quick hotkey {combo} clashes with your primary TaskCopy hotkey.";
            return;
        }

        // Reject a small reserved set so users don't lock themselves out of
        // standard copy/paste. (Pure modifier presses are filtered upstream.)
        if (IsReservedCombo(key, modifiers))
        {
            StatusMessage = $"Quick hotkey {combo} is reserved by Windows or common apps. Pick another.";
            return;
        }

        var snippet = SelectedSnippet;
        snippet.QuickHotkey = combo;

        try { _db.SetQuickHotkey(snippet.Id, combo); }
        catch (Exception ex) { CrashLog.Write("SetQuickHotkey", ex); }

        _hotkeys.UnregisterSnippet(snippet.Id);
        if (_hotkeys.TryRegisterSnippet(snippet.Id, combo))
        {
            StatusMessage = $"Quick hotkey {combo} set for \"{snippet.Title}\".";
        }
        else
        {
            StatusMessage = $"Quick hotkey {combo} could not be registered — already in use? Try another.";
        }
        OnPropertyChanged(nameof(EditQuickHotkeyDisplay));
    }

    /// <summary>Remove the selected snippet's quick-hotkey binding.</summary>
    public void ClearQuickHotkey()
    {
        if (SelectedSnippet is null) return;
        var snippet = SelectedSnippet;
        var prior = snippet.QuickHotkey;
        if (string.IsNullOrEmpty(prior)) return;

        snippet.QuickHotkey = null;
        try { _db.SetQuickHotkey(snippet.Id, null); }
        catch (Exception ex) { CrashLog.Write("ClearQuickHotkey", ex); }
        _hotkeys.UnregisterSnippet(snippet.Id);
        StatusMessage = $"Quick hotkey cleared for \"{snippet.Title}\".";
        OnPropertyChanged(nameof(EditQuickHotkeyDisplay));
    }

    private static bool IsReservedCombo(Key key, ModifierKeys modifiers)
    {
        // Ctrl-only: classic shortcuts the user almost always means literally.
        if (modifiers == ModifierKeys.Control)
        {
            if (key is Key.C or Key.V or Key.X or Key.Z or Key.Y or Key.A
                       or Key.S or Key.N or Key.O or Key.P or Key.F or Key.W or Key.T) return true;
        }
        // No combo with no modifiers — caller already filters this, but defensive.
        if (modifiers == ModifierKeys.None) return true;
        return false;
    }

    /// <summary>Fires when the per-snippet quick-hotkey "Capture…" button is clicked.</summary>
    public event EventHandler<Snippet>? QuickHotkeyRebindRequested;

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void RebindQuickHotkey()
    {
        if (SelectedSnippet is null) return;
        QuickHotkeyRebindRequested?.Invoke(this, SelectedSnippet);
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void ClearQuickHotkeyBinding() => ClearQuickHotkey();

    public IReadOnlyList<FlyoutSortModeOption> FlyoutSortModes { get; } =
    [
        new(FlyoutSortMode.Manual,        "Manual order"),
        new(FlyoutSortMode.MostUsed,      "Most used first (pinned on top)"),
        new(FlyoutSortMode.RecentlyUsed,  "Recently used first (pinned on top)"),
    ];

    public IReadOnlyList<ThemeOption> ThemeOptions { get; } =
    [
        new(Theme.Mocha, "Catppuccin Mocha (dark)"),
        new(Theme.Latte, "Catppuccin Latte (light)"),
        new(Theme.Auto,  "Follow system"),
    ];

    public ThemeOption SelectedTheme
    {
        get => ThemeOptions.FirstOrDefault(t => t.Value == _settings.Theme) ?? ThemeOptions[0];
        set
        {
            if (value is null) return;
            if (_settings.Theme == value.Value) return;
            _settings.Theme = value.Value;
            OnPropertyChanged();

            // I16 Option A: offer immediate-apply via relaunch since the brushes
            // are bound StaticResource and a live dictionary swap won't propagate
            // into already-shown windows. Mocha/Latte refactor to DynamicResource
            // is the Option B follow-up.
            ApplyThemeRequested?.Invoke(this, value.Label);
        }
    }

    /// <summary>App-level subscribes to offer "restart now to apply" UX.</summary>
    public event EventHandler<string>? ApplyThemeRequested;

    public FlyoutSortModeOption SelectedFlyoutSort
    {
        get => FlyoutSortModes.FirstOrDefault(m => m.Mode == _settings.FlyoutSortMode)
               ?? FlyoutSortModes[0];
        set
        {
            if (value is null) return;
            _settings.FlyoutSortMode = value.Mode;
            OnPropertyChanged();
            StatusMessage = $"Flyout order: {value.Label}.";
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
    [NotifyPropertyChangedFor(nameof(HotkeyStatusLabel))]
    [NotifyPropertyChangedFor(nameof(HotkeyStatusBrush))]
    private string _hotkeyDisplay = string.Empty;

    /// <summary>True when HotkeyService confirms the primary hotkey is registered.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HotkeyStatusLabel))]
    [NotifyPropertyChangedFor(nameof(HotkeyStatusBrush))]
    private bool _hotkeyIsRegistered;

    public string HotkeyStatusLabel => HotkeyIsRegistered ? "Active" : "Couldn't register — try another";

    public string HotkeyStatusBrushKey
        => HotkeyIsRegistered ? "Mocha.Green.Brush" : "Mocha.Red.Brush";

    public System.Windows.Media.Brush HotkeyStatusBrush
        => (System.Windows.Media.Brush)System.Windows.Application.Current.Resources[HotkeyStatusBrushKey];

    [ObservableProperty]
    private Key _hotkeyKey;

    [ObservableProperty]
    private ModifierKeys _hotkeyModifiers;

    [ObservableProperty]
    private bool _startWithWindows;

    [ObservableProperty]
    private bool _autoPaste;

    [ObservableProperty]
    private bool _recentClipsEnabled;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public sealed record FlyoutSortModeOption(FlyoutSortMode Mode, string Label)
    {
        public override string ToString() => Label;
    }

    public sealed record ThemeOption(Theme Value, string Label)
    {
        public override string ToString() => Label;
    }

    public event EventHandler? DirtyChanged;
    public event EventHandler<(Key key, ModifierKeys modifiers)>? HotkeyRebindRequested;
    public event EventHandler? ManageGroupsRequested;
    public event EventHandler? ShowTrashRequested;

    [RelayCommand]
    private void ShowTrash() => ShowTrashRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void ManageGroups() => ManageGroupsRequested?.Invoke(this, EventArgs.Empty);

    public SettingsViewModel(SnippetDatabase db, SettingsStore settings,
                              StartupService startup, HotkeyService hotkeys)
    {
        _db = db;
        _settings = settings;
        _startup = startup;
        _hotkeys = hotkeys;

        // Keep the status-bar counter in sync with the snippet + group lists.
        Snippets.CollectionChanged += (_, _) => OnPropertyChanged(nameof(SnippetCountStatus));
        Groups.CollectionChanged += (_, _) => OnPropertyChanged(nameof(SnippetCountStatus));

        // Reflect hotkey registration state in the UI badge.
        _hotkeys.PrimaryRegistrationChanged += (_, registered) =>
            System.Windows.Application.Current?.Dispatcher.Invoke(() => HotkeyIsRegistered = registered);

        LoadFromStore();
        HotkeyIsRegistered = _hotkeys.IsPrimaryRegistered;
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
        RecentClipsEnabled = _settings.RecentClipsEnabled;
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

        var title = SelectedSnippet.Title;
        var result = System.Windows.MessageBox.Show(
            $"Delete snippet \"{title}\"?\n\nIt will be moved to the trash and permanently removed after 30 days.",
            "TaskCopy",
            System.Windows.MessageBoxButton.OKCancel,
            System.Windows.MessageBoxImage.Question,
            System.Windows.MessageBoxResult.Cancel);
        if (result != System.Windows.MessageBoxResult.OK) return;

        var snippet = SelectedSnippet;
        var idx = Snippets.IndexOf(snippet);

        // Unregister any per-snippet hotkey before soft-delete so it stops firing.
        if (!string.IsNullOrEmpty(snippet.QuickHotkey)) _hotkeys.UnregisterSnippet(snippet.Id);

        _db.SoftDelete(snippet.Id);
        Snippets.RemoveAt(idx);
        SelectedSnippet = idx < Snippets.Count ? Snippets[idx]
                          : idx > 0 ? Snippets[idx - 1] : null;
        StatusMessage = $"Moved \"{title}\" to trash.";
    }

    /// <summary>
    /// Persist the current order of Snippets to the DB. Called from the
    /// SettingsWindow drag-reorder handler after a successful drop.
    /// </summary>
    public void PersistCurrentOrder() => PersistOrder();

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

    /// <summary>App-level handler picks a backup slot via dialog + swaps it in.</summary>
    public event EventHandler? RestoreBackupRequested;

    [RelayCommand]
    private void RestoreBackup() => RestoreBackupRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void CopyDiagnostics()
    {
        try
        {
            var bundle = BuildDiagnosticsMarkdown();
            System.Windows.Clipboard.SetDataObject(bundle, copy: true);
            StatusMessage = "Diagnostics bundle copied to clipboard — paste into a GitHub issue.";
        }
        catch (Exception ex)
        {
            CrashLog.Write("CopyDiagnostics", ex);
            StatusMessage = $"Couldn't build diagnostics: {ex.Message}";
        }
    }

    private string BuildDiagnosticsMarkdown()
    {
        var sb = new System.Text.StringBuilder();
        var version = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown";
        var os = Environment.OSVersion.VersionString;
        var schema = Migrations.CurrentVersion;
        var snippetCount = Snippets.Count;
        var groupCount = Math.Max(0, Groups.Count - 1);
        var lastBackup = _settings.LastBackupAt == 0
            ? "(none)"
            : DateTimeOffset.FromUnixTimeSeconds(_settings.LastBackupAt).ToLocalTime().ToString("yyyy-MM-dd HH:mm");

        sb.AppendLine("```");
        sb.AppendLine($"TaskCopy diagnostics — {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}");
        sb.AppendLine($"version    : {version}");
        sb.AppendLine($"schema     : {schema}");
        sb.AppendLine($"os         : {os}");
        sb.AppendLine($"snippets   : {snippetCount}");
        sb.AppendLine($"groups     : {groupCount}");
        sb.AppendLine($"hotkey     : {HotkeyDisplay} ({(HotkeyIsRegistered ? "active" : "not registered")})");
        sb.AppendLine($"lastBackup : {lastBackup}");
        sb.AppendLine($"theme      : {_settings.Theme}");
        sb.AppendLine($"autoPaste  : {_settings.AutoPaste}");
        sb.AppendLine($"recentClips: {_settings.RecentClipsEnabled}");
        sb.AppendLine("```");

        // Tail crash.log if present.
        try
        {
            if (System.IO.File.Exists(CrashLog.LogPath))
            {
                var allLines = System.IO.File.ReadAllLines(CrashLog.LogPath);
                var tail = allLines.Length > 200 ? allLines[^200..] : allLines;
                sb.AppendLine();
                sb.AppendLine("<details><summary>crash.log (last 200 lines)</summary>");
                sb.AppendLine();
                sb.AppendLine("```");
                foreach (var line in tail) sb.AppendLine(line);
                sb.AppendLine("```");
                sb.AppendLine();
                sb.AppendLine("</details>");
            }
        }
        catch { /* best-effort */ }

        return sb.ToString();
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

    partial void OnRecentClipsEnabledChanged(bool value)
    {
        // Persisted + watcher-toggled by App via SetRecentClipsEnabled;
        // SetRecentClipsEnabled is wired through the optional callback.
        ToggleRecentClipsRequested?.Invoke(this, value);
        StatusMessage = value
            ? "Recent clipboard auto-capture is ON. Items flagged 'do not include' are still excluded."
            : "Recent clipboard auto-capture is OFF.";
    }

    public event EventHandler<bool>? ToggleRecentClipsRequested;

    [RelayCommand]
    private void ClearRecentClips()
    {
        try
        {
            _db.ClearRecentClips();
            StatusMessage = "Recent clipboard items cleared.";
        }
        catch (Exception ex)
        {
            CrashLog.Write("ClearRecentClips", ex);
            StatusMessage = $"Clear failed: {ex.Message}";
        }
    }
}
