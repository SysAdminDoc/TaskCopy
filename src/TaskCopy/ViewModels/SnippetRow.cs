using TaskCopy.Models;

namespace TaskCopy.ViewModels;

/// <summary>
/// Lightweight wrapper that carries the visible 1-based row number for the
/// snippet flyout so 1..9 number-key quick-pick can show "1", "2", ... next to
/// the first nine rows. The Settings list still binds to Snippet directly.
/// </summary>
public sealed class SnippetRow
{
    public Snippet Snippet { get; }
    public int DisplayIndex { get; }
    public string IndexLabel => DisplayIndex >= 1 && DisplayIndex <= 9 ? DisplayIndex.ToString() : string.Empty;

    public string Title => Snippet.Title;
    public string Preview => Snippet.Preview;
    public string Body => Snippet.Body;
    public bool Pinned => Snippet.Pinned;

    public SnippetRow(Snippet snippet, int displayIndex)
    {
        Snippet = snippet;
        DisplayIndex = displayIndex;
    }
}
