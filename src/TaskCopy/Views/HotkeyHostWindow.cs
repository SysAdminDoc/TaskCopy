using System.Windows;
using System.Windows.Interop;

namespace TaskCopy.Views;

/// <summary>
/// Invisible window whose only purpose is to host the global hotkey HWND.
/// NHotkey.Wpf uses <c>Application.Current.MainWindow</c> for registration.
/// </summary>
public sealed class HotkeyHostWindow : Window
{
    public HotkeyHostWindow()
    {
        Width = 0;
        Height = 0;
        Left = -32000;
        Top = -32000;
        WindowStyle = WindowStyle.None;
        ShowInTaskbar = false;
        ShowActivated = false;
        AllowsTransparency = true;
        Background = System.Windows.Media.Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        Visibility = Visibility.Hidden;

        // Force HWND creation without ever calling Show.
        new WindowInteropHelper(this).EnsureHandle();
    }
}
