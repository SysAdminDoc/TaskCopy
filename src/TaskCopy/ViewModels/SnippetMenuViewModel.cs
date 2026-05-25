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

    /// <summary>F35: caller supplies the captured foreground process name so per-snippet target_app_glob can filter.</summary>
    public string? CurrentTargetApp { get; set; }

    // I37: BulkObservableCollection suppresses per-item CollectionChanged
    // events during ApplyFilter's rebuild, then fires one Reset. WPF rebinds
    // the whole list in one frame instead of issuing N add/remove events —
    // measurable improvement at >500-snippet libraries during typing.
    public BulkObservableCollection<SnippetRow> Snippets { get; } = new();
    public BulkObservableCollection<RecentClipRow> RecentClips { get; } = new();
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
    /// F32: ordered list of snippet IDs the user has multi-picked via Ctrl+Click
    /// or Ctrl+Enter. When non-empty, Enter pastes all of them concatenated with
    /// <see cref="SettingsStore.MultiPasteSeparator"/>. Esc clears.
    /// </summary>
    private readonly List<long> _multiSelection = new();
    public IReadOnlyList<long> MultiSelection => _multiSelection;

    [ObservableProperty]
    private bool _hasMultiSelection;

    /// <summary>Label for the footer hint: "Paste 3 selected (Enter)" or empty.</summary>
    public string MultiSelectionLabel => _multiSelection.Count == 0
        ? string.Empty
        : $"Paste {_multiSelection.Count} selected (Enter)";

    /// <summary>
    /// Raised when the user picks a snippet. The orchestrator (App) expands
    /// placeholders, writes the clipboard, records use, closes the flyout,
    /// and triggers auto-paste.
    /// </summary>
    public event EventHandler<Snippet>? SnippetCopyRequested;
    /// <summary>F32: raised when the user confirms multi-paste with Enter.</summary>
    public event EventHandler<IReadOnlyList<Snippet>>? MultiSnippetCopyRequested;
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
        // I23: MostUsed uses decay-weighted frecency so a snippet used 100x last
        // year doesn't outrank one used 5x today. Tau = 7 days.
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        IEnumerable<Snippet> ordered = mode switch
        {
            FlyoutSortMode.MostUsed => input
                .OrderByDescending(s => s.Pinned)
                .ThenByDescending(s => Frecency(s, now))
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

    /// <summary>
    /// I23: count × exp(-Δt / τ) with τ = 7 days. Never-used snippets fall back
    /// to their used_count (which is 0) so they sit at the bottom in MostUsed mode.
    /// </summary>
    private static double Frecency(Snippet s, long nowSeconds)
    {
        if (s.UsedCount == 0) return 0.0;
        var last = s.LastUsedAt ?? s.CreatedAt;
        var ageDays = Math.Max(0, (nowSeconds - last) / 86400.0);
        const double Tau = 7.0;
        return s.UsedCount * Math.Exp(-ageDays / Tau);
    }

    partial void OnFilterChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        var q = (Filter ?? string.Empty).Trim();
        var groupId = SelectedGroupId;
        // I37: build the new lists in plain List<T>, then ReplaceAll into the
        // BulkObservableCollections so WPF sees one Reset notification.
        var nextSnippets = new List<SnippetRow>();
        var nextRecent = new List<RecentClipRow>();

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

            // F35: per-app rule — hide snippets whose target_app_glob is set
            // and doesn't match the captured foreground process. Unset = universal.
            if (!AppGlob.Matches(s.TargetAppGlob, CurrentTargetApp)) continue;

            nextSnippets.Add(new SnippetRow(s, displayIndex));
            displayIndex++;
        }
        Snippets.ReplaceAll(nextSnippets);
        HasSnippets = Snippets.Count > 0;

        // Recent clips — filter is applied to body only.
        foreach (var c in _allRecent)
        {
            if (string.IsNullOrEmpty(q) || c.Body.Contains(q, StringComparison.OrdinalIgnoreCase))
            {
                nextRecent.Add(new RecentClipRow(c));
            }
        }
        RecentClips.ReplaceAll(nextRecent);
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

    /// <summary>
    /// F32: toggle the highlighted snippet's membership in the multi-paste set.
    /// Returns true if a change happened (so the caller can refresh the row UI).
    /// </summary>
    public bool ToggleMultiSelectionAtIndex(int oneBasedOrZeroBased, bool isOneBased = false)
    {
        var idx = isOneBased ? oneBasedOrZeroBased - 1 : oneBasedOrZeroBased;
        if (idx < 0 || idx >= Snippets.Count) return false;
        var id = Snippets[idx].Snippet.Id;
        if (_multiSelection.Remove(id)) { /* removed */ }
        else { _multiSelection.Add(id); }
        HasMultiSelection = _multiSelection.Count > 0;
        OnPropertyChanged(nameof(MultiSelectionLabel));
        UpdateRowSelectionState();
        return true;
    }

    public void ClearMultiSelection()
    {
        if (_multiSelection.Count == 0) return;
        _multiSelection.Clear();
        HasMultiSelection = false;
        OnPropertyChanged(nameof(MultiSelectionLabel));
        UpdateRowSelectionState();
    }

    public bool IsMultiSelected(long snippetId) => _multiSelection.Contains(snippetId);

    /// <summary>
    /// F32: if there's a multi-selection, paste it. Returns true when the
    /// event fired so the caller knows to skip the single-snippet path.
    /// </summary>
    public bool TryCopyMultiSelection()
    {
        if (_multiSelection.Count == 0) return false;
        var snippets = _multiSelection
            .Select(id => _all.FirstOrDefault(s => s.Id == id))
            .Where(s => s is not null)
            .Cast<Snippet>()
            .ToList();
        if (snippets.Count == 0) return false;
        MultiSnippetCopyRequested?.Invoke(this, snippets);
        return true;
    }

    private void UpdateRowSelectionState()
    {
        // Force per-row re-render of any UI bound to IsMultiSelected. The
        // BulkObservableCollection doesn't fire per-item PropertyChanged, so
        // expose a virtual "version" property the view can rebind on.
        OnPropertyChanged(nameof(MultiSelection));
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
