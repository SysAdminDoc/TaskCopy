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
        var exe = Assembly.GetEntryAssembly()?.Location;
        var dir = string.IsNullOrEmpty(exe) ? null : Path.GetDirectoryName(exe);
        var local = dir is null ? null : Path.Combine(dir, "LICENSE");
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
