using System.Windows;
using Microsoft.Win32;
using TaskCopy.Data;

namespace TaskCopy.Services;

/// <summary>
/// Swaps the merged palette resource dictionary at startup based on the
/// stored Theme preference. Auto reads HKCU AppsUseLightTheme.
/// Theme changes after startup require an app restart (the styles use
/// StaticResource bindings; swapping mid-flight would not re-evaluate them).
/// </summary>
public static class ThemeService
{
    private const string MochaUri = "pack://application:,,,/Themes/Mocha.xaml";
    private const string LatteUri = "pack://application:,,,/Themes/Latte.xaml";

    /// <summary>
    /// Resolve a Theme preference (Mocha / Latte / Auto) to the concrete
    /// palette to apply. Auto returns Latte if the OS apps theme is Light.
    /// </summary>
    public static Theme Resolve(Theme preference)
    {
        if (preference == Theme.Auto) return IsSystemLight() ? Theme.Latte : Theme.Mocha;
        return preference;
    }

    /// <summary>
    /// Replace the first merged dictionary in App.Resources (the palette slot)
    /// with the target theme. Must run before any window is shown — the styles
    /// resolve brushes via StaticResource at parse time and won't react to a
    /// later swap.
    /// </summary>
    public static void Apply(Theme concrete)
    {
        var app = Application.Current;
        if (app is null) return;

        var uri = concrete == Theme.Latte ? LatteUri : MochaUri;
        var dict = new ResourceDictionary { Source = new Uri(uri, UriKind.Absolute) };

        if (app.Resources.MergedDictionaries.Count == 0)
        {
            app.Resources.MergedDictionaries.Add(dict);
        }
        else
        {
            app.Resources.MergedDictionaries[0] = dict;
        }
    }

    private static bool IsSystemLight()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", writable: false);
            return key?.GetValue("AppsUseLightTheme") is int v && v != 0;
        }
        catch
        {
            return false;
        }
    }
}
