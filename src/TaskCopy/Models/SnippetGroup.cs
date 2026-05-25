using CommunityToolkit.Mvvm.ComponentModel;

namespace TaskCopy.Models;

public partial class SnippetGroup : ObservableObject
{
    [ObservableProperty]
    private long _id;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private int _sortOrder;
}
