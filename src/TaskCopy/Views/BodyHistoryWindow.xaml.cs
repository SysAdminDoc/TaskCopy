using System.Windows;
using TaskCopy.ViewModels;

namespace TaskCopy.Views;

public partial class BodyHistoryWindow : Window
{
    public BodyHistoryViewModel ViewModel { get; }

    public BodyHistoryWindow(BodyHistoryViewModel vm)
    {
        ViewModel = vm;
        DataContext = vm;
        InitializeComponent();
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
