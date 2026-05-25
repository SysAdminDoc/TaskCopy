using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskCopy.Data;
using TaskCopy.Models;

namespace TaskCopy.ViewModels;

/// <summary>
/// Trash bin (F23). Lists soft-deleted snippets with their deleted_at
/// timestamps, lets the user Restore them back into the live list,
/// permanently delete one, or Empty Trash for the whole table.
/// The 30-day startup purge already runs in App.OnStartup, so the
/// "permanent" path here is a manual override.
/// </summary>
public partial class TrashViewModel : ObservableObject
{
    private readonly SnippetDatabase _db;

    public ObservableCollection<TrashedRow> Items { get; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RestoreCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeletePermanentlyCommand))]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private TrashedRow? _selected;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public bool HasSelection => Selected is not null;

    public TrashViewModel(SnippetDatabase db)
    {
        _db = db;
        Reload();
    }

    public void Reload()
    {
        Items.Clear();
        foreach (var s in _db.GetTrashed())
        {
            Items.Add(new TrashedRow(s));
        }
        StatusMessage = Items.Count == 0
            ? "Trash is empty."
            : $"{Items.Count} snippet{(Items.Count == 1 ? "" : "s")} in trash.";
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Restore()
    {
        if (Selected is null) return;
        var title = Selected.Snippet.Title;
        try { _db.Restore(Selected.Snippet.Id); }
        catch (Exception ex)
        {
            Services.CrashLog.Write("Trash.Restore", ex);
            StatusMessage = $"Restore failed: {ex.Message}";
            return;
        }
        Reload();
        StatusMessage = $"Restored \"{title}\".";
        RestoredAny?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void DeletePermanently()
    {
        if (Selected is null) return;
        var title = Selected.Snippet.Title;
        var result = System.Windows.MessageBox.Show(
            $"Permanently delete \"{title}\"? This cannot be undone.",
            "TaskCopy — Trash",
            System.Windows.MessageBoxButton.OKCancel,
            System.Windows.MessageBoxImage.Warning,
            System.Windows.MessageBoxResult.Cancel);
        if (result != System.Windows.MessageBoxResult.OK) return;

        try { _db.Delete(Selected.Snippet.Id); }
        catch (Exception ex)
        {
            Services.CrashLog.Write("Trash.DeletePermanently", ex);
            StatusMessage = $"Delete failed: {ex.Message}";
            return;
        }
        Reload();
        StatusMessage = $"Deleted \"{title}\" permanently.";
    }

    [RelayCommand]
    private void EmptyTrash()
    {
        if (Items.Count == 0) return;
        var result = System.Windows.MessageBox.Show(
            $"Permanently delete every snippet in trash ({Items.Count})? This cannot be undone.",
            "TaskCopy — Trash",
            System.Windows.MessageBoxButton.OKCancel,
            System.Windows.MessageBoxImage.Warning,
            System.Windows.MessageBoxResult.Cancel);
        if (result != System.Windows.MessageBoxResult.OK) return;

        try
        {
            // Use the 30-day purge code path with a future cutoff so it eats everything.
            var n = _db.PurgeDeletedOlderThan(long.MaxValue);
            StatusMessage = $"Emptied trash ({n} snippets).";
        }
        catch (Exception ex)
        {
            Services.CrashLog.Write("Trash.EmptyTrash", ex);
            StatusMessage = $"Empty failed: {ex.Message}";
            return;
        }
        Reload();
    }

    public event EventHandler? RestoredAny;
}

/// <summary>
/// Display row for a trashed snippet. Computes a human "deleted N days ago"
/// label + a "purge in N days" hint based on the 30-day rolling purge window.
/// </summary>
public sealed class TrashedRow
{
    public Snippet Snippet { get; }

    public TrashedRow(Snippet snippet)
    {
        Snippet = snippet;
    }

    public string Title => Snippet.Title;
    public string Preview => Snippet.Preview;
    public string Body => Snippet.Body;

    public string DeletedAtLabel
    {
        get
        {
            if (Snippet.DeletedAt is not long ts) return string.Empty;
            var when = DateTimeOffset.FromUnixTimeSeconds(ts).ToLocalTime();
            return when.ToString("yyyy-MM-dd HH:mm");
        }
    }

    public string PurgesInLabel
    {
        get
        {
            if (Snippet.DeletedAt is not long ts) return string.Empty;
            var deleted = DateTimeOffset.FromUnixTimeSeconds(ts);
            var remaining = (deleted.AddDays(30) - DateTimeOffset.UtcNow).TotalDays;
            if (remaining <= 0) return "purges next launch";
            if (remaining < 1) return "purges within a day";
            return $"purges in {(int)remaining} day{((int)remaining == 1 ? "" : "s")}";
        }
    }
}
