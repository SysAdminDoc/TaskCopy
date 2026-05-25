using System.Windows;
using System.Windows.Input;

namespace TaskCopy.Views;

public partial class AskWindow : Window
{
    public string? Result { get; private set; }

    private AskWindow(string field)
    {
        InitializeComponent();
        Title = $"TaskCopy — {field}";
        PromptLabel.Text = field;
        Loaded += (_, _) => { ValueBox.Focus(); Keyboard.Focus(ValueBox); };
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        Result = ValueBox.Text;
        DialogResult = true;
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        Result = null;
        DialogResult = false;
        Close();
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            OnCancel(sender, e);
            e.Handled = true;
        }
    }

    /// <summary>
    /// Shows a modal prompt and returns the entered text, or null if cancelled.
    /// Must be called on the UI thread.
    /// </summary>
    public static string? Prompt(string field, Window? owner = null)
    {
        var w = new AskWindow(field);
        if (owner is not null) w.Owner = owner;
        var ok = w.ShowDialog() == true;
        return ok ? w.Result : null;
    }
}
