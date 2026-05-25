using TaskCopy.Models;

namespace TaskCopy.Services;

/// <summary>
/// Lightweight ranking for the flyout type-ahead (F27). Substring matches with
/// a few score boosts — title prefix > title contains > body contains > group
/// contains. Score 0 = no match; the VM filters those out and sorts by score
/// desc, then by the existing flyout sort order.
///
/// Pure-managed string ops; FTS5 would be a future upgrade if libraries cross
/// 1000+ snippets and the in-memory scan becomes the dominant cost (it isn't
/// today — typical libraries are well under 100 entries).
/// </summary>
public static class SnippetMatch
{
    public static int Score(Snippet snippet, string query)
    {
        if (string.IsNullOrEmpty(query)) return 1; // no filter => everything matches

        // Fielded operators: title:foo / body:foo / g:groupname
        if (query.Contains(':'))
        {
            var colonIdx = query.IndexOf(':');
            var field = query[..colonIdx].Trim().ToLowerInvariant();
            var needle = query[(colonIdx + 1)..].Trim();
            if (needle.Length == 0) return 0;
            return field switch
            {
                "title" => snippet.Title.Contains(needle, StringComparison.OrdinalIgnoreCase) ? 50 : 0,
                "body"  => snippet.Body.Contains(needle, StringComparison.OrdinalIgnoreCase) ? 20 : 0,
                "g" or "group" =>
                    // Caller (VM) doesn't have group names on Snippet; treat g:* as a
                    // fall-through — the VM can apply the chip filter for proper group
                    // matching. Score zero so this operator is a no-op at row level.
                    0,
                _ => ScoreUnfielded(snippet, query),
            };
        }

        return ScoreUnfielded(snippet, query);
    }

    private static int ScoreUnfielded(Snippet snippet, string query)
    {
        var t = snippet.Title;
        var b = snippet.Body;

        // Title prefix (fastest target user wants)
        if (t.StartsWith(query, StringComparison.OrdinalIgnoreCase)) return 100;
        // Title contains
        if (t.Contains(query, StringComparison.OrdinalIgnoreCase)) return 60;
        // Body contains
        if (b.Contains(query, StringComparison.OrdinalIgnoreCase)) return 20;

        return 0;
    }
}
