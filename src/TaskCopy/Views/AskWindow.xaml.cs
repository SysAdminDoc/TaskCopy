using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;

namespace TaskCopy.Views;

public partial class AskWindow : Window
{
    private readonly bool _isSecret;

    public string? Result { get; private set; }

    private AskWindow(string field, bool isSecret = false)
    {
        InitializeComponent();
        _isSecret = isSecret;
        Title = $"TaskCopy — {field}";
        PromptLabel.Text = field;
        AutomationProperties.SetName(ValueBox, field);
        AutomationProperties.SetName(SecretBox, field);

        if (_isSecret)
        {
            ValueBox.Visibility = Visibility.Collapsed;
            SecretBox.Visibility = Visibility.Visible;
        }

        Loaded += (_, _) =>
        {
            if (_isSecret)
            {
                SecretBox.Focus();
                Keyboard.Focus(SecretBox);
            }
            else
            {
                ValueBox.Focus();
                Keyboard.Focus(ValueBox);
            }
        };
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        Result = _isSecret ? SecretBox.Password : ValueBox.Text;
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

    public static string? PromptSecret(string field, Window? owner = null)
    {
        var w = new AskWindow(field, isSecret: true);
        if (owner is not null) w.Owner = owner;
        var ok = w.ShowDialog() == true;
        return ok ? w.Result : null;
    }
}
