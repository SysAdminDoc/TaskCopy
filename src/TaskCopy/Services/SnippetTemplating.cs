using System.Text;
using System.Text.RegularExpressions;

namespace TaskCopy.Services;

/// <summary>
/// Pure function that expands snippet placeholders like {{date}}, {{time}},
/// {{clipboard}}, {{cursor}}, {{ask:Field}}. Unknown tokens are left literal.
/// Single pass — no recursion (a templated value can't itself contain tokens).
/// </summary>
public static class SnippetTemplating
{
    private static readonly Regex TokenRegex = new(@"\{\{([^{}]+)\}\}", RegexOptions.Compiled);

    public static ExpansionResult Expand(string body, TemplatingContext ctx)
    {
        if (string.IsNullOrEmpty(body)) return new ExpansionResult { Body = body };

        var sb = new StringBuilder(body.Length);
        var lastIndex = 0;
        int? cursorAbsPosInExpanded = null;

        foreach (Match m in TokenRegex.Matches(body))
        {
            sb.Append(body, lastIndex, m.Index - lastIndex);
            var raw = m.Groups[1].Value.Trim();

            // F28: pipe-chained transforms — "clipboard|trim|upper" splits into
            // the producer token "clipboard" + transform list ["trim", "upper"].
            var pipeIdx = raw.IndexOf('|');
            var token = pipeIdx < 0 ? raw : raw[..pipeIdx].TrimEnd();
            var transforms = pipeIdx < 0
                ? Array.Empty<string>()
                : raw[(pipeIdx + 1)..]
                    .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            var lower = token.ToLowerInvariant();

            if (lower == "cursor")
            {
                cursorAbsPosInExpanded ??= sb.Length;
                lastIndex = m.Index + m.Length;
                continue;
            }

            if (TryExpandToken(token, ctx, out var replacement, out var cancelled))
            {
                if (cancelled) return new ExpansionResult { Cancelled = true };
                replacement = ApplyTransforms(replacement ?? string.Empty, transforms);
                sb.Append(replacement);
            }
            else
            {
                // Unknown token — preserve verbatim.
                sb.Append(m.Value);
            }
            lastIndex = m.Index + m.Length;
        }
        sb.Append(body, lastIndex, body.Length - lastIndex);

        var expanded = sb.ToString();
        int? cursorOffsetFromEnd = cursorAbsPosInExpanded is int pos ? expanded.Length - pos : null;
        return new ExpansionResult { Body = expanded, CursorOffsetFromEnd = cursorOffsetFromEnd };
    }

    private static string ApplyTransforms(string value, string[] transforms)
    {
        foreach (var t in transforms)
        {
            value = SnippetTransforms.Apply(t, value);
        }
        return value;
    }

    private static bool TryExpandToken(string token, TemplatingContext ctx, out string? replacement, out bool cancelled)
    {
        cancelled = false;
        replacement = null;

        var lower = token.ToLowerInvariant();

        if (lower == "date") { replacement = ctx.Now.ToString("yyyy-MM-dd"); return true; }
        if (lower.StartsWith("date:", StringComparison.Ordinal))
        {
            try { replacement = ctx.Now.ToString(token[5..]); return true; }
            catch { replacement = $"{{{{{token}}}}}"; return true; }
        }

        if (lower == "time") { replacement = ctx.Now.ToString("HH:mm:ss"); return true; }
        if (lower.StartsWith("time:", StringComparison.Ordinal))
        {
            try { replacement = ctx.Now.ToString(token[5..]); return true; }
            catch { replacement = $"{{{{{token}}}}}"; return true; }
        }

        if (lower == "clipboard") { replacement = ctx.PreviousClipboard ?? string.Empty; return true; }

        if (lower.StartsWith("ask:", StringComparison.Ordinal))
        {
            var field = token[4..];
            var value = ctx.PromptFor?.Invoke(field);
            if (value is null) { cancelled = true; return true; }
            replacement = value;
            return true;
        }

        return false;
    }
}

public sealed class TemplatingContext
{
    public string PreviousClipboard { get; init; } = string.Empty;
    public Func<string, string?>? PromptFor { get; init; }
    public DateTime Now { get; init; } = DateTime.Now;
}

public sealed class ExpansionResult
{
    public string Body { get; init; } = string.Empty;
    public int? CursorOffsetFromEnd { get; init; }
    public bool Cancelled { get; init; }
}
