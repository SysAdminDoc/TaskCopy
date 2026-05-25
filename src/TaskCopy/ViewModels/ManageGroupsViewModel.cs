using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskCopy.Data;
using TaskCopy.Models;

namespace TaskCopy.ViewModels;

public partial class ManageGroupsViewModel : ObservableObject
{
    private readonly SnippetDatabase _db;

    public ObservableCollection<SnippetGroup> Groups { get; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeleteGroupCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveGroupUpCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveGroupDownCommand))]
    private SnippetGroup? _selectedGroup;

    [ObservableProperty]
    private string _newGroupName = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public ManageGroupsViewModel(SnippetDatabase db)
    {
        _db = db;
        Reload();
    }

    public void Reload()
    {
        Groups.Clear();
        foreach (var g in _db.GetGroups()) Groups.Add(g);
    }

    [RelayCommand]
    private void AddGroup()
    {
        var name = (NewGroupName ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(name))
        {
            StatusMessage = "Enter a group name first.";
            return;
        }
        if (Groups.Any(g => string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            StatusMessage = $"Group \"{name}\" already exists.";
            return;
        }
        var id = _db.InsertGroup(name);
        Groups.Add(new SnippetGroup { Id = id, Name = name, SortOrder = Groups.Count - 1 });
        NewGroupName = string.Empty;
        StatusMessage = $"Added group \"{name}\".";
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void DeleteGroup()
    {
        if (SelectedGroup is null) return;
        var name = SelectedGroup.Name;
        _db.DeleteGroup(SelectedGroup.Id);
        Groups.Remove(SelectedGroup);
        StatusMessage = $"Deleted group \"{name}\". Its snippets were moved to Ungrouped.";
        SelectedGroup = null;
    }

    [RelayCommand(CanExecute = nameof(CanMoveUp))]
    private void MoveGroupUp()
    {
        if (SelectedGroup is null) return;
        var idx = Groups.IndexOf(SelectedGroup);
        if (idx <= 0) return;
        Groups.Move(idx, idx - 1);
        PersistOrder();
    }

    [RelayCommand(CanExecute = nameof(CanMoveDown))]
    private void MoveGroupDown()
    {
        if (SelectedGroup is null) return;
        var idx = Groups.IndexOf(SelectedGroup);
        if (idx < 0 || idx >= Groups.Count - 1) return;
        Groups.Move(idx, idx + 1);
        PersistOrder();
    }

    public bool HasSelection => SelectedGroup is not null;
    private bool CanMoveUp() => SelectedGroup is not null && Groups.IndexOf(SelectedGroup) > 0;
    private bool CanMoveDown() => SelectedGroup is not null && Groups.IndexOf(SelectedGroup) < Groups.Count - 1;

    private void PersistOrder() => _db.ReorderGroups(Groups.Select(g => g.Id).ToList());

    public void RenameSelected(string newName)
    {
        if (SelectedGroup is null) return;
        var name = newName.Trim();
        if (string.IsNullOrEmpty(name)) return;
        _db.RenameGroup(SelectedGroup.Id, name);
        SelectedGroup.Name = name;
        StatusMessage = $"Renamed to \"{name}\".";
    }
}
