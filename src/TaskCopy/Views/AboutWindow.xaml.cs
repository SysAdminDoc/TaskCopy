using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Input;

namespace TaskCopy.Views;

public partial class AboutWindow : Window
{
    private const string RepoUrl = "https://github.com/SysAdminDoc/TaskCopy";

    public AboutWindow()
    {
        InitializeComponent();
        var version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "?";
        VersionText.Text = $"Version {version}";
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
