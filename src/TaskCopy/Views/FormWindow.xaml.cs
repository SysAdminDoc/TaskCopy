using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;

namespace TaskCopy.Views;

public partial class FormWindow : Window
{
    private readonly Dictionary<string, TextBox> _boxes = new(StringComparer.OrdinalIgnoreCase);
    private TextBox? _firstBox;

    public IReadOnlyDictionary<string, string>? Result { get; private set; }

    private FormWindow(IReadOnlyList<string> fields)
    {
        InitializeComponent();
        Title = "TaskCopy - Snippet fields";

        foreach (var field in fields)
        {
            var label = new TextBlock
            {
                Text = field,
                Style = (Style)Application.Current.Resources["Mocha.Body.Subtle"],
                Margin = new Thickness(0, _boxes.Count == 0 ? 0 : 10, 0, 4),
            };
            FieldsPanel.Children.Add(label);

            var box = new TextBox
            {
                Style = (Style)Application.Current.Resources["Mocha.TextBox"],
                MinWidth = 360,
            };
            AutomationProperties.SetName(box, field);
            box.KeyDown += OnTextBoxKeyDown;
            FieldsPanel.Children.Add(box);

            _boxes[field] = box;
            _firstBox ??= box;
        }

        Loaded += (_, _) =>
        {
            if (_firstBox is null) return;
            _firstBox.Focus();
            Keyboard.Focus(_firstBox);
        };
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in _boxes)
        {
            values[entry.Key] = entry.Value.Text;
        }
        Result = values;
        DialogResult = true;
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        Result = null;
        DialogResult = false;
        Close();
    }

    private void OnTextBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            OnCancel(sender, e);
            e.Handled = true;
        }
    }

    public static IReadOnlyDictionary<string, string>? Prompt(IReadOnlyList<string> fields, Window? owner = null)
    {
        var normalized = fields
            .Where(f => !string.IsNullOrWhiteSpace(f))
            .Select(f => f.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (normalized.Count == 0)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var w = new FormWindow(normalized);
        if (owner is not null) w.Owner = owner;
        var ok = w.ShowDialog() == true;
        return ok ? w.Result : null;
    }
}
