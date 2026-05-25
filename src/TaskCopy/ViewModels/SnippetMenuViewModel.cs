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
    private readonly SettingsStore _settings;
    private List<Snippet> _all = new();
    private List<RecentClip> _allRecent = new();

    public ObservableCollection<SnippetRow> Snippets { get; } = new();
    public ObservableCollection<RecentClipRow> RecentClips { get; } = new();
    public ObservableCollection<GroupChip> GroupChips { get; } = new();

    [ObservableProperty]
    private string _statusText = "TaskCopy";

    [ObservableProperty]
    private bool _hasSnippets;

    [ObservableProperty]
    private bool _hasAnySnippets;

    [ObservableProperty]
    private bool _hasRecentClips;

    [ObservableProperty]
    private bool _hasGroups;

    /// <summary>Selected group filter: 0 = All, -1 = Ungrouped, positive = group id.</summary>
    [ObservableProperty]
    private long _selectedGroupId;

    [ObservableProperty]
    private string _filter = string.Empty;

    [ObservableProperty]
    private int _selectedIndex = -1;

    /// <summary>
    /// Raised when the user picks a snippet. The orchestrator (App) expands
    /// placeholders, writes the clipboard, records use, closes the flyout,
    /// and triggers auto-paste.
    /// </summary>
    public event EventHandler<Snippet>? SnippetCopyRequested;
    /// <summary>Raised when the user picks a recent-clipboard row (F19).</summary>
    public event EventHandler<RecentClip>? RecentClipCopyRequested;
    /// <summary>Raised when the user promotes a recent clip to a real snippet (F19).</summary>
    public event EventHandler<RecentClip>? PromoteRecentClipRequested;
    public event EventHandler? EditRequested;
    public event EventHandler? AboutRequested;
    public event EventHandler? QuitRequested;

    public SnippetMenuViewModel(SnippetDatabase db, SettingsStore settings)
    {
        _db = db;
        _settings = settings;
    }

    public void Refresh()
    {
        _all = SortForFlyout(_db.GetAll(), _settings.FlyoutSortMode);
        HasAnySnippets = _all.Count > 0;
        // F19: load the most-recent N clipboard items when capture is on, so
        // the flyout can show a "Recent" section above curated snippets.
        if (_settings.RecentClipsEnabled)
        {
            try { _allRecent = _db.GetRecentClips(10); }
            catch { _allRecent = new List<RecentClip>(); }
        }
        else
        {
            _allRecent = new List<RecentClip>();
        }
        // F20: group pivot chips. Always offer "All" + per-group rows + an
        // "Ungrouped" chip when at least one snippet is ungrouped.
        RebuildGroupChips();
        // Restore last selected group if it still exists (else fall back to All).
        var saved = _settings.FlyoutLastGroupId;
        if (saved != 0 && saved != -1 && _db.GetGroups().All(g => g.Id != saved))
        {
            saved = 0;
        }
        SelectedGroupId = saved;
        ApplyFilter();
    }

    private void RebuildGroupChips()
    {
        GroupChips.Clear();
        var groups = _db.GetGroups();
        if (groups.Count == 0)
        {
            HasGroups = false;
            return;
        }

        HasGroups = true;
        var allCount = _all.Count;
        GroupChips.Add(new GroupChip(0, "All", allCount));
        foreach (var g in groups)
        {
            var count = _all.Count(s => s.GroupId == g.Id);
            GroupChips.Add(new GroupChip(g.Id, g.Name, count));
        }
        var ungroupedCount = _all.Count(s => s.GroupId is null);
        if (ungroupedCount > 0)
        {
            GroupChips.Add(new GroupChip(-1L, "Ungrouped", ungroupedCount));
        }
    }

    public void SelectGroup(long groupId)
    {
        if (SelectedGroupId == groupId) return;
        SelectedGroupId = groupId;
        try { _settings.FlyoutLastGroupId = groupId; } catch { }
        ApplyFilter();
    }

    private static List<Snippet> SortForFlyout(List<Snippet> input, FlyoutSortMode mode)
    {
        // Manual respects the user's drag order exactly (no pin promotion).
        if (mode == FlyoutSortMode.Manual)
        {
            return input.OrderBy(s => s.SortOrder).ThenBy(s => s.Id).ToList();
        }

        // All other modes: pinned first (still ordered by the mode within each band).
        IEnumerable<Snippet> ordered = mode switch
        {
            FlyoutSortMode.MostUsed => input
                .OrderByDescending(s => s.Pinned)
                .ThenByDescending(s => s.UsedCount)
                .ThenByDescending(s => s.LastUsedAt ?? 0L)
                .ThenBy(s => s.SortOrder),
            FlyoutSortMode.RecentlyUsed => input
                .OrderByDescending(s => s.Pinned)
                .ThenByDescending(s => s.LastUsedAt ?? 0L)
                .ThenByDescending(s => s.UsedCount)
                .ThenBy(s => s.SortOrder),
            _ => input.OrderBy(s => s.SortOrder),
        };
        return ordered.ToList();
    }

    partial void OnFilterChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        Snippets.Clear();
        RecentClips.Clear();
        var q = (Filter ?? string.Empty).Trim();
        var groupId = SelectedGroupId;

        // F27: score + rank candidates so title-prefix matches rise above body matches.
        // For empty query the score is 1 across the board and we fall back to the
        // existing flyout order (Manual / MostUsed / RecentlyUsed) by emitting _all as-is.
        IEnumerable<Snippet> candidates;
        if (string.IsNullOrEmpty(q))
        {
            candidates = _all;
        }
        else
        {
            candidates = _all
                .Select(s => (snippet: s, score: SnippetMatch.Score(s, q)))
                .Where(p => p.score > 0)
                // Stable sort: high score first, then existing _all order (which
                // already encodes the user's chosen flyout sort).
                .OrderByDescending(p => p.score)
                .Select(p => p.snippet);
        }

        var displayIndex = 1;
        foreach (var s in candidates)
        {
            // Group filter: 0 = All, -1 = Ungrouped, positive = specific group.
            if (groupId == -1L && s.GroupId is not null) continue;
            if (groupId > 0L && s.GroupId != groupId) continue;

            Snippets.Add(new SnippetRow(s, displayIndex));
            displayIndex++;
        }
        HasSnippets = Snippets.Count > 0;

        // Recent clips — filter is applied to body only.
        foreach (var c in _allRecent)
        {
            if (string.IsNullOrEmpty(q) || c.Body.Contains(q, StringComparison.OrdinalIgnoreCase))
            {
                RecentClips.Add(new RecentClipRow(c));
            }
        }
        HasRecentClips = RecentClips.Count > 0;

        SelectedIndex = HasSnippets ? 0 : -1;
        UpdateStatus(q);
    }

    private void UpdateStatus(string filter)
    {
        if (!HasAnySnippets)
        {
            StatusText = "TaskCopy · no snippets yet";
            return;
        }
        if (!HasSnippets)
        {
            StatusText = $"TaskCopy · no matches for \"{filter}\"";
            return;
        }
        if (!string.IsNullOrEmpty(filter))
        {
            StatusText = $"TaskCopy · {Snippets.Count} match{(Snippets.Count == 1 ? "" : "es")}";
            return;
        }
        StatusText = $"TaskCopy · {Snippets.Count} snippet{(Snippets.Count == 1 ? "" : "s")}";
    }

    private static bool Matches(Snippet s, string q)
    {
        return (s.Title is { } t && t.Contains(q, StringComparison.OrdinalIgnoreCase))
            || (s.Body is { } b && b.Contains(q, StringComparison.OrdinalIgnoreCase));
    }

    public void MoveSelection(int delta)
    {
        if (!HasSnippets) return;
        var next = SelectedIndex + delta;
        if (next < 0) next = 0;
        if (next >= Snippets.Count) next = Snippets.Count - 1;
        SelectedIndex = next;
    }

    public void CopySelected()
    {
        if (!HasSnippets) return;
        if (SelectedIndex < 0 || SelectedIndex >= Snippets.Count) return;
        Copy(Snippets[SelectedIndex].Snippet);
    }

    public void CopyAtVisibleIndex(int oneBased)
    {
        var idx = oneBased - 1;
        if (idx < 0 || idx >= Snippets.Count) return;
        Copy(Snippets[idx].Snippet);
    }

    public bool ClearFilterIfAny()
    {
        if (string.IsNullOrEmpty(Filter)) return false;
        Filter = string.Empty;
        return true;
    }

    [RelayCommand]
    private void Copy(Snippet? snippet)
    {
        if (snippet is null) return;
        SnippetCopyRequested?.Invoke(this, snippet);
    }

    [RelayCommand]
    private void Edit() => EditRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void About() => AboutRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void Quit() => QuitRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void CopyRecent(RecentClip? clip)
    {
        if (clip is null) return;
        RecentClipCopyRequested?.Invoke(this, clip);
    }

    [RelayCommand]
    private void PromoteRecent(RecentClip? clip)
    {
        if (clip is null) return;
        PromoteRecentClipRequested?.Invoke(this, clip);
    }
}

/// <summary>Display row for a recent clipboard item in the flyout (F19).</summary>
public sealed class RecentClipRow
{
    public RecentClip Clip { get; }
    public string Preview => Clip.Preview;
    public string Body => Clip.Body;

    public RecentClipRow(RecentClip clip)
    {
        Clip = clip;
    }
}

/// <summary>One group chip in the flyout pivot strip (F20).</summary>
public sealed class GroupChip
{
    public long Id { get; }
    public string Name { get; }
    public int Count { get; }
    public string Label => Count > 0 ? $"{Name} · {Count}" : Name;

    public GroupChip(long id, string name, int count)
    {
        Id = id;
        Name = name;
        Count = count;
    }
}
