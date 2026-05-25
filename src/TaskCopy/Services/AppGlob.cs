using System.Text.RegularExpressions;

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
    /// <summary>True when <paramref name="targetProcess"/> matches any pattern in the comma-separated list.</summary>
    public static bool Matches(string? globList, string? targetProcess)
    {
        if (string.IsNullOrWhiteSpace(globList)) return true; // unset = universal
        if (string.IsNullOrEmpty(targetProcess)) return false;

        var patterns = globList.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var p in patterns)
        {
            if (GlobToRegex(p).IsMatch(targetProcess)) return true;
        }
        return false;
    }

    private static Regex GlobToRegex(string glob)
    {
        // Anchored full-match; only `*` is wildcard. Everything else is escaped
        // so e.g. "code-insiders.exe" sees literal . and -.
        var sb = new System.Text.StringBuilder();
        sb.Append('^');
        foreach (var c in glob)
        {
            sb.Append(c == '*' ? ".*" : Regex.Escape(c.ToString()));
        }
        sb.Append('$');
        return new Regex(sb.ToString(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}
