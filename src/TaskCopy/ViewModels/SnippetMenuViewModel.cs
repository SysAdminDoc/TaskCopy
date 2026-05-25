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
    private readonly ClipboardService _clipboard;

    public ObservableCollection<Snippet> Snippets { get; } = new();

    [ObservableProperty]
    private string _statusText = "TaskCopy";

    [ObservableProperty]
    private bool _hasSnippets;

    public event EventHandler? SnippetCopied;
    public event EventHandler? EditRequested;
    public event EventHandler? AboutRequested;
    public event EventHandler? QuitRequested;

    public SnippetMenuViewModel(SnippetDatabase db, ClipboardService clipboard)
    {
        _db = db;
        _clipboard = clipboard;
    }

    public void Refresh()
    {
        Snippets.Clear();
        foreach (var snippet in _db.GetAll())
        {
            Snippets.Add(snippet);
        }
        HasSnippets = Snippets.Count > 0;
        StatusText = HasSnippets
            ? $"TaskCopy · {Snippets.Count} snippet{(Snippets.Count == 1 ? "" : "s")}"
            : "TaskCopy · no snippets yet";
    }

    [RelayCommand]
    private void Copy(Snippet? snippet)
    {
        if (snippet is null) return;
        if (_clipboard.TryCopy(snippet.Body))
        {
            SnippetCopied?.Invoke(this, EventArgs.Empty);
        }
    }

    [RelayCommand]
    private void Edit() => EditRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void About() => AboutRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void Quit() => QuitRequested?.Invoke(this, EventArgs.Empty);
}
