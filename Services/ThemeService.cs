using System.Runtime.InteropServices;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Application = System.Windows.Application;
using WpfButton = System.Windows.Controls.Button;
using WpfCheckBox = System.Windows.Controls.CheckBox;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfControl = System.Windows.Controls.Control;
using WpfCursors = System.Windows.Input.Cursors;
using WpfHorizontalAlignment = System.Windows.HorizontalAlignment;
using WpfLabel = System.Windows.Controls.Label;
using WpfOrientation = System.Windows.Controls.Orientation;
using WpfPanel = System.Windows.Controls.Panel;
using WpfPath = System.Windows.Shapes.Path;
using WpfScrollBar = System.Windows.Controls.Primitives.ScrollBar;
using WpfShape = System.Windows.Shapes.Shape;
using WpfSlider = System.Windows.Controls.Slider;
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
        ApplyControlTree(root, palette, new HashSet<DependencyObject>());
    }

    private static void ApplyControlTree(DependencyObject root, ThemePalette palette, HashSet<DependencyObject> visited)
    {
        var windowBrush = new SolidColorBrush(palette.Window);
        var panelBrush = new SolidColorBrush(palette.Panel);
        var textBrush = new SolidColorBrush(palette.Text);
        var mutedBrush = new SolidColorBrush(palette.MutedText);
        var borderBrush = new SolidColorBrush(palette.Border);

        if (!visited.Add(root))
        {
            return;
        }

        ApplyControl(root, windowBrush, panelBrush, textBrush, mutedBrush, borderBrush, palette);
        if (root is Visual or System.Windows.Media.Media3D.Visual3D)
        {
            for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
            {
                ApplyControlTree(VisualTreeHelper.GetChild(root, index), palette, visited);
            }
        }

        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
        {
            ApplyControlTree(child, palette, visited);
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
                scrollViewer.Resources[typeof(WpfScrollBar)] = CreateScrollBarStyle(palette);
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
                checkBox.Style = CreateCheckBoxStyle(palette);
                break;
            case WpfSlider slider:
                slider.Foreground = new SolidColorBrush(palette.Accent);
                slider.Background = panelBrush;
                slider.BorderBrush = borderBrush;
                break;
            case WpfScrollBar scrollBar:
                scrollBar.Style = CreateScrollBarStyle(palette);
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

    private static Style CreateCheckBoxStyle(ThemePalette palette)
    {
        var windowBrush = new SolidColorBrush(palette.Window);
        var panelBrush = new SolidColorBrush(palette.Panel);
        var textBrush = new SolidColorBrush(palette.Text);
        var borderBrush = new SolidColorBrush(palette.Border);
        var accentBrush = new SolidColorBrush(palette.Accent);
        var checkBrush = new SolidColorBrush(palette.LightTheme ? Colors.White : Colors.Black);
        var hoverBrush = new SolidColorBrush(Blend(palette.Panel, palette.Text, 0.10));

        var template = new ControlTemplate(typeof(WpfCheckBox));
        var root = new FrameworkElementFactory(typeof(StackPanel));
        root.SetValue(StackPanel.OrientationProperty, WpfOrientation.Horizontal);
        root.SetValue(FrameworkElement.MinHeightProperty, 26.0);

        var boxGrid = new FrameworkElementFactory(typeof(Grid));
        boxGrid.SetValue(FrameworkElement.WidthProperty, 26.0);
        boxGrid.SetValue(FrameworkElement.HeightProperty, 26.0);
        boxGrid.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);

        var border = new FrameworkElementFactory(typeof(Border));
        border.Name = "CheckBorder";
        border.SetValue(FrameworkElement.WidthProperty, 18.0);
        border.SetValue(FrameworkElement.HeightProperty, 18.0);
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(2));
        border.SetValue(Border.BorderThicknessProperty, new Thickness(1.4));
        border.SetValue(Border.BackgroundProperty, panelBrush);
        border.SetValue(Border.BorderBrushProperty, borderBrush);
        border.SetValue(FrameworkElement.HorizontalAlignmentProperty, WpfHorizontalAlignment.Left);
        border.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        boxGrid.AppendChild(border);

        var check = new FrameworkElementFactory(typeof(WpfPath));
        check.Name = "CheckMark";
        check.SetValue(WpfPath.DataProperty, Geometry.Parse("M 4 9 L 8 13 L 15 5"));
        check.SetValue(WpfShape.StrokeProperty, checkBrush);
        check.SetValue(WpfShape.StrokeThicknessProperty, 2.4);
        check.SetValue(WpfShape.StrokeStartLineCapProperty, PenLineCap.Round);
        check.SetValue(WpfShape.StrokeEndLineCapProperty, PenLineCap.Round);
        check.SetValue(WpfShape.StrokeLineJoinProperty, PenLineJoin.Round);
        check.SetValue(UIElement.VisibilityProperty, Visibility.Hidden);
        check.SetValue(FrameworkElement.WidthProperty, 18.0);
        check.SetValue(FrameworkElement.HeightProperty, 18.0);
        check.SetValue(FrameworkElement.HorizontalAlignmentProperty, WpfHorizontalAlignment.Left);
        check.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        boxGrid.AppendChild(check);
        root.AppendChild(boxGrid);

        var content = new FrameworkElementFactory(typeof(ContentPresenter));
        content.SetValue(ContentPresenter.MarginProperty, new Thickness(4, 0, 0, 0));
        content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        content.SetValue(ContentPresenter.RecognizesAccessKeyProperty, true);
        root.AppendChild(content);

        template.VisualTree = root;

        var isChecked = new Trigger { Property = WpfCheckBox.IsCheckedProperty, Value = true };
        isChecked.Setters.Add(new Setter(Border.BackgroundProperty, accentBrush, "CheckBorder"));
        isChecked.Setters.Add(new Setter(Border.BorderBrushProperty, accentBrush, "CheckBorder"));
        isChecked.Setters.Add(new Setter(UIElement.VisibilityProperty, Visibility.Visible, "CheckMark"));
        template.Triggers.Add(isChecked);

        var isMouseOver = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        isMouseOver.Setters.Add(new Setter(Border.BorderBrushProperty, accentBrush, "CheckBorder"));
        isMouseOver.Setters.Add(new Setter(Border.BackgroundProperty, hoverBrush, "CheckBorder"));
        template.Triggers.Add(isMouseOver);

        var isDisabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
        isDisabled.Setters.Add(new Setter(UIElement.OpacityProperty, 0.45));
        template.Triggers.Add(isDisabled);

        return new Style(typeof(WpfCheckBox))
        {
            Setters =
            {
                new Setter(WpfControl.ForegroundProperty, textBrush),
                new Setter(WpfControl.BackgroundProperty, windowBrush),
                new Setter(WpfControl.PaddingProperty, new Thickness(0)),
                new Setter(WpfControl.CursorProperty, WpfCursors.Hand),
                new Setter(FrameworkElement.MinWidthProperty, 26.0),
                new Setter(FrameworkElement.MinHeightProperty, 26.0),
                new Setter(WpfControl.TemplateProperty, template),
            },
        };
    }

    private static MediaColor Blend(MediaColor from, MediaColor to, double amount)
    {
        amount = Math.Clamp(amount, 0, 1);
        return MediaColor.FromRgb(
            (byte)(from.R + ((to.R - from.R) * amount)),
            (byte)(from.G + ((to.G - from.G) * amount)),
            (byte)(from.B + ((to.B - from.B) * amount)));
    }

    private static Style CreateScrollBarStyle(ThemePalette palette)
    {
        var panel = ToHex(palette.Panel);
        var border = ToHex(palette.Border);
        var thumb = ToHex(Blend(palette.Panel, palette.Text, palette.LightTheme ? 0.28 : 0.22));
        var hover = ToHex(Blend(palette.Panel, palette.Text, palette.LightTheme ? 0.38 : 0.34));
        var xaml =
            $$"""
            <Style xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                   xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                   TargetType="{x:Type ScrollBar}">
              <Setter Property="Width" Value="14"/>
              <Setter Property="MinWidth" Value="14"/>
              <Setter Property="Background" Value="{{panel}}"/>
              <Setter Property="Template">
                <Setter.Value>
                  <ControlTemplate TargetType="{x:Type ScrollBar}">
                    <Border Background="{{panel}}" BorderBrush="{{border}}" BorderThickness="1,0,0,0">
                      <Track x:Name="PART_Track" IsDirectionReversed="True">
                        <Track.Thumb>
                          <Thumb MinHeight="28" Background="{{thumb}}">
                            <Thumb.Template>
                              <ControlTemplate TargetType="{x:Type Thumb}">
                                <Border x:Name="ThumbBorder"
                                        Margin="2"
                                        CornerRadius="2"
                                        Background="{TemplateBinding Background}"/>
                                <ControlTemplate.Triggers>
                                  <Trigger Property="IsMouseOver" Value="True">
                                    <Setter TargetName="ThumbBorder" Property="Background" Value="{{hover}}"/>
                                  </Trigger>
                                </ControlTemplate.Triggers>
                              </ControlTemplate>
                            </Thumb.Template>
                          </Thumb>
                        </Track.Thumb>
                      </Track>
                    </Border>
                  </ControlTemplate>
                </Setter.Value>
              </Setter>
            </Style>
            """;
        return (Style)XamlReader.Parse(xaml);
    }

    private static string ToHex(MediaColor color)
    {
        return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
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
