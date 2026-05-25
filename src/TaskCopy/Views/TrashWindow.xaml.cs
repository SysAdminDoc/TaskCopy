using System.Windows;
using TaskCopy.ViewModels;

namespace TaskCopy.Views;

public partial class TrashWindow : Window
{
    public TrashViewModel ViewModel { get; }

    public TrashWindow(TrashViewModel vm)
    {
        ViewModel = vm;
        DataContext = vm;
        InitializeComponent();
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
