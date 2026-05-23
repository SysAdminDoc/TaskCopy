using CommunityToolkit.Mvvm.ComponentModel;

namespace TaskCopy.Models;

public partial class Snippet : ObservableObject
{
    [ObservableProperty]
    private long _id;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _body = string.Empty;

    [ObservableProperty]
    private int _sortOrder;

    [ObservableProperty]
    private long _createdAt;

    public string Preview
    {
        get
        {
            if (string.IsNullOrEmpty(Body)) return string.Empty;
            var line = Body.Split('\n', 2)[0].Trim();
            return line.Length > 80 ? line[..80] + "…" : line;
        }
    }

    partial void OnBodyChanged(string value) => OnPropertyChanged(nameof(Preview));
}
