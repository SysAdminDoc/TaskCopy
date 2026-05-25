using System.Windows;

namespace TaskCopy.Views;

/// <summary>
/// F47: WPF custom dialog so the delete confirm can carry a "Don't ask again"
/// checkbox (MessageBox doesn't ship one). The result tuple gives the caller
/// both the OK/Cancel decision and the suppress flag.
/// </summary>
public partial class ConfirmDeleteWindow : Window
{
    public bool Confirmed { get; private set; }
    public bool DontAskAgain { get; private set; }

    private ConfirmDeleteWindow(string snippetTitle)
    {
        InitializeComponent();
        PromptText.Text = $"Delete snippet “{snippetTitle}”?";
    }

    private void OnDelete(object sender, RoutedEventArgs e)
    {
        Confirmed = true;
        DontAskAgain = DontAskAgainBox.IsChecked == true;
        DialogResult = true;
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        Confirmed = false;
        DontAskAgain = false;
        DialogResult = false;
        Close();
    }

    /// <summary>Shows the modal and returns (confirmed, dontAskAgain).</summary>
    public static (bool Confirmed, bool DontAskAgain) Prompt(string snippetTitle, Window? owner = null)
    {
        var w = new ConfirmDeleteWindow(snippetTitle);
        if (owner is not null) w.Owner = owner;
        var ok = w.ShowDialog() == true;
        return (ok && w.Confirmed, w.DontAskAgain);
    }
}
