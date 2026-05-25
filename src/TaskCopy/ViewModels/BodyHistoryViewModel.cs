using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskCopy.Data;
using TaskCopy.Models;
using TaskCopy.Services;

namespace TaskCopy.ViewModels;

/// <summary>
/// F46: backs the per-snippet body history modal. Loads the 10 newest versions
/// from <c>snippet_body_history</c>, lets the user Restore one back into the
/// live snippet, or permanently drop a history entry.
/// </summary>
public partial class BodyHistoryViewModel : ObservableObject
{
    private readonly SnippetDatabase _db;
    private readonly long _snippetId;
    private readonly string _snippetTitle;

    public ObservableCollection<BodyHistoryEntry> Entries { get; } = new();

    public bool HasEntries => Entries.Count > 0;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RestoreCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteEntryCommand))]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private BodyHistoryEntry? _selected;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public string HeaderText { get; }

    public bool HasSelection => Selected is not null;

    /// <summary>Raised when the user picks Restore; SettingsViewModel persists via EditBody.</summary>
    public event EventHandler<string>? RestoreRequested;

    public BodyHistoryViewModel(SnippetDatabase db, long snippetId, string snippetTitle)
    {
        _db = db;
        _snippetId = snippetId;
        _snippetTitle = snippetTitle;
        HeaderText = $"Edit history — {snippetTitle}";
        Entries.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasEntries));
        Reload();
    }

    private void Reload()
    {
        Entries.Clear();
        foreach (var e in _db.GetBodyHistory(_snippetId)) Entries.Add(e);
        StatusMessage = Entries.Count == 0
            ? "No history yet — versions are recorded on every save."
            : $"{Entries.Count} version{(Entries.Count == 1 ? "" : "s")} (newest first).";
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Restore()
    {
        if (Selected is null) return;
        RestoreRequested?.Invoke(this, Selected.Body);
        StatusMessage = $"Restored version from {Selected.SavedAtDisplay}.";
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void DeleteEntry()
    {
        if (Selected is null) return;
        var stamp = Selected.SavedAtDisplay;
        try { _db.DeleteBodyHistoryEntry(Selected.Id); }
        catch (Exception ex)
        {
            CrashLog.Write("BodyHistory.DeleteEntry", ex);
            StatusMessage = $"Delete failed: {ex.Message}";
            return;
        }
        Reload();
        StatusMessage = $"Removed version from {stamp}.";
    }
}
