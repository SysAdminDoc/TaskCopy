using System.Windows;
using Microsoft.Win32;
using TaskCopy.Data;

namespace TaskCopy.Services;

/// <summary>
/// Swaps the merged palette resource dictionary at startup based on the
/// stored Theme preference. Auto reads HKCU AppsUseLightTheme.
/// Theme changes after startup require an app restart (the styles use
/// StaticResource bindings; swapping mid-flight would not re-evaluate them).
///
/// B17: also publishes a SystemThemeChanged event when the OS theme flips
/// at runtime, so App can offer the same I16-A relaunch prompt the Settings
/// dropdown uses. The listener only fires when the user opted into Auto mode.
/// </summary>
public static class ThemeService
{
    private static Theme _currentPreference = Theme.Mocha;
    private static Theme _lastResolved = Theme.Mocha;

    /// <summary>
    /// Raised when the OS theme flips while the user has Theme.Auto selected,
    /// AND the resolved palette would actually change (no false positives on
    /// unrelated `UserPreferenceChanged` categories like Locale or Accessibility).
    /// </summary>
    public static event EventHandler? SystemThemeChanged;

    /// <summary>
    /// Hook the OS user-preference listener. Idempotent — safe to call more
    /// than once but App calls it exactly once at OnStartup after Apply.
    /// </summary>
    public static void StartSystemThemeWatcher(Theme initialPreference)
    {
        _currentPreference = initialPreference;
        _lastResolved = Resolve(initialPreference);
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    public static void StopSystemThemeWatcher()
    {
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
    }

    /// <summary>Update the cached preference when the user picks a new theme in Settings.</summary>
    public static void UpdatePreference(Theme preference)
    {
        _currentPreference = preference;
        _lastResolved = Resolve(preference);
    }

    private static void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        // PreferenceCategory.General fires on app/system theme + accent flips;
        // PreferenceCategory.Accessibility fires on HighContrast toggles.
        if (e.Category != UserPreferenceCategory.General
            && e.Category != UserPreferenceCategory.Accessibility) return;

        // Compare against `_currentPreference` (the user's choice) — Resolve()
        // already factors in OS HighContrast which always wins. So this fires
        // both for Theme.Auto light/dark flips AND for HC-on / HC-off events
        // regardless of which preference the user picked.
        var nowResolved = Resolve(_currentPreference);
        if (nowResolved == _lastResolved) return;
        _lastResolved = nowResolved;
        SystemThemeChanged?.Invoke(null, EventArgs.Empty);
    }

    private const string MochaUri = "pack://application:,,,/Themes/Mocha.xaml";
    private const string LatteUri = "pack://application:,,,/Themes/Latte.xaml";
    private const string HighContrastUri = "pack://application:,,,/Themes/HighContrast.xaml";

    /// <summary>
    /// Resolve a Theme preference (Mocha / Latte / Auto / HighContrast) to the
    /// concrete palette to apply. Auto returns Latte if the OS apps theme is
    /// Light, OR HighContrast if the OS reports HighContrast is on
    /// (which always wins — accessibility users get the right palette even
    /// if they didn't explicitly pick it).
    /// </summary>
    public static Theme Resolve(Theme preference)
    {
        if (System.Windows.SystemParameters.HighContrast) return Theme.HighContrast;
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

        var uri = concrete switch
        {
            Theme.Latte => LatteUri,
            Theme.HighContrast => HighContrastUri,
            _ => MochaUri,
        };
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
