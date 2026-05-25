using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TaskCopy.Models;
using TaskCopy.ViewModels;

namespace TaskCopy.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _vm;
    private bool _capturingHotkey;
    private Point? _dragOrigin;
    private Snippet? _draggedSnippet;

    public SettingsWindow(SettingsViewModel vm)
    {
        _vm = vm;
        DataContext = vm;
        InitializeComponent();

        _vm.HotkeyRebindRequested += (_, _) => BeginHotkeyCapture();
        PreviewKeyDown += OnPreviewKeyDown;
    }

    private void BeginHotkeyCapture()
    {
        _capturingHotkey = true;
        _vm.StatusMessage = "Press the new key combination (Esc to cancel)…";
        Focus();
        Keyboard.Focus(this);
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!_capturingHotkey) return;

        if (e.Key == Key.Escape)
        {
            _capturingHotkey = false;
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

        _capturingHotkey = false;
        _vm.SetHotkey(key, modifiers);
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
