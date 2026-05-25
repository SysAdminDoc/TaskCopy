using System.IO;
using TaskCopy.Data;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace TaskCopy.Services;

/// <summary>
/// F38: import [Espanso](https://espanso.org) match YAML files into TaskCopy
/// snippets. Espanso's `matches:` array is the canonical "snippet pack"
/// format in that ecosystem; this lets users bring an existing library over
/// without rewriting it by hand.
///
/// Mapping from Espanso to TaskCopy:
///   trigger (or first triggers[]) -> snippet title
///   label                          -> snippet title (overrides trigger when set)
///   replace                        -> snippet body
///
/// Espanso variables, regex matchers, image/html/markdown replacements, and form prompts
/// are NOT translated - they're Espanso-specific behaviors. We import only
/// the static-replace items and skip the rest with a counted reason.
/// </summary>
public static class EspansoImport
{
    public sealed record ImportResult(int Added, int Skipped, int GroupsCreated);

    /// <summary>
    /// Parse <paramref name="yamlPath"/> as an Espanso match file and write each
    /// static-replace match as a snippet. Optional <paramref name="groupName"/>
    /// scopes the import into a group (created if missing); pass null to import
    /// into the Ungrouped bucket.
    /// </summary>
    public static ImportResult Import(SnippetDatabase db, string yamlPath, string? groupName = null)
    {
        var text = File.ReadAllText(yamlPath);
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        EspansoFile root;
        try
        {
            root = deserializer.Deserialize<EspansoFile>(text) ?? new EspansoFile();
        }
        catch (Exception ex)
        {
            throw new InvalidDataException($"YAML parse failed: {ex.Message}", ex);
        }

        int groupsCreated = 0;
        long? groupId = null;

        long? EnsureGroup()
        {
            if (string.IsNullOrWhiteSpace(groupName)) return null;
            if (groupId is long id) return id;

            var normalized = groupName.Trim();
            var hit = db.GetGroups()
                .FirstOrDefault(g => string.Equals(g.Name, normalized, StringComparison.OrdinalIgnoreCase));
            if (hit is not null)
            {
                groupId = hit.Id;
                return groupId;
            }

            groupId = db.InsertGroup(normalized);
            groupsCreated = 1;
            return groupId;
        }

        var existingTitles = new HashSet<string>(
            db.GetAll().Select(s => s.Title),
            StringComparer.OrdinalIgnoreCase);

        int added = 0, skipped = 0;
        foreach (var m in root.Matches ?? new List<EspansoMatch>())
        {
            // Skip everything that isn't a plain static replace. Espanso's
            // image / form / regex / variable behaviors don't map to TaskCopy.
            var replace = m.Replace;
            if (string.IsNullOrEmpty(replace)) { skipped++; continue; }
            if (m.HasUnsupportedBehavior()) { skipped++; continue; }

            // Title preference: explicit `label`, else first `triggers[]`, else `trigger`.
            var title = FirstNonWhiteSpace(m.Label, m.Triggers?.FirstOrDefault(), m.Trigger);
            if (string.IsNullOrEmpty(title)) { skipped++; continue; }
            if (existingTitles.Contains(title)) { skipped++; continue; }

            db.Insert(title, replace, EnsureGroup());
            existingTitles.Add(title);
            added++;
        }

        return new ImportResult(added, skipped, groupsCreated);
    }

    private static string FirstNonWhiteSpace(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value)) return value.Trim();
        }
        return string.Empty;
    }

    /// <summary>YAML root: typically `{ matches: [ ... ] }`.</summary>
    private sealed class EspansoFile
    {
        public List<EspansoMatch>? Matches { get; set; }
    }

    /// <summary>
    /// One Espanso match. Most fields are optional - we only need trigger/label
    /// + replace; the rest exist for skip-detection so we don't try to import
    /// things that wouldn't behave the same in TaskCopy.
    /// </summary>
    private sealed class EspansoMatch
    {
        public string? Trigger { get; set; }
        public List<string>? Triggers { get; set; }
        public string? Label { get; set; }
        public string? Replace { get; set; }
        public string? Regex { get; set; }

        [YamlMember(Alias = "image_path")]
        public string? ImagePath { get; set; }

        public string? Html { get; set; }
        public string? Markdown { get; set; }
        public object? Form { get; set; }

        [YamlMember(Alias = "form_fields")]
        public object? FormFields { get; set; }

        public object? Vars { get; set; }

        public bool HasUnsupportedBehavior()
            => Regex is not null
               || ImagePath is not null
               || Html is not null
               || Markdown is not null
               || Form is not null
               || FormFields is not null
               || Vars is not null;
    }
}
