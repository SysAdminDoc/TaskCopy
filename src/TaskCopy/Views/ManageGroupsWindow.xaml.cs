using System.Windows;
using System.Windows.Input;
using TaskCopy.ViewModels;

namespace TaskCopy.Views;

public partial class ManageGroupsWindow : Window
{
    private readonly ManageGroupsViewModel _vm;

    public ManageGroupsWindow(ManageGroupsViewModel vm)
    {
        _vm = vm;
        DataContext = vm;
        InitializeComponent();
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    private void OnRenameClicked(object sender, RoutedEventArgs e) => PromptRename();

    private void OnGroupDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_vm.HasSelection) PromptRename();
    }

    private void PromptRename()
    {
        if (!_vm.HasSelection) return;
        var current = _vm.SelectedGroup?.Name ?? string.Empty;
        var entered = AskWindow.Prompt($"Rename group (current: {current})", this);
        if (string.IsNullOrWhiteSpace(entered)) return;
        _vm.RenameSelected(entered);
    }
}
