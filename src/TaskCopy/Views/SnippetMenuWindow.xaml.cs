using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TaskCopy.Services;
using TaskCopy.ViewModels;

namespace TaskCopy.Views;

public partial class SnippetMenuWindow : Window
{
    private readonly SnippetMenuViewModel _vm;
    private readonly uint _ownPid = (uint)Environment.ProcessId;

    public SnippetMenuWindow(SnippetMenuViewModel vm)
    {
        _vm = vm;
        DataContext = vm;
        InitializeComponent();

        Deactivated += OnDeactivated;
        PreviewKeyDown += OnPreviewKeyDown;
    }

    private void OnDeactivated(object? sender, EventArgs e)
    {
        // Stay open if focus moved to another TaskCopy window (e.g. Settings
        // about to open, an in-process popup). Close on third-party focus.
        var fg = NativeMethods.GetForegroundWindow();
        if (fg == IntPtr.Zero) { Close(); return; }
        NativeMethods.GetWindowThreadProcessId(fg, out var pid);
        if (pid != _ownPid) Close();
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                if (_vm.ClearFilterIfAny())
                {
                    SearchBox.CaretIndex = 0;
                    e.Handled = true;
                    return;
                }
                Close();
                e.Handled = true;
                return;

            case Key.Down:
                _vm.MoveSelection(+1);
                SnippetList.ScrollIntoView(SnippetList.SelectedItem);
                e.Handled = true;
                return;

            case Key.Up:
                _vm.MoveSelection(-1);
                SnippetList.ScrollIntoView(SnippetList.SelectedItem);
                e.Handled = true;
                return;

            case Key.PageDown:
                _vm.MoveSelection(+8);
                SnippetList.ScrollIntoView(SnippetList.SelectedItem);
                e.Handled = true;
                return;

            case Key.PageUp:
                _vm.MoveSelection(-8);
                SnippetList.ScrollIntoView(SnippetList.SelectedItem);
                e.Handled = true;
                return;

            case Key.Enter:
                _vm.CopySelected();
                e.Handled = true;
                return;
        }

        // Number-key quick-pick (Alt+1..9). Plain 1..9 stays available for the
        // search box (e.g. searching "PO #1"); Alt is the unambiguous picker.
        if (Keyboard.Modifiers == ModifierKeys.Alt
            && e.SystemKey >= Key.D1 && e.SystemKey <= Key.D9)
        {
            _vm.CopyAtVisibleIndex((int)(e.SystemKey - Key.D0));
            e.Handled = true;
            return;
        }
        if (Keyboard.Modifiers == ModifierKeys.Alt
            && e.SystemKey >= Key.NumPad1 && e.SystemKey <= Key.NumPad9)
        {
            _vm.CopyAtVisibleIndex((int)(e.SystemKey - Key.NumPad0));
            e.Handled = true;
            return;
        }
    }

    private void OnSnippetRowClicked(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem { DataContext: SnippetRow row })
        {
            _vm.CopyCommand.Execute(row.Snippet);
            e.Handled = true;
        }
    }

    private void OnRecentClipClicked(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem { DataContext: RecentClipRow row })
        {
            _vm.CopyRecentCommand.Execute(row.Clip);
            e.Handled = true;
        }
    }

    private void OnRecentCopyMenu(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is RecentClipRow row)
        {
            _vm.CopyRecentCommand.Execute(row.Clip);
        }
    }

    private void OnRecentPromoteMenu(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is RecentClipRow row)
        {
            _vm.PromoteRecentCommand.Execute(row.Clip);
        }
    }

    private void OnGroupChipClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: long id })
        {
            _vm.SelectGroup(id);
            // Refocus search so subsequent typing keeps filtering.
            SearchBox.Focus();
            Keyboard.Focus(SearchBox);
        }
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
        SearchBox.Focus();
        Keyboard.Focus(SearchBox);
    }
}
