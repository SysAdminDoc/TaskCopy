using System.Diagnostics;
using System.IO;
using System.Windows;

namespace TaskCopy.Services;

public static class CrashLog
{
    private const long MaxLogBytes = 1L * 1024 * 1024;

    public static string LogDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TaskCopy", "logs");

    public static string LogPath { get; } = Path.Combine(LogDirectory, "crash.log");
    public static string RotatedLogPath { get; } = LogPath + ".1";

    public static void Install()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Write("UnhandledException", e.ExceptionObject as Exception);

        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Write("UnobservedTaskException", e.Exception);
            e.SetObserved();
        };

        if (Application.Current is { } app)
        {
            app.DispatcherUnhandledException += (_, e) =>
            {
                Write("DispatcherUnhandledException", e.Exception);
                MessageBox.Show(
                    $"TaskCopy hit an unexpected error and will keep running.\n\n{e.Exception.Message}\n\nDetails written to:\n{LogPath}",
                    "TaskCopy",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                e.Handled = true;
            };
        }
    }

    public static void Write(string source, Exception? ex)
    {
        try
        {
            Directory.CreateDirectory(LogDirectory);
            RotateIfNeeded();
            var entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {source}: {ex}{Environment.NewLine}";
            File.AppendAllText(LogPath, entry);
        }
        catch
        {
            // Last-resort handler — swallow.
        }
    }

    private static void RotateIfNeeded()
    {
        try
        {
            var fi = new FileInfo(LogPath);
            if (!fi.Exists || fi.Length <= MaxLogBytes) return;
            if (File.Exists(RotatedLogPath)) File.Delete(RotatedLogPath);
            File.Move(LogPath, RotatedLogPath);
        }
        catch
        {
            // Rotation failure must not block the actual write.
        }
    }

    public static void OpenFolder()
    {
        try
        {
            Directory.CreateDirectory(LogDirectory);
            Process.Start(new ProcessStartInfo
            {
                FileName = LogDirectory,
                UseShellExecute = true,
            });
        }
        catch
        {
            // best-effort
        }
    }
}
