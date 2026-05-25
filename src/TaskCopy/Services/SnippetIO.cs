using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using TaskCopy.Data;

namespace TaskCopy.Services;

/// <summary>
/// JSON export/import for snippets + groups. Format is versioned;
/// SkipDuplicates is the default merge strategy (title-based, case-insensitive).
/// Used counts / last-used timestamps are intentionally omitted — those are
/// per-machine usage stats, not portable content.
/// </summary>
public static class SnippetIO
{
    private const int FormatVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public sealed record ExportSnippet(
        string Title,
        string Body,
        int SortOrder,
        long CreatedAt,
        string? QuickHotkey,
        bool Pinned,
        bool IsMonospace,
        string? GroupName);

    public sealed record ExportGroup(string Name, int SortOrder);

    public sealed record ExportPayload(
        int Version,
        string ExportedAt,
        IReadOnlyList<ExportGroup> Groups,
        IReadOnlyList<ExportSnippet> Snippets);

    public sealed record ImportResult(int Added, int Skipped, int GroupsCreated);

    public static int Export(SnippetDatabase db, string path)
    {
        var groups = db.GetGroups();
        var nameById = groups.ToDictionary(g => g.Id, g => g.Name);
        var snippets = db.GetAll();

        var payload = new ExportPayload(
            Version: FormatVersion,
            ExportedAt: DateTimeOffset.UtcNow.ToString("o"),
            Groups: groups.Select(g => new ExportGroup(g.Name, g.SortOrder)).ToList(),
            Snippets: snippets.Select(s => new ExportSnippet(
                Title: s.Title,
                Body: s.Body,
                SortOrder: s.SortOrder,
                CreatedAt: s.CreatedAt,
                QuickHotkey: s.QuickHotkey,
                Pinned: s.Pinned,
                IsMonospace: s.IsMonospace,
                GroupName: s.GroupId is long gid && nameById.TryGetValue(gid, out var gn) ? gn : null
            )).ToList());

        var json = JsonSerializer.Serialize(payload, JsonOptions);
        File.WriteAllText(path, json);
        return snippets.Count;
    }

    public static ImportResult Import(SnippetDatabase db, string path)
    {
        var json = File.ReadAllText(path);
        var payload = JsonSerializer.Deserialize<ExportPayload>(json, JsonOptions)
                      ?? throw new InvalidDataException("Empty or unreadable export file.");

        if (payload.Version != FormatVersion)
        {
            throw new InvalidDataException(
                $"Unsupported export version {payload.Version} (this build expects {FormatVersion}).");
        }

        var existingGroups = db.GetGroups()
            .ToDictionary(g => g.Name, g => g.Id, StringComparer.OrdinalIgnoreCase);
        var groupsCreated = 0;
        foreach (var g in payload.Groups ?? Array.Empty<ExportGroup>())
        {
            if (string.IsNullOrWhiteSpace(g.Name)) continue;
            if (existingGroups.ContainsKey(g.Name)) continue;
            var id = db.InsertGroup(g.Name);
            existingGroups[g.Name] = id;
            groupsCreated++;
        }

        var existingTitles = new HashSet<string>(
            db.GetAll().Select(s => s.Title),
            StringComparer.OrdinalIgnoreCase);

        int added = 0, skipped = 0;
        foreach (var s in payload.Snippets ?? Array.Empty<ExportSnippet>())
        {
            if (string.IsNullOrEmpty(s.Title))
            {
                skipped++;
                continue;
            }
            if (existingTitles.Contains(s.Title))
            {
                skipped++;
                continue;
            }

            long? groupId = !string.IsNullOrEmpty(s.GroupName)
                            && existingGroups.TryGetValue(s.GroupName, out var gid)
                ? gid
                : null;

            var id = db.Insert(s.Title, s.Body, groupId);
            if (s.IsMonospace) db.SetMonospace(id, true);
            if (s.Pinned) db.SetPinned(id, true);
            if (!string.IsNullOrEmpty(s.QuickHotkey)) db.SetQuickHotkey(id, s.QuickHotkey);
            existingTitles.Add(s.Title);
            added++;
        }

        return new ImportResult(added, skipped, groupsCreated);
    }
}
