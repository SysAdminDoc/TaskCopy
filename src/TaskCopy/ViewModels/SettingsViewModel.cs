using System.Collections.ObjectModel;
using System.Windows.Input;
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

    public ObservableCollection<Snippet> Snippets { get; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeleteSnippetCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveUpCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveDownCommand))]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    [NotifyPropertyChangedFor(nameof(EditTitle))]
    [NotifyPropertyChangedFor(nameof(EditBody))]
    private Snippet? _selectedSnippet;

    public bool HasSelection => SelectedSnippet is not null;

    public string EditTitle
    {
        get => SelectedSnippet?.Title ?? string.Empty;
        set
        {
            if (SelectedSnippet is null) return;
            if (SelectedSnippet.Title == value) return;
            SelectedSnippet.Title = value;
            _db.Update(SelectedSnippet.Id, SelectedSnippet.Title, SelectedSnippet.Body);
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
            _db.Update(SelectedSnippet.Id, SelectedSnippet.Title, SelectedSnippet.Body);
            OnPropertyChanged();
            DirtyChanged?.Invoke(this, EventArgs.Empty);
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

        HotkeyKey = _settings.HotkeyKey;
        HotkeyModifiers = _settings.HotkeyModifiers;
        HotkeyDisplay = HotkeyService.FormatHotkey(HotkeyKey, HotkeyModifiers);
        StartWithWindows = _startup.IsEnabled;
        AutoPaste = _settings.AutoPaste;
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

    public void SetHotkey(Key key, ModifierKeys modifiers)
    {
        HotkeyKey = key;
        HotkeyModifiers = modifiers;
        _settings.HotkeyKey = key;
        _settings.HotkeyModifiers = modifiers;
        HotkeyDisplay = HotkeyService.FormatHotkey(key, modifiers);

        if (_hotkeys.TryRegister(key, modifiers))
        {
            StatusMessage = $"Hotkey set to {HotkeyDisplay}.";
        }
        else
        {
            StatusMessage = $"Hotkey {HotkeyDisplay} could not be registered — try another combo.";
        }
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
