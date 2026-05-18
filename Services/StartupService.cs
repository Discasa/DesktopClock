using System.Diagnostics;
using System.IO;
using DesktopClock.Models;
using Microsoft.Win32;

namespace DesktopClock.Services;

public static class StartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static void SyncWindowsStartupRegistration(ClockConfig config, string configPath)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        if (key is null)
        {
            return;
        }

        var entryName = string.IsNullOrWhiteSpace(config.WINDOWS_STARTUP_NAME)
            ? "Desktop Clock"
            : config.WINDOWS_STARTUP_NAME.Trim();

        if (config.START_WITH_WINDOWS)
        {
            key.SetValue(entryName, BuildClockCommand(configPath), RegistryValueKind.String);
            return;
        }

        try
        {
            key.DeleteValue(entryName, throwOnMissingValue: false);
        }
        catch
        {
            // The Run key can be locked by policy. The editor should keep running.
        }
    }

    public static string BuildClockCommand(string configPath)
    {
        var exePath = ResolveExecutablePath();
        return $"\"{exePath}\" --clock --config \"{Path.GetFullPath(configPath)}\"";
    }

    public static string ResolveExecutablePath()
    {
        var appHost = Path.Combine(AppContext.BaseDirectory, "Desktop Clock.exe");
        if (File.Exists(appHost))
        {
            return appHost;
        }

        appHost = Path.Combine(AppContext.BaseDirectory, "DesktopClock.exe");
        if (File.Exists(appHost))
        {
            return appHost;
        }

        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(processPath) && File.Exists(processPath))
        {
            return processPath;
        }

        return Process.GetCurrentProcess().MainModule?.FileName ?? "DesktopClock.exe";
    }
}
