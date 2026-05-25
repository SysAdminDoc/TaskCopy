using System.Text;
using System.Text.RegularExpressions;

namespace TaskCopy.Services;

/// <summary>
/// Pure function that expands snippet placeholders like {{date}}, {{time}},
/// {{clipboard}}, {{cursor}}, {{ask:Field}}, and {{form:Field1|Field2}}.
/// Unknown tokens are left literal. Single pass — no recursion (a templated
/// value can't itself contain tokens).
/// </summary>
public static class SnippetTemplating
{
    private static readonly Regex TokenRegex = new(@"\{\{([^{}]+)\}\}", RegexOptions.Compiled);

    public static ExpansionResult Expand(string body, TemplatingContext ctx)
    {
        if (string.IsNullOrEmpty(body)) return new ExpansionResult { Body = body };

        var promptedValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var formFields = ExtractFormFields(body);
        if (formFields.Count > 0)
        {
            IReadOnlyDictionary<string, string>? values;
            if (ctx.PromptForMany is not null)
            {
                values = ctx.PromptForMany(formFields);
            }
            else
            {
                values = PromptIndividually(formFields, ctx);
            }

            if (values is null) return new ExpansionResult { Cancelled = true };
            foreach (var field in formFields)
            {
                promptedValues[field] = values.TryGetValue(field, out var value) ? value : string.Empty;
            }
        }

        var sb = new StringBuilder(body.Length);
        var lastIndex = 0;
        int? cursorAbsPosInExpanded = null;

        foreach (Match m in TokenRegex.Matches(body))
        {
            sb.Append(body, lastIndex, m.Index - lastIndex);
            var raw = m.Groups[1].Value.Trim();

            // F28: pipe-chained transforms — "clipboard|trim|upper" splits into
            // the producer token "clipboard" + transform list ["trim", "upper"].
            // F36: `form:` uses `|` as the field separator, so it does not
            // participate in pipe transforms.
            var rawIsForm = raw.StartsWith("form:", StringComparison.OrdinalIgnoreCase);
            var pipeIdx = rawIsForm ? -1 : raw.IndexOf('|');
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

            if (TryExpandToken(token, ctx, promptedValues, out var replacement, out var cancelled))
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

    private static List<string> ExtractFormFields(string body)
    {
        var fields = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match m in TokenRegex.Matches(body))
        {
            var raw = m.Groups[1].Value.Trim();
            if (!raw.StartsWith("form:", StringComparison.OrdinalIgnoreCase)) continue;

            foreach (var field in ParseFormFields(raw))
            {
                if (seen.Add(field)) fields.Add(field);
            }
        }

        return fields;
    }

    private static IEnumerable<string> ParseFormFields(string token)
    {
        if (!token.StartsWith("form:", StringComparison.OrdinalIgnoreCase)) yield break;
        foreach (var field in token[5..].Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!string.IsNullOrWhiteSpace(field)) yield return field;
        }
    }

    private static IReadOnlyDictionary<string, string>? PromptIndividually(
        IReadOnlyList<string> fields,
        TemplatingContext ctx)
    {
        if (ctx.PromptFor is null) return null;

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in fields)
        {
            var value = ctx.PromptFor(field);
            if (value is null) return null;
            values[field] = value;
        }
        return values;
    }

    private static string ApplyTransforms(string value, string[] transforms)
    {
        foreach (var t in transforms)
        {
            value = SnippetTransforms.Apply(t, value);
        }
        return value;
    }

    private static bool TryExpandToken(
        string token,
        TemplatingContext ctx,
        IDictionary<string, string> promptedValues,
        out string? replacement,
        out bool cancelled)
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

        if (lower.StartsWith("form:", StringComparison.Ordinal))
        {
            replacement = string.Empty;
            return true;
        }

        if (lower.StartsWith("ask:", StringComparison.Ordinal))
        {
            var field = token[4..].Trim();
            if (promptedValues.TryGetValue(field, out var cached))
            {
                replacement = cached;
                return true;
            }

            var value = ctx.PromptFor?.Invoke(field);
            if (value is null) { cancelled = true; return true; }
            promptedValues[field] = value;
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
    public Func<IReadOnlyList<string>, IReadOnlyDictionary<string, string>?>? PromptForMany { get; init; }
    public DateTime Now { get; init; } = DateTime.Now;
}

public sealed class ExpansionResult
{
    public string Body { get; init; } = string.Empty;
    public int? CursorOffsetFromEnd { get; init; }
    public bool Cancelled { get; init; }
}
