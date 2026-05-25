namespace TaskCopy.Models;

/// <summary>
/// F46: one row from snippet_body_history. Carries the saved body + the
/// Unix-seconds timestamp; the view layer renders the timestamp + a preview.
/// </summary>
public sealed class BodyHistoryEntry
{
    public long Id { get; init; }
    public string Body { get; init; } = string.Empty;
    public long SavedAt { get; init; }

    /// <summary>First-line preview, capped at 80 chars (matches Snippet.Preview).</summary>
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

    /// <summary>Human-friendly "yyyy-MM-dd HH:mm" rendering of SavedAt in local time.</summary>
    public string SavedAtDisplay =>
        DateTimeOffset.FromUnixTimeSeconds(SavedAt).ToLocalTime().ToString("yyyy-MM-dd HH:mm");
}
