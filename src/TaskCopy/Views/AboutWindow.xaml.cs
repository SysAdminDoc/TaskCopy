using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Input;

namespace TaskCopy.Views;

public partial class AboutWindow : Window
{
    private const string RepoUrl = "https://github.com/SysAdminDoc/TaskCopy";

    public AboutWindow() : this(null) { }

    /// <summary>
    /// F37: when a SettingsStore is supplied, the dialog also surfaces a
    /// usage-stats line (pastes + characters typed for you). Null-safe so
    /// older call sites that constructed AboutWindow without arguments still
    /// work — those just see version + repo + license.
    /// </summary>
    public AboutWindow(Data.SettingsStore? settings)
    {
        InitializeComponent();
        var version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "?";
        VersionText.Text = $"Version {version}";

        if (settings is not null)
        {
            try
            {
                var pastes = settings.StatsTotalPastes;
                var chars = settings.StatsTotalChars;
                if (pastes > 0)
                {
                    // Rough "time saved" estimate: average typing ≈ 5 chars/sec,
                    // so chars / 5 = seconds. Keep it informational, not promised.
                    var minutesSaved = (chars / 5.0) / 60.0;
                    UsageStatsText.Text = minutesSaved >= 1.0
                        ? $"You've pasted {pastes:N0} snippet{(pastes == 1 ? "" : "s")} — about {minutesSaved:N0} minute{((int)minutesSaved == 1 ? "" : "s")} of typing TaskCopy did for you."
                        : $"You've pasted {pastes:N0} snippet{(pastes == 1 ? "" : "s")} ({chars:N0} characters).";
                }
                else
                {
                    UsageStatsText.Text = "No pastes yet — press your hotkey to open the picker.";
                }
            }
            catch { /* stats are decorative; never block the About surface */ }
        }
    }

    private void OnOpenRepo(object sender, MouseButtonEventArgs e) => OpenUrl(RepoUrl);

    private void OnOpenLicense(object sender, MouseButtonEventArgs e)
    {
        // Single-file publish makes Assembly.Location return empty (IL3000).
        // AppContext.BaseDirectory points at the exe's directory whether the
        // build is single-file or classic — same lookup, single-file safe.
        var dir = AppContext.BaseDirectory;
        var local = string.IsNullOrEmpty(dir) ? null : Path.Combine(dir, "LICENSE");
        if (!string.IsNullOrEmpty(local) && File.Exists(local))
        {
            OpenUrl(local);
            return;
        }
        OpenUrl($"{RepoUrl}/blob/master/LICENSE");
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true,
            });
        }
        catch
        {
            // best-effort
        }
    }
}
