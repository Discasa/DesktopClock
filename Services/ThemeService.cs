using System.Runtime.InteropServices;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Application = System.Windows.Application;
using WpfButton = System.Windows.Controls.Button;
using WpfCheckBox = System.Windows.Controls.CheckBox;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfLabel = System.Windows.Controls.Label;
using WpfPanel = System.Windows.Controls.Panel;
using WpfTabControl = System.Windows.Controls.TabControl;
using WpfTextBox = System.Windows.Controls.TextBox;
using MediaBrush = System.Windows.Media.Brush;
using MediaColor = System.Windows.Media.Color;

namespace DesktopClock.Services;

public static class ThemeService
{
    private const string PersonalizePath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwaBorderColor = 34;
    private const int DwmwaCaptionColor = 35;
    private const int DwmwaTextColor = 36;
    private const int DwmwcpRound = 2;

    public static bool IsLightTheme()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(PersonalizePath);
        object? value = key?.GetValue("AppsUseLightTheme");
        return value is not int intValue || intValue != 0;
    }

    public static ThemePalette CurrentPalette()
    {
        var light = IsLightTheme();
        return light
            ? new ThemePalette(
                true,
                MediaColor.FromRgb(255, 255, 255),
                MediaColor.FromRgb(246, 246, 246),
                MediaColor.FromRgb(24, 24, 24),
                MediaColor.FromRgb(96, 96, 96),
                MediaColor.FromRgb(218, 218, 218),
                MediaColor.FromRgb(0, 120, 212))
            : new ThemePalette(
                false,
                MediaColor.FromRgb(32, 32, 32),
                MediaColor.FromRgb(43, 43, 43),
                MediaColor.FromRgb(245, 245, 245),
                MediaColor.FromRgb(190, 190, 190),
                MediaColor.FromRgb(72, 72, 72),
                MediaColor.FromRgb(77, 163, 255));
    }

    public static ImageSource? LoadAppIcon(bool lightTheme)
    {
        var iconName = lightTheme ? "app-dark.ico" : "app-light.ico";
        var iconPath = Path.Combine(ConfigService.RootDirectory, "Assets", "Icons", iconName);
        if (!File.Exists(iconPath))
        {
            iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Icons", iconName);
        }

        if (!File.Exists(iconPath))
        {
            var resourceUri = new Uri($"pack://application:,,,/Assets/Icons/{iconName}", UriKind.Absolute);
            var resource = Application.GetResourceStream(resourceUri);
            if (resource is null)
            {
                return null;
            }

            using var stream = resource.Stream;
            return BitmapFrame.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        }

        return BitmapFrame.Create(new Uri(iconPath), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
    }

    public static void ApplyWindowFrame(Window window, ThemePalette palette)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763))
        {
            return;
        }

        var hwnd = new System.Windows.Interop.WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        var useDarkMode = palette.LightTheme ? 0 : 1;
        DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref useDarkMode, sizeof(int));

        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
        {
            return;
        }

        var cornerPreference = DwmwcpRound;
        var captionColor = ToColorRef(palette.Window);
        var textColor = ToColorRef(palette.Text);
        var borderColor = ToColorRef(palette.Border);
        DwmSetWindowAttribute(hwnd, DwmwaWindowCornerPreference, ref cornerPreference, sizeof(int));
        DwmSetWindowAttribute(hwnd, DwmwaCaptionColor, ref captionColor, sizeof(int));
        DwmSetWindowAttribute(hwnd, DwmwaTextColor, ref textColor, sizeof(int));
        DwmSetWindowAttribute(hwnd, DwmwaBorderColor, ref borderColor, sizeof(int));
    }

    public static void ApplyControlTree(DependencyObject root, ThemePalette palette)
    {
        var windowBrush = new SolidColorBrush(palette.Window);
        var panelBrush = new SolidColorBrush(palette.Panel);
        var textBrush = new SolidColorBrush(palette.Text);
        var mutedBrush = new SolidColorBrush(palette.MutedText);
        var borderBrush = new SolidColorBrush(palette.Border);

        ApplyControl(root, windowBrush, panelBrush, textBrush, mutedBrush, borderBrush, palette);
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            ApplyControlTree(VisualTreeHelper.GetChild(root, index), palette);
        }
    }

    private static void ApplyControl(
        DependencyObject control,
        MediaBrush windowBrush,
        MediaBrush panelBrush,
        MediaBrush textBrush,
        MediaBrush mutedBrush,
        MediaBrush borderBrush,
        ThemePalette palette)
    {
        switch (control)
        {
            case Window window:
                window.Background = windowBrush;
                window.Foreground = textBrush;
                window.Icon = LoadAppIcon(palette.LightTheme);
                break;
            case ScrollViewer scrollViewer:
                scrollViewer.Background = windowBrush;
                break;
            case WpfPanel panel:
                panel.Background ??= windowBrush;
                if (panel is DockPanel or StackPanel or Grid)
                {
                    panel.Background = windowBrush;
                }
                break;
            case TextBlock text:
                text.Foreground = text.Text.Contains("Previa", StringComparison.OrdinalIgnoreCase) ? mutedBrush : textBrush;
                break;
            case WpfLabel label:
                label.Foreground = textBrush;
                label.Background = windowBrush;
                break;
            case WpfTextBox textBox:
                textBox.Foreground = textBrush;
                textBox.Background = panelBrush;
                textBox.BorderBrush = borderBrush;
                break;
            case WpfComboBox comboBox:
                comboBox.Foreground = textBrush;
                comboBox.Background = panelBrush;
                comboBox.BorderBrush = borderBrush;
                break;
            case WpfButton button when button.Tag is string tag && tag.StartsWith('#'):
                break;
            case WpfButton button:
                button.Foreground = textBrush;
                button.Background = panelBrush;
                button.BorderBrush = borderBrush;
                break;
            case WpfCheckBox checkBox:
                checkBox.Foreground = textBrush;
                checkBox.Background = windowBrush;
                break;
            case WpfTabControl tabControl:
                tabControl.Background = windowBrush;
                tabControl.BorderBrush = borderBrush;
                break;
            case TabItem tabItem:
                tabItem.Foreground = textBrush;
                tabItem.Background = panelBrush;
                tabItem.BorderBrush = borderBrush;
                break;
            case Border border:
                border.Background = panelBrush;
                border.BorderBrush = borderBrush;
                break;
        }
    }

    private static int ToColorRef(MediaColor color)
    {
        return color.R | (color.G << 8) | (color.B << 16);
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int attributeValue, int attributeSize);
}

public sealed record ThemePalette(
    bool LightTheme,
    MediaColor Window,
    MediaColor Panel,
    MediaColor Text,
    MediaColor MutedText,
    MediaColor Border,
    MediaColor Accent);
