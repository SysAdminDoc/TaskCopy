using System.Windows;
using System.Windows.Input;
using TaskCopy.Services;
using TaskCopy.ViewModels;

namespace TaskCopy.Views;

public partial class SnippetMenuWindow : Window
{
    private readonly SnippetMenuViewModel _vm;

    public SnippetMenuWindow(SnippetMenuViewModel vm)
    {
        _vm = vm;
        DataContext = vm;
        InitializeComponent();

        Deactivated += (_, _) => Close();
        KeyDown += OnKeyDown;
        vm.SnippetCopied += (_, _) => Dispatcher.Invoke(Close);
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) Close();
    }

    public void ShowAtCursor()
    {
        _vm.Refresh();

        var (cursor, workArea, scale) = NativeMethods.GetCursorContext();
        if (scale <= 0) scale = 1.0;

        // Measure first so we know how big the window will be.
        Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var size = DesiredSize;

        var widthPx = size.Width * scale;
        var heightPx = size.Height * scale;

        // Open above-and-left of the cursor (like a real context menu),
        // then clamp into the monitor work area.
        var leftPx = cursor.X - widthPx + 24;
        var topPx = cursor.Y - heightPx + 24;

        if (leftPx + widthPx > workArea.Right) leftPx = workArea.Right - widthPx - 4;
        if (leftPx < workArea.Left + 4) leftPx = workArea.Left + 4;
        if (topPx + heightPx > workArea.Bottom) topPx = workArea.Bottom - heightPx - 4;
        if (topPx < workArea.Top + 4) topPx = workArea.Top + 4;

        Left = leftPx / scale;
        Top = topPx / scale;

        Show();
        Activate();
        Focus();
    }
}
