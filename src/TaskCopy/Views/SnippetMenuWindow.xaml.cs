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

    /// <summary>F50: set when the caller opted into "Last position (sticky)" — on close we persist `Left`/`Top` back to settings.</summary>
    private Data.SettingsStore? _persistSettings;

    public SnippetMenuWindow(SnippetMenuViewModel vm)
    {
        _vm = vm;
        DataContext = vm;
        InitializeComponent();

        Deactivated += OnDeactivated;
        PreviewKeyDown += OnPreviewKeyDown;
        Closing += OnClosingPersistPosition;
    }

    private void OnClosingPersistPosition(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // Persist final position so the next sticky open lands here.
        if (_persistSettings is not null)
        {
            try { _persistSettings.FlyoutLastPosition = (Left, Top); }
            catch (Exception ex) { Services.CrashLog.Write("SnippetMenuWindow.PersistPosition", ex); }
        }
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

    /// <summary>
    /// F50: when settings is supplied AND position == LastPosition, restore
    /// the stored DIPs and write a fresh position when the window closes.
    /// Settings is optional so existing callers that don't care still work.
    /// </summary>
    public void ShowAtCursor(Data.FlyoutPosition position = Data.FlyoutPosition.Cursor,
                              Data.SettingsStore? settings = null)
    {
        _vm.Refresh();

        var (cursor, workArea, scale) = NativeMethods.GetCursorContext();
        if (scale <= 0) scale = 1.0;

        // Measure first so we know how big the window will be.
        Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var size = DesiredSize;

        var widthPx = size.Width * scale;
        var heightPx = size.Height * scale;

        double leftPx, topPx;

        // F50: when sticky mode is on AND we have a recorded position, restore
        // it. NaN means "no position recorded yet" → fall through to cursor.
        var stickyApplied = false;
        if (position == Data.FlyoutPosition.LastPosition && settings is not null)
        {
            _persistSettings = settings;
            var (lastX, lastY) = settings.FlyoutLastPosition;
            if (!double.IsNaN(lastX) && !double.IsNaN(lastY))
            {
                leftPx = lastX * scale;
                topPx = lastY * scale;
                stickyApplied = true;
            }
            else
            {
                leftPx = cursor.X - widthPx + 24;
                topPx = cursor.Y - heightPx + 24;
            }
        }
        else if (position == Data.FlyoutPosition.MonitorCenter)
        {
            // I19: useful on ultrawide monitors where above-and-left of cursor
            // can pin the flyout to one screen edge. Center horizontally on the
            // cursor's active monitor; vertically just above the middle so the
            // user's eye doesn't track far.
            leftPx = workArea.Left + ((workArea.Right - workArea.Left) - widthPx) / 2.0;
            topPx = workArea.Top + ((workArea.Bottom - workArea.Top) - heightPx) / 2.5;
        }
        else
        {
            // Open above-and-left of the cursor (like a real context menu),
            // then clamp into the monitor work area.
            leftPx = cursor.X - widthPx + 24;
            topPx = cursor.Y - heightPx + 24;
        }
        _ = stickyApplied; // diagnostic only; keeps the local readable


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
