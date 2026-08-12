using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Resources;

namespace TaskCopy.Services;

/// <summary>
/// Small localization seam for the WPF surface. The application currently
/// ships an en-US baseline plus an es-ES proof culture; adding a culture is a
/// resource-only change and does not require touching view models.
/// </summary>
public sealed class LocalizationService : INotifyPropertyChanged
{
    private static readonly CultureInfo EnglishCulture = CultureInfo.GetCultureInfo("en-US");
    private static readonly CultureInfo[] Supported =
    [
        EnglishCulture,
        CultureInfo.GetCultureInfo("es-ES"),
    ];

    private readonly ResourceManager _resources = new(
        "TaskCopy.Resources.Strings",
        Assembly.GetExecutingAssembly());

    private CultureInfo _culture;

    private LocalizationService()
    {
        _culture = ResolveInitialCulture();
    }

    public static LocalizationService Instance { get; } = new();

    public static IReadOnlyList<CultureInfo> SupportedCultures => Supported;

    public static CultureInfo CurrentCulture => Instance._culture;

    /// <summary>
    /// Indexer form lets <see cref="LocExtension"/> bind to a resource key and
    /// refresh visible controls after SetCulture changes the active culture.
    /// </summary>
    public string this[string key] => Lookup(key, fallback: key);

    public event PropertyChangedEventHandler? PropertyChanged;

    public static string Get(string key, string? fallback = null)
        => Instance.Lookup(key, fallback ?? key);

    public static bool TrySetCulture(string? name)
    {
        if (!TryResolveSupported(name, out var culture)) return false;
        Instance.ApplyCulture(culture);
        return true;
    }

    public static void SetCulture(CultureInfo culture)
    {
        if (!TryResolveSupported(culture.Name, out var supported))
            throw new ArgumentException($"Unsupported TaskCopy culture: {culture.Name}", nameof(culture));

        Instance.ApplyCulture(supported);
    }

    private void ApplyCulture(CultureInfo culture)
    {
        if (string.Equals(_culture.Name, culture.Name, StringComparison.OrdinalIgnoreCase)) return;
        _culture = culture;

        // Item[] is the binding notification convention for indexers. The
        // empty name covers simple bindings used by future consumers.
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }

    private string Lookup(string key, string fallback)
    {
        if (string.IsNullOrWhiteSpace(key)) return fallback;
        try
        {
            return _resources.GetString(key, _culture) ?? fallback;
        }
        catch (MissingManifestResourceException)
        {
            // A missing satellite resource must never prevent the app from
            // opening; the key is a useful translator/debugging fallback.
            return fallback;
        }
    }

    private static CultureInfo ResolveInitialCulture()
    {
        var overrideName = Environment.GetEnvironmentVariable("TASKCOPY_UI_CULTURE");
        if (TryResolveSupported(overrideName, out var overridden)) return overridden;
        if (TryResolveSupported(CultureInfo.CurrentUICulture.Name, out var system)) return system;
        return EnglishCulture;
    }

    private static bool TryResolveSupported(string? name, out CultureInfo culture)
    {
        culture = EnglishCulture;
        if (string.IsNullOrWhiteSpace(name)) return false;

        var match = Supported.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase)
            || string.Equals(candidate.TwoLetterISOLanguageName, name, StringComparison.OrdinalIgnoreCase));
        if (match is null) return false;
        culture = match;
        return true;
    }
}
