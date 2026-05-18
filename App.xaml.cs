using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using DesktopClock.Clock;
using DesktopClock.Editor;
using DesktopClock.Services;
using Application = System.Windows.Application;
using WpfMessageBox = System.Windows.MessageBox;

namespace DesktopClock;

public partial class App : Application
{
    private AppUpdateService? _updateService;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnLastWindowClose;
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        try
        {
            var args = AppArguments.Parse(e.Args);
            var configPath = args.ConfigPath ?? ConfigService.DefaultConfigPath;

            if (args.NormalizeConfig)
            {
                var config = ConfigService.LoadConfig(configPath);
                ConfigService.SaveConfig(configPath, config, includePreview: false);
                Shutdown(0);
                return;
            }

            Window window = args.EditorMode
                ? new EditorWindow(configPath)
                : new ClockWindow(configPath, args.PreviewMode);
            window.Show();

            if (!args.PreviewMode)
            {
                _updateService = new AppUpdateService();
                _updateService.UpdateInstallerReady += UpdateService_UpdateInstallerReady;
                _updateService.Start();
            }
        }
        catch (Exception ex)
        {
            ConfigService.LogError(ex);
            WpfMessageBox.Show(ex.Message, "DesktopClock", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        ConfigService.LogError(e.Exception);
        WpfMessageBox.Show(e.Exception.Message, "DesktopClock", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_updateService is not null)
        {
            _updateService.UpdateInstallerReady -= UpdateService_UpdateInstallerReady;
            _updateService.Dispose();
        }

        base.OnExit(e);
    }

    private void UpdateService_UpdateInstallerReady(object? sender, UpdateInstallerReadyEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            try
            {
                Process.Start(new ProcessStartInfo(e.InstallerPath)
                {
                    UseShellExecute = false,
                    WorkingDirectory = Path.GetTempPath(),
                    ArgumentList = { "--silent", "--from-update" },
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true,
                });
                Shutdown(0);
            }
            catch (Exception ex)
            {
                ConfigService.LogError(ex);
            }
        });
    }

    private sealed class AppArguments
    {
        public bool EditorMode { get; private init; }
        public bool PreviewMode { get; private init; }
        public bool NormalizeConfig { get; private init; }
        public string? ConfigPath { get; private init; }

        public static AppArguments Parse(string[] args)
        {
            var editor = false;
            var preview = false;
            var normalize = false;
            string? configPath = null;

            for (var index = 0; index < args.Length; index++)
            {
                var arg = args[index];
                if (string.Equals(arg, "--editor", StringComparison.OrdinalIgnoreCase))
                {
                    editor = true;
                }
                else if (string.Equals(arg, "--clock", StringComparison.OrdinalIgnoreCase))
                {
                    editor = false;
                }
                else if (string.Equals(arg, "--preview", StringComparison.OrdinalIgnoreCase))
                {
                    preview = true;
                }
                else if (string.Equals(arg, "--normalize-config", StringComparison.OrdinalIgnoreCase))
                {
                    normalize = true;
                }
                else if (string.Equals(arg, "--config", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
                {
                    configPath = args[++index];
                }
                else if (arg.StartsWith("--config=", StringComparison.OrdinalIgnoreCase))
                {
                    configPath = arg["--config=".Length..].Trim('"');
                }
            }

            return new AppArguments
            {
                EditorMode = editor,
                PreviewMode = preview,
                NormalizeConfig = normalize,
                ConfigPath = configPath,
            };
        }
    }
}
