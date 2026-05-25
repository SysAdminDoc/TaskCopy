using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskCopy.Data;
using TaskCopy.Models;
using TaskCopy.Services;

namespace TaskCopy.ViewModels;

public partial class SnippetMenuViewModel : ObservableObject
{
    private readonly SnippetDatabase _db;
    private List<Snippet> _all = new();

    public ObservableCollection<SnippetRow> Snippets { get; } = new();

    [ObservableProperty]
    private string _statusText = "TaskCopy";

    [ObservableProperty]
    private bool _hasSnippets;

    [ObservableProperty]
    private bool _hasAnySnippets;

    [ObservableProperty]
    private string _filter = string.Empty;

    [ObservableProperty]
    private int _selectedIndex = -1;

    /// <summary>
    /// Raised when the user picks a snippet. The orchestrator (App) expands
    /// placeholders, writes the clipboard, records use, closes the flyout,
    /// and triggers auto-paste.
    /// </summary>
    public event EventHandler<Snippet>? SnippetCopyRequested;
    public event EventHandler? EditRequested;
    public event EventHandler? AboutRequested;
    public event EventHandler? QuitRequested;

    public SnippetMenuViewModel(SnippetDatabase db)
    {
        _db = db;
    }

    public void Refresh()
    {
        _all = _db.GetAll();
        HasAnySnippets = _all.Count > 0;
        ApplyFilter();
    }

    partial void OnFilterChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        Snippets.Clear();
        var q = (Filter ?? string.Empty).Trim();
        var displayIndex = 1;
        foreach (var s in _all)
        {
            if (string.IsNullOrEmpty(q) || Matches(s, q))
            {
                Snippets.Add(new SnippetRow(s, displayIndex));
                displayIndex++;
            }
        }
        HasSnippets = Snippets.Count > 0;
        SelectedIndex = HasSnippets ? 0 : -1;
        UpdateStatus(q);
    }

    private void UpdateStatus(string filter)
    {
        if (!HasAnySnippets)
        {
            StatusText = "TaskCopy · no snippets yet";
            return;
        }
        if (!HasSnippets)
        {
            StatusText = $"TaskCopy · no matches for \"{filter}\"";
            return;
        }
        if (!string.IsNullOrEmpty(filter))
        {
            StatusText = $"TaskCopy · {Snippets.Count} match{(Snippets.Count == 1 ? "" : "es")}";
            return;
        }
        StatusText = $"TaskCopy · {Snippets.Count} snippet{(Snippets.Count == 1 ? "" : "s")}";
    }

    private static bool Matches(Snippet s, string q)
    {
        return (s.Title is { } t && t.Contains(q, StringComparison.OrdinalIgnoreCase))
            || (s.Body is { } b && b.Contains(q, StringComparison.OrdinalIgnoreCase));
    }

    public void MoveSelection(int delta)
    {
        if (!HasSnippets) return;
        var next = SelectedIndex + delta;
        if (next < 0) next = 0;
        if (next >= Snippets.Count) next = Snippets.Count - 1;
        SelectedIndex = next;
    }

    public void CopySelected()
    {
        if (!HasSnippets) return;
        if (SelectedIndex < 0 || SelectedIndex >= Snippets.Count) return;
        Copy(Snippets[SelectedIndex].Snippet);
    }

    public void CopyAtVisibleIndex(int oneBased)
    {
        var idx = oneBased - 1;
        if (idx < 0 || idx >= Snippets.Count) return;
        Copy(Snippets[idx].Snippet);
    }

    public bool ClearFilterIfAny()
    {
        if (string.IsNullOrEmpty(Filter)) return false;
        Filter = string.Empty;
        return true;
    }

    [RelayCommand]
    private void Copy(Snippet? snippet)
    {
        if (snippet is null) return;
        SnippetCopyRequested?.Invoke(this, snippet);
    }

    [RelayCommand]
    private void Edit() => EditRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void About() => AboutRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void Quit() => QuitRequested?.Invoke(this, EventArgs.Empty);
}
