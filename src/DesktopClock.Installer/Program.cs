using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using DesktopClock.SetupUi;

namespace DesktopClock.Installer;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        Directory.SetCurrentDirectory(Path.GetTempPath());
        ApplicationConfiguration.Initialize();
        var options = InstallerOptions.Parse(args);
        if (options.Silent)
        {
            return InstallerForm.RunSilent(options);
        }

        Application.Run(new InstallerForm(options));
        return 0;
    }
}

internal sealed record InstallerOptions(bool Silent, bool FromUpdate)
{
    public static InstallerOptions Parse(string[] args)
    {
        var silent = args.Any(arg => string.Equals(arg, "--silent", StringComparison.OrdinalIgnoreCase));
        var fromUpdate = args.Any(arg => string.Equals(arg, "--from-update", StringComparison.OrdinalIgnoreCase));
        return new InstallerOptions(silent, fromUpdate);
    }
}

internal sealed class InstallerForm : Form
{
    private const string AppName = "Desktop Clock";
    private const string AppExeName = "Desktop Clock.exe";
    private const string UninstallerName = "Desktop Clock Uninstaller.exe";
    private const string AppIconName = "Desktop Clock.ico";
    private const string ConfigName = "desktop-image-clock.json";
    private const string AppVersion = "1.0.2";
    private const string Publisher = "anderson";
    private const string UninstallRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\Desktop Clock";
    private const string StartupRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    private readonly string _language = SetupStrings.DetectLanguage();
    private readonly string _installDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        AppName);
    private readonly Label _titleLabel = new();
    private readonly Label _bodyLabel = new();
    private readonly Label _locationLabel = new();
    private readonly Label _statusLabel = new();
    private readonly SetupFieldPanel _locationPanel = new();
    private readonly SetupProgressBar _progressBar = new();
    private readonly SetupButton _installButton = new();
    private readonly SetupButton _cancelButton = new();
    private bool _installComplete;
    private bool _installing;

    public InstallerForm(InstallerOptions _)
    {
        Text = T("InstallerTitleBar");
        var icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        if (icon is not null)
        {
            Icon = icon;
        }

        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(560, 360);
        var fixedSize = Size;
        MinimumSize = fixedSize;
        MaximumSize = fixedSize;
        MaximizeBox = false;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        BuildUi();
        ApplyTheme();
    }

    public static int RunSilent(InstallerOptions options)
    {
        try
        {
            using var form = new InstallerForm(options);
            form.Install(new Progress<InstallProgress>());
            return 0;
        }
        catch (Exception ex)
        {
            LogInstallError(ex);
            return 1;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ApplyTheme();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_installing)
        {
            e.Cancel = true;
        }

        base.OnFormClosing(e);
    }

    private void BuildUi()
    {
        _titleLabel.Text = T("InstallTitle");
        _titleLabel.Font = new Font("Segoe UI", 18F, FontStyle.Regular, GraphicsUnit.Point);
        _titleLabel.Location = new Point(28, 28);
        _titleLabel.Size = new Size(504, 36);
        _titleLabel.TextAlign = ContentAlignment.MiddleLeft;

        _bodyLabel.Text = T("InstallBody");
        _bodyLabel.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        _bodyLabel.Location = new Point(30, 72);
        _bodyLabel.Size = new Size(500, 42);
        _bodyLabel.AutoEllipsis = true;

        var locationTitle = new Label
        {
            Text = T("InstallLocation"),
            Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point),
            Dock = DockStyle.Top,
            Height = 20
        };
        _locationLabel.Text = _installDir;
        _locationLabel.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        _locationLabel.Dock = DockStyle.Fill;
        _locationLabel.AutoEllipsis = true;
        _locationPanel.Location = new Point(28, 134);
        _locationPanel.Size = new Size(504, 58);
        _locationPanel.Controls.Add(_locationLabel);
        _locationPanel.Controls.Add(locationTitle);

        _progressBar.Location = new Point(28, 218);
        _progressBar.Size = new Size(504, 8);
        _progressBar.ProgressValue = 0;
        _progressBar.Visible = false;

        _statusLabel.Text = T("ReadyToInstall");
        _statusLabel.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        _statusLabel.Location = new Point(30, 238);
        _statusLabel.Size = new Size(500, 34);

        _installButton.Text = T("InstallButton");
        _installButton.Location = new Point(422, 304);
        _installButton.Size = new Size(110, 36);
        _installButton.Click += InstallButton_Click;
        _cancelButton.Text = T("Cancel");
        _cancelButton.Location = new Point(300, 304);
        _cancelButton.Size = new Size(110, 36);
        _cancelButton.Click += (_, _) => Close();

        var separator = new Panel
        {
            Location = new Point(28, 288),
            Size = new Size(504, 1)
        };
        separator.Paint += (_, e) =>
        {
            using var pen = new Pen(_locationPanel.BorderColor);
            e.Graphics.DrawLine(pen, 0, 0, separator.Width, 0);
        };

        Controls.AddRange([
            _titleLabel,
            _bodyLabel,
            _locationPanel,
            _progressBar,
            _statusLabel,
            separator,
            _cancelButton,
            _installButton
        ]);
    }

    private async void InstallButton_Click(object? sender, EventArgs e)
    {
        if (_installComplete)
        {
            Close();
            return;
        }

        _installing = true;
        _installButton.Enabled = false;
        _cancelButton.Enabled = false;
        _progressBar.Visible = true;
        _titleLabel.Text = T("InstallingTitle");
        _bodyLabel.Text = T("InstallingBody");

        var progress = new Progress<InstallProgress>(UpdateProgress);

        try
        {
            await Task.Run(() => Install(progress));
            UpdateProgress(new InstallProgress(100, T("InstalledStatus")));
            _titleLabel.Text = T("InstalledTitle");
            _bodyLabel.Text = T("InstalledBody");
            _installComplete = true;
            _installButton.Text = T("Finish");
            _installButton.Enabled = true;
            _cancelButton.Visible = false;
        }
        catch (Exception ex)
        {
            _titleLabel.Text = T("InstallFailedTitle");
            _bodyLabel.Text = ex.Message;
            _statusLabel.Text = T("SetupCouldNotComplete");
            _installButton.Text = T("Close");
            _installComplete = true;
            _installButton.Enabled = true;
            _cancelButton.Visible = false;
            LogInstallError(ex);
        }
        finally
        {
            _installing = false;
        }
    }

    private void UpdateProgress(InstallProgress progress)
    {
        _progressBar.ProgressValue = progress.Percent;
        _statusLabel.Text = progress.Message;
    }

    private void Install(IProgress<InstallProgress> progress)
    {
        var appExe = Path.Combine(_installDir, AppExeName);
        var uninstallerExe = Path.Combine(_installDir, UninstallerName);
        var appIcon = Path.Combine(_installDir, AppIconName);
        var configPath = Path.Combine(_installDir, ConfigName);

        progress.Report(new InstallProgress(5, T("PreparingInstall")));
        StopKnownProcesses();

        progress.Report(new InstallProgress(18, T("RemovingStartup")));
        DeleteStartupEntries();

        progress.Report(new InstallProgress(32, T("CreatingFolder")));
        var preservedConfig = PreserveConfig(configPath);
        PrepareInstallDirectory(_installDir);

        progress.Report(new InstallProgress(52, T("CopyingFiles")));
        InstallPayload(_installDir);
        RestoreConfig(preservedConfig, configPath);
        if (!File.Exists(appExe))
        {
            throw new FileNotFoundException(T("AppNotCopied"), appExe);
        }

        progress.Report(new InstallProgress(68, T("CreatingShortcuts")));
        CreateShortcuts(appExe, uninstallerExe, appIcon, configPath);

        progress.Report(new InstallProgress(78, T("RegisteringInstalledApps")));
        RegisterInstalledApp(_installDir, appExe, uninstallerExe, appIcon);

        progress.Report(new InstallProgress(90, T("ConfiguringStartup")));
        if (string.Equals(Environment.GetEnvironmentVariable("DESKTOPCLOCK_SKIP_STARTUP"), "1", StringComparison.Ordinal))
        {
            progress.Report(new InstallProgress(90, T("SkippingStartup")));
        }
        else
        {
            CreateStartupEntry(appExe, configPath);
        }

        progress.Report(new InstallProgress(96, T("StartingApp")));
        if (string.Equals(Environment.GetEnvironmentVariable("DESKTOPCLOCK_SKIP_LAUNCH"), "1", StringComparison.Ordinal))
        {
            return;
        }

        Process.Start(new ProcessStartInfo(appExe)
        {
            UseShellExecute = true,
            ArgumentList = { "--clock", "--config", configPath }
        });
    }

    private string T(string key) => SetupStrings.Get(_language, key);

    private static string? PreserveConfig(string configPath)
    {
        if (!File.Exists(configPath))
        {
            return null;
        }

        var tempConfig = Path.Combine(Path.GetTempPath(), $"DesktopClock-config-{Guid.NewGuid():N}.json");
        File.Copy(configPath, tempConfig, overwrite: true);
        return tempConfig;
    }

    private static void RestoreConfig(string? preservedConfig, string configPath)
    {
        if (preservedConfig is not null && File.Exists(preservedConfig))
        {
            File.Copy(preservedConfig, configPath, overwrite: true);
            File.Delete(preservedConfig);
        }
    }

    private static void StopKnownProcesses()
    {
        foreach (var processName in new[] { "Desktop Clock", "DesktopClock" })
        {
            foreach (var process in Process.GetProcessesByName(processName))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(5000);
                }
                catch
                {
                }
            }
        }
    }

    private static void PrepareInstallDirectory(string installDir)
    {
        var localAppData = Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
        var resolvedInstallDir = Path.GetFullPath(installDir);
        if (!resolvedInstallDir.StartsWith(localAppData, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Refusing to clean unexpected folder: {resolvedInstallDir}");
        }

        if (Directory.Exists(resolvedInstallDir))
        {
            DeleteDirectoryWithRetries(resolvedInstallDir);
        }

        Directory.CreateDirectory(resolvedInstallDir);
    }

    private static void DeleteDirectoryWithRetries(string directory)
    {
        Exception? lastError = null;
        for (var attempt = 0; attempt < 30; attempt++)
        {
            try
            {
                Directory.Delete(directory, recursive: true);
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                lastError = ex;
                Thread.Sleep(1000);
            }
        }

        throw new IOException($"Could not remove the previous installation folder: {directory}", lastError);
    }

    private static void InstallPayload(string installDir)
    {
        var resourceNames = Assembly.GetExecutingAssembly()
            .GetManifestResourceNames()
            .Where(name => name.StartsWith("Payload.", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (resourceNames.Length == 0)
        {
            throw new DirectoryNotFoundException("Embedded installer payload not found.");
        }

        foreach (var resourceName in resourceNames)
        {
            var fileName = resourceName["Payload.".Length..];
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
            if (stream is null)
            {
                throw new InvalidOperationException($"Embedded payload not found: {resourceName}");
            }

            using var target = File.Create(Path.Combine(installDir, fileName));
            stream.CopyTo(target);
        }
    }

    private static void CreateShortcuts(string appExe, string uninstallerExe, string appIcon, string configPath)
    {
        var programs = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
        var startMenuFolder = Path.Combine(programs, AppName);
        Directory.CreateDirectory(startMenuFolder);
        DeleteOldRootShortcuts(programs);

        var iconLocation = File.Exists(appIcon) ? appIcon : $"{appExe},0";
        CreateShortcut(Path.Combine(startMenuFolder, $"{AppName}.lnk"), appExe, AppName, iconLocation, $"--clock --config \"{configPath}\"");
        CreateShortcut(Path.Combine(startMenuFolder, $"{AppName} Editor.lnk"), appExe, $"{AppName} Editor", iconLocation, $"--editor --config \"{configPath}\"");

        if (File.Exists(uninstallerExe))
        {
            CreateShortcut(Path.Combine(startMenuFolder, $"Uninstall {AppName}.lnk"), uninstallerExe, $"Uninstall {AppName}", iconLocation, "");
        }
    }

    private static void DeleteOldRootShortcuts(string programs)
    {
        foreach (var shortcut in new[]
        {
            Path.Combine(programs, $"{AppName}.lnk"),
            Path.Combine(programs, $"{AppName} Editor.lnk"),
            Path.Combine(programs, $"Uninstall {AppName}.lnk")
        })
        {
            if (File.Exists(shortcut))
            {
                File.Delete(shortcut);
            }
        }
    }

    private static void RegisterInstalledApp(string installDir, string appExe, string uninstallerExe, string appIcon)
    {
        using var key = Registry.CurrentUser.CreateSubKey(UninstallRegistryPath);
        key.SetValue("DisplayName", AppName);
        key.SetValue("DisplayVersion", AppVersion);
        key.SetValue("Publisher", Publisher);
        key.SetValue("InstallLocation", installDir);
        key.SetValue("DisplayIcon", $"\"{appExe}\",0");
        key.SetValue("UninstallString", $"\"{uninstallerExe}\"");
        key.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        key.SetValue("EstimatedSize", EstimateInstallSizeKb(installDir), RegistryValueKind.DWord);
    }

    private static int EstimateInstallSizeKb(string installDir)
    {
        var bytes = Directory.EnumerateFiles(installDir, "*", SearchOption.AllDirectories)
            .Sum(file => new FileInfo(file).Length);
        return Math.Max(1, (int)Math.Ceiling(bytes / 1024.0));
    }

    private static void CreateShortcut(string shortcutPath, string targetPath, string description, string iconLocation, string arguments)
    {
        var shellType = Type.GetTypeFromProgID("WScript.Shell") ?? throw new InvalidOperationException("Could not access WScript.Shell.");
        dynamic shell = Activator.CreateInstance(shellType)!;
        dynamic shortcut = shell.CreateShortcut(shortcutPath);
        shortcut.TargetPath = targetPath;
        shortcut.Arguments = arguments;
        shortcut.WorkingDirectory = Path.GetDirectoryName(targetPath);
        shortcut.IconLocation = iconLocation;
        shortcut.Description = description;
        shortcut.Save();
    }

    private static void CreateStartupEntry(string appExe, string configPath)
    {
        using var key = Registry.CurrentUser.CreateSubKey(StartupRegistryPath);
        key.SetValue(AppName, $"\"{appExe}\" --clock --config \"{configPath}\"");
    }

    private static void DeleteStartupEntries()
    {
        using var key = Registry.CurrentUser.OpenSubKey(StartupRegistryPath, writable: true);
        key?.DeleteValue(AppName, throwOnMissingValue: false);
        key?.DeleteValue("DesktopImageClock", throwOnMissingValue: false);
        key?.DeleteValue("DesktopClock", throwOnMissingValue: false);
        DeleteTask(AppName);
        DeleteTask("DesktopImageClock");
    }

    private static void DeleteTask(string taskName)
    {
        RunSchtasks(["/end", "/tn", taskName], throwOnError: false);
        RunSchtasks(["/delete", "/tn", taskName, "/f"], throwOnError: false);
    }

    private static void RunSchtasks(string[] arguments, bool throwOnError)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo("schtasks.exe")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            }
        };

        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.Start();
        process.WaitForExit();

        if (throwOnError && process.ExitCode != 0)
        {
            throw new InvalidOperationException($"schtasks failed with exit code {process.ExitCode}.");
        }
    }

    private static void LogInstallError(Exception exception)
    {
        try
        {
            var logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName);
            Directory.CreateDirectory(logDir);
            File.AppendAllText(Path.Combine(logDir, "install.log"), $"[{DateTimeOffset.Now:O}] {exception}{Environment.NewLine}");
        }
        catch
        {
        }
    }

    private void ApplyTheme()
    {
        var lightTheme = IsLightTheme();
        if (IsHandleCreated)
        {
            ApplyWindowFrame(this, lightTheme);
        }

        var back = lightTheme ? Color.White : Color.FromArgb(32, 32, 32);
        var fore = lightTheme ? Color.FromArgb(24, 24, 24) : Color.White;
        var panel = lightTheme ? Color.FromArgb(246, 246, 246) : Color.FromArgb(43, 43, 43);
        var border = lightTheme ? Color.FromArgb(218, 218, 218) : Color.FromArgb(72, 72, 72);

        BackColor = back;
        ForeColor = fore;
        _locationPanel.FillColor = panel;
        _locationPanel.BorderColor = border;
        _progressBar.TrackColor = lightTheme ? Color.FromArgb(226, 226, 226) : Color.FromArgb(58, 58, 58);
        _progressBar.ProgressColor = Color.FromArgb(0, 120, 212);

        foreach (var control in Controls.Cast<Control>().SelectMany(FlattenControls))
        {
            control.ForeColor = fore;
            if (control.Parent is SetupFieldPanel parentFieldPanel)
            {
                control.BackColor = parentFieldPanel.FillColor;
            }
            else if (control is SetupFieldPanel fieldPanel)
            {
                fieldPanel.BackColor = back;
                fieldPanel.FillColor = panel;
                fieldPanel.BorderColor = border;
                fieldPanel.Invalidate();
            }
            else if (control is SetupProgressBar progressBar)
            {
                progressBar.BackColor = back;
                progressBar.TrackColor = lightTheme ? Color.FromArgb(226, 226, 226) : Color.FromArgb(58, 58, 58);
                progressBar.ProgressColor = Color.FromArgb(0, 120, 212);
            }
            else if (control is SetupButton setupButton)
            {
                setupButton.BackColor = lightTheme ? Color.FromArgb(252, 252, 252) : Color.FromArgb(37, 37, 37);
                setupButton.BorderColor = border;
                setupButton.HoverBackColor = lightTheme ? Color.FromArgb(242, 242, 242) : Color.FromArgb(50, 50, 50);
                setupButton.PressedBackColor = lightTheme ? Color.FromArgb(235, 235, 235) : Color.FromArgb(58, 58, 58);
                setupButton.ForeColor = fore;
                setupButton.Invalidate();
            }
            else if (control is Panel)
            {
                control.BackColor = back;
            }
            else
            {
                control.BackColor = back;
            }
        }
    }

    private static IEnumerable<Control> FlattenControls(Control root)
    {
        yield return root;
        foreach (Control child in root.Controls)
        {
            foreach (var nested in FlattenControls(child))
            {
                yield return nested;
            }
        }
    }

    private static bool IsLightTheme()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        var value = key?.GetValue("AppsUseLightTheme");
        return value is not int intValue || intValue != 0;
    }

    private static void ApplyWindowFrame(Form form, bool lightTheme)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763))
        {
            return;
        }

        var useDarkMode = lightTheme ? 0 : 1;
        DwmSetWindowAttribute(form.Handle, 20, ref useDarkMode, sizeof(int));

        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
        {
            return;
        }

        var cornerPreference = 2;
        var captionColor = ToColorRef(lightTheme ? Color.White : Color.FromArgb(32, 32, 32));
        var textColor = ToColorRef(lightTheme ? Color.FromArgb(24, 24, 24) : Color.White);
        var borderColor = ToColorRef(lightTheme ? Color.FromArgb(208, 208, 208) : Color.FromArgb(64, 64, 64));
        DwmSetWindowAttribute(form.Handle, 33, ref cornerPreference, sizeof(int));
        DwmSetWindowAttribute(form.Handle, 35, ref captionColor, sizeof(int));
        DwmSetWindowAttribute(form.Handle, 36, ref textColor, sizeof(int));
        DwmSetWindowAttribute(form.Handle, 34, ref borderColor, sizeof(int));
    }

    private static int ToColorRef(Color color) => color.R | (color.G << 8) | (color.B << 16);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int attributeValue, int attributeSize);
}

internal sealed record InstallProgress(int Percent, string Message);
