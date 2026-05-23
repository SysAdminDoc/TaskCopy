using System.Windows;
using System.Windows.Input;
using TaskCopy.ViewModels;

namespace TaskCopy.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _vm;
    private bool _capturingHotkey;

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
}
