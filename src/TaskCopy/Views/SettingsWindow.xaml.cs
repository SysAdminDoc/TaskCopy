using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TaskCopy.Models;
using TaskCopy.ViewModels;

namespace TaskCopy.Views;

public partial class SettingsWindow : Window
{
    private enum CaptureMode { None, PrimaryHotkey, QuickHotkey }

    private readonly SettingsViewModel _vm;
    private CaptureMode _capture = CaptureMode.None;
    private Point? _dragOrigin;
    private Snippet? _draggedSnippet;

    public SettingsWindow(SettingsViewModel vm)
    {
        _vm = vm;
        DataContext = vm;
        InitializeComponent();

        _vm.HotkeyRebindRequested += (_, _) => BeginCapture(CaptureMode.PrimaryHotkey, "Press the new key combination (Esc to cancel)…");
        _vm.QuickHotkeyRebindRequested += (_, snippet) =>
            BeginCapture(CaptureMode.QuickHotkey, $"Press the per-snippet combo for \"{snippet.Title}\" (Esc to cancel)…");
        PreviewKeyDown += OnPreviewKeyDown;
    }

    private void BeginCapture(CaptureMode mode, string message)
    {
        _capture = mode;
        _vm.StatusMessage = message;
        Focus();
        Keyboard.Focus(this);
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_capture == CaptureMode.None) return;

        if (e.Key == Key.Escape)
        {
            _capture = CaptureMode.None;
            _vm.StatusMessage = "Hotkey unchanged.";
            e.Handled = true;
            return;
        }

        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        // Ignore bare modifier presses; wait for the actual key.
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
        {
            e.Handled = true;
            return;
        }

        var modifiers = Keyboard.Modifiers;
        if (modifiers == ModifierKeys.None)
        {
            _vm.StatusMessage = "Add at least one modifier (Ctrl / Alt / Shift / Win).";
            e.Handled = true;
            return;
        }

        var capturedMode = _capture;
        _capture = CaptureMode.None;
        if (capturedMode == CaptureMode.PrimaryHotkey)
        {
            _vm.SetHotkey(key, modifiers);
        }
        else
        {
            _vm.SetQuickHotkey(key, modifiers);
        }
        e.Handled = true;
    }

    private void OnInsertDate(object sender, RoutedEventArgs e) => InsertAtCaret("{{date}}");
    private void OnInsertTime(object sender, RoutedEventArgs e) => InsertAtCaret("{{time}}");
    private void OnInsertClipboard(object sender, RoutedEventArgs e) => InsertAtCaret("{{clipboard}}");
    private void OnInsertCursor(object sender, RoutedEventArgs e) => InsertAtCaret("{{cursor}}");

    private void OnInsertAsk(object sender, RoutedEventArgs e)
    {
        var field = AskWindow.Prompt("Prompt label", this);
        if (string.IsNullOrEmpty(field)) return;
        InsertAtCaret($"{{{{ask:{field}}}}}");
    }

    private void OnInsertForm(object sender, RoutedEventArgs e)
    {
        var fields = AskWindow.Prompt("Form fields (separate with |)", this);
        if (string.IsNullOrWhiteSpace(fields)) return;
        InsertAtCaret($"{{{{form:{fields}}}}}");
    }

    // -----------------------------------------------------------------------
    // Keyboard accelerators on the snippet list (I32)
    // -----------------------------------------------------------------------

    /// <summary>
    /// I41 (light): right-click on a snippet row in Settings → "Move to group →
    /// (Ungrouped) / Work / Personal / ..." Picks straight from the live
    /// `Groups` collection so the menu reflects whatever the user has defined.
    /// </summary>
    private void OnSnippetListContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        SnippetListContextMenu.Items.Clear();
        if (_vm.SelectedSnippet is null)
        {
            e.Handled = true;
            return;
        }

        var moveToHeader = new MenuItem
        {
            Header = $"Move \"{_vm.SelectedSnippet.Title}\" to…",
            Style = (Style)Application.Current.Resources["Mocha.MenuItem"],
        };
        SnippetListContextMenu.Items.Add(moveToHeader);

        foreach (var g in _vm.Groups)
        {
            var captured = g;
            var item = new MenuItem
            {
                Header = g.Name,
                Style = (Style)Application.Current.Resources["Mocha.MenuItem"],
            };
            item.Click += (_, _) => _vm.EditGroup = captured;
            moveToHeader.Items.Add(item);
        }
    }

    private void OnSnippetListKeyDown(object sender, KeyEventArgs e)
    {
        // Don't steal keys when the user is interacting with the rename-in-place
        // editor or any other focused TextBox (none today, but defensive).
        if (Keyboard.FocusedElement is TextBox) return;

        if (e.Key == Key.Delete && _vm.HasSelection)
        {
            _vm.DeleteSnippetCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.N && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            _vm.AddSnippetCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.F2 && _vm.HasSelection)
        {
            // Focus the Title editor with the existing text selected so the
            // user can immediately overwrite — same gesture as Explorer rename.
            var titleBox = FindTitleBox();
            if (titleBox is not null)
            {
                titleBox.Focus();
                Keyboard.Focus(titleBox);
                titleBox.SelectAll();
            }
            e.Handled = true;
        }
    }

    private TextBox? FindTitleBox()
    {
        // The title TextBox is the first one declared in the right-hand editor grid;
        // walk the visual tree from this window to find it. Tag-based selection
        // would be cleaner but this is one call per F2 keypress.
        return FindDescendant<TextBox>(this, tb =>
            tb.DataContext == _vm
            && AutomationProperties.GetName(tb) == "Snippet title");
    }

    private static T? FindDescendant<T>(DependencyObject root, Func<T, bool> match) where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T t && match(t)) return t;
            var deeper = FindDescendant<T>(child, match);
            if (deeper is not null) return deeper;
        }
        return null;
    }

    // -----------------------------------------------------------------------
    // Drag-reorder for the snippet list (I7)
    // -----------------------------------------------------------------------

    private void OnSnippetListMouseDown(object sender, MouseButtonEventArgs e)
    {
        _dragOrigin = e.GetPosition(SnippetList);
        _draggedSnippet = (FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject))?.DataContext as Snippet;
    }

    private void OnSnippetListMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _draggedSnippet is null || _dragOrigin is null) return;
        var pos = e.GetPosition(SnippetList);
        if (Math.Abs(pos.X - _dragOrigin.Value.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(pos.Y - _dragOrigin.Value.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }
        var payload = _draggedSnippet;
        _dragOrigin = null;
        _draggedSnippet = null;
        try
        {
            DragDrop.DoDragDrop(SnippetList, payload, DragDropEffects.Move);
        }
        catch
        {
            // tolerate aborted drags (e.g. another modal stole focus)
        }
    }

    private void OnSnippetListDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(Snippet)) ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnSnippetListDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(Snippet)) is not Snippet source) return;
        var target = (FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject))?.DataContext as Snippet;
        if (target is null || ReferenceEquals(source, target)) return;

        var oldIdx = _vm.Snippets.IndexOf(source);
        var newIdx = _vm.Snippets.IndexOf(target);
        if (oldIdx < 0 || newIdx < 0 || oldIdx == newIdx) return;

        _vm.Snippets.Move(oldIdx, newIdx);
        _vm.PersistCurrentOrder();
        _vm.SelectedSnippet = source;
    }

    private static T? FindAncestor<T>(DependencyObject? start) where T : DependencyObject
    {
        var cur = start;
        while (cur is not null)
        {
            if (cur is T t) return t;
            cur = VisualTreeHelper.GetParent(cur);
        }
        return null;
    }

    private void InsertAtCaret(string text)
    {
        if (!_vm.HasSelection) return;
        var caret = BodyEditor.CaretIndex;
        var selStart = BodyEditor.SelectionStart;
        var selLen = BodyEditor.SelectionLength;

        var body = _vm.EditBody ?? string.Empty;
        if (selLen > 0)
        {
            body = body.Remove(selStart, selLen).Insert(selStart, text);
            caret = selStart + text.Length;
        }
        else
        {
            body = body.Insert(caret, text);
            caret += text.Length;
        }
        _vm.EditBody = body;
        BodyEditor.Focus();
        BodyEditor.CaretIndex = caret;
    }
}
