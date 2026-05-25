namespace TaskCopy.Services;

/// <summary>
/// F35: tiny glob matcher for the per-snippet target_app_glob column.
/// Accepts a comma-separated list of patterns; each pattern supports `*` as a
/// "zero-or-more-chars" wildcard. Matching is case-insensitive against the
/// foreground process name shape (e.g. "outlook.exe", "code-insiders.exe").
///
/// Examples:
///   "outlook.exe"          → matches exactly outlook.exe
///   "*code*.exe"           → matches code.exe / vscode.exe / code-insiders.exe
///   "outlook.exe,Teams.exe" → matches either
///   "" or null              → no filter (universal — caller should treat as "match always")
/// </summary>
public static class AppGlob
{
    private const int MaxPatterns = 32;
    private const int MaxPatternChars = 256;

    /// <summary>True when <paramref name="targetProcess"/> matches any pattern in the comma-separated list.</summary>
    public static bool Matches(string? globList, string? targetProcess)
    {
        if (string.IsNullOrWhiteSpace(globList)) return true; // unset = universal
        if (string.IsNullOrEmpty(targetProcess)) return false;

        var patterns = globList
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Take(MaxPatterns);
        foreach (var p in patterns)
        {
            if (p.Length is 0 or > MaxPatternChars) continue;
            if (WildcardMatch(p, targetProcess)) return true;
        }
        return false;
    }

    private static bool WildcardMatch(string pattern, string text)
    {
        var p = 0;
        var t = 0;
        var star = -1;
        var match = 0;

        while (t < text.Length)
        {
            if (p < pattern.Length && pattern[p] == '*')
            {
                star = p++;
                match = t;
                continue;
            }

            if (p < pattern.Length && CharsEqual(pattern[p], text[t]))
            {
                p++;
                t++;
                continue;
            }

            if (star >= 0)
            {
                p = star + 1;
                t = ++match;
                continue;
            }

            return false;
        }

        while (p < pattern.Length && pattern[p] == '*') p++;
        return p == pattern.Length;
    }

    private static bool CharsEqual(char left, char right) =>
        char.ToUpperInvariant(left) == char.ToUpperInvariant(right);
}
