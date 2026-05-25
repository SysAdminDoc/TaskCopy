using System.Windows;
using ICSharpCode.AvalonEdit;

namespace TaskCopy.Views;

public static class AvalonEditBinding
{
    public static readonly DependencyProperty BindableTextProperty =
        DependencyProperty.RegisterAttached(
            "BindableText",
            typeof(string),
            typeof(AvalonEditBinding),
            new FrameworkPropertyMetadata(
                string.Empty,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnBindableTextChanged));

    public static string GetBindableText(DependencyObject obj)
        => (string)obj.GetValue(BindableTextProperty);

    public static void SetBindableText(DependencyObject obj, string value)
        => obj.SetValue(BindableTextProperty, value);

    private static void OnBindableTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextEditor editor) return;
        var newText = e.NewValue as string ?? string.Empty;

        editor.TextChanged -= OnEditorTextChanged;
        if (editor.Text != newText) editor.Text = newText;
        editor.TextChanged += OnEditorTextChanged;
    }

    private static void OnEditorTextChanged(object? sender, EventArgs e)
    {
        if (sender is TextEditor editor)
        {
            SetBindableText(editor, editor.Text);
        }
    }
}
