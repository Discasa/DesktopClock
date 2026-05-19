using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using DesktopClock.Models;
using DesktopClock.Services;
using Forms = System.Windows.Forms;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using ComboBox = System.Windows.Controls.ComboBox;
using MediaBrushes = System.Windows.Media.Brushes;
using Slider = System.Windows.Controls.Slider;
using TabControl = System.Windows.Controls.TabControl;
using TextBlock = System.Windows.Controls.TextBlock;
using TextBox = System.Windows.Controls.TextBox;
using WpfCursors = System.Windows.Input.Cursors;
using WpfHorizontalAlignment = System.Windows.HorizontalAlignment;

namespace DesktopClock.Editor;

public sealed class EditorWindow : Window
{
    private const double LabelWidth = 155;
    private const double ApplyColumnWidth = 58;

    private readonly string _configPath;
    private readonly string _previewPath;
    private readonly ClockConfig _defaults = ClockConfig.CreateDefault();
    private readonly DispatcherTimer _previewTimer = new() { Interval = TimeSpan.FromMilliseconds(80) };
    private readonly Dictionary<string, CheckBox> _applyAll = new();
    private readonly string[] _fonts;

    private ClockConfig _config;
    private string _selectedSlot = ClockConfig.SlotIds[0];
    private bool _loading;
    private Process? _previewProcess;

    private TextBox _centerX = null!;
    private TextBox _centerY = null!;
    private TextBox _screenIndex = null!;
    private ComboBox _layoutMode = null!;
    private CheckBox _showSeconds = null!;
    private CheckBox _showSeparators = null!;
    private ComboBox _hourMode = null!;
    private TextBox _colonCharacter = null!;
    private TextBox _digitSpacing = null!;
    private TextBox _separatorSpacing = null!;
    private TextBox _groupSpacing = null!;
    private TextBox _verticalSpacing = null!;
    private TextBox _columnWidth = null!;
    private ComboBox _verticalAlign = null!;
    private ComboBox _animationEasing = null!;
    private TextBox _updateInterval = null!;
    private TextBox _fontOutlineWidth = null!;
    private TextBox _fontPaddingX = null!;
    private TextBox _fontPaddingY = null!;
    private TextBox _fontOffsetX = null!;
    private TextBox _fontOffsetY = null!;
    private Slider _windowOpacity = null!;
    private TextBlock _windowOpacityValue = null!;
    private CheckBox _clickThrough = null!;
    private CheckBox _alwaysOnTop = null!;
    private CheckBox _alwaysOnBottom = null!;
    private CheckBox _showInTaskbar = null!;
    private CheckBox _startWithWindows = null!;

    private TextBlock _selectedLabel = null!;
    private ComboBox _slotFont = null!;
    private TextBox _slotSize = null!;
    private CheckBox _slotBold = null!;
    private CheckBox _slotItalic = null!;
    private Button _slotColor = null!;
    private ComboBox _slotRenderMode = null!;
    private Slider _slotOpacity = null!;
    private TextBlock _slotOpacityValue = null!;
    private TextBox _slotAnimationDuration = null!;
    private TextBox _slotWidth = null!;
    private TextBox _slotHeight = null!;
    private TextBox _slotTextX = null!;
    private TextBox _slotTextY = null!;
    private TextBox _slotPosX = null!;
    private TextBox _slotPosY = null!;
    private TextBlock _status = null!;

    public EditorWindow(string configPath)
    {
        _configPath = Path.GetFullPath(configPath);
        _previewPath = ConfigService.PreviewConfigPath;
        _config = ConfigService.LoadConfig(_configPath);
        _fonts = Fonts.SystemFontFamilies.Select(font => font.Source).Distinct().OrderBy(x => x).ToArray();

        Title = "Desktop Clock Editor";
        Width = 520;
        Height = 800;
        MinWidth = 460;
        MinHeight = 620;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Icon = ThemeService.LoadAppIcon(ThemeService.IsLightTheme());

        Content = BuildUi();
        _previewTimer.Tick += (_, _) =>
        {
            _previewTimer.Stop();
            UpdateLivePreview();
        };

        Loaded += (_, _) =>
        {
            ApplyTheme();
            RefreshAllControls();
            ScheduleLivePreview();
        };
        SourceInitialized += (_, _) => ApplyTheme();
        Microsoft.Win32.SystemEvents.UserPreferenceChanged += SystemEvents_UserPreferenceChanged;
        Closing += (_, _) => StopPreview();
        Closed += (_, _) =>
        {
            Microsoft.Win32.SystemEvents.UserPreferenceChanged -= SystemEvents_UserPreferenceChanged;
            DeletePreviewConfig();
        };
    }

    private UIElement BuildUi()
    {
        var root = new DockPanel { Margin = new Thickness(10) };
        var footer = BuildFooter();
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);

        var tabs = new TabControl();
        tabs.SelectionChanged += (_, _) => Dispatcher.BeginInvoke(ApplyTheme);
        root.Children.Add(tabs);

        var generalPanel = new StackPanel();
        tabs.Items.Add(new TabItem
        {
            Header = "Geral",
            Content = new ScrollViewer { Content = generalPanel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto },
        });
        BuildGeneralTab(generalPanel);

        var itemPanel = new StackPanel();
        tabs.Items.Add(new TabItem
        {
            Header = "Item",
            Content = new ScrollViewer { Content = itemPanel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto },
        });
        BuildItemTab(itemPanel);

        return root;
    }

    private UIElement BuildFooter()
    {
        var grid = new Grid { Margin = new Thickness(0, 10, 0, 0) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _status = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = MediaBrushes.DimGray,
            Text = "Previa ao vivo ativa",
        };
        Grid.SetColumn(_status, 0);
        grid.Children.Add(_status);

        var resetItem = new Button { Content = "Restaurar item", Padding = new Thickness(10, 5, 10, 5), Margin = new Thickness(8, 0, 0, 0) };
        resetItem.Click += (_, _) => ResetSlotDefaults();
        Grid.SetColumn(resetItem, 1);
        grid.Children.Add(resetItem);

        var resetAll = new Button { Content = "Restaurar tudo", Padding = new Thickness(10, 5, 10, 5), Margin = new Thickness(6, 0, 0, 0) };
        resetAll.Click += (_, _) => RestoreDefaults();
        Grid.SetColumn(resetAll, 2);
        grid.Children.Add(resetAll);

        var apply = new Button { Content = "Aplicar", Padding = new Thickness(14, 5, 14, 5), Margin = new Thickness(6, 0, 0, 0) };
        apply.Click += (_, _) => ApplyToClock();
        Grid.SetColumn(apply, 3);
        grid.Children.Add(apply);

        return grid;
    }

    private void BuildGeneralTab(StackPanel panel)
    {
        _centerX = CreateIntBox(_config.CENTER_X, v => SetConfig(() => _config.CENTER_X = v));
        _centerY = CreateIntBox(_config.CENTER_Y, v => SetConfig(() => _config.CENTER_Y = v));
        _screenIndex = CreateIntBox(_config.SCREEN_INDEX, v => SetConfig(() => _config.SCREEN_INDEX = v));
        AddSection(panel, "Posicao");
        AddRow(panel, "Centro X", _centerX);
        AddRow(panel, "Centro Y", _centerY);
        AddRow(panel, "Monitor", _screenIndex);

        _layoutMode = CreateCombo(["horizontal", "vertical"], _config.LAYOUT_MODE, v => SetConfig(() => _config.LAYOUT_MODE = v));
        _showSeconds = CreateCheck(_config.SHOW_SECONDS, v => SetConfig(() => _config.SHOW_SECONDS = v));
        _showSeparators = CreateCheck(_config.SHOW_SEPARATORS, v => SetConfig(() => _config.SHOW_SEPARATORS = v));
        _hourMode = CreateCombo(["12h", "24h"], _config.HOUR_MODE, v => SetConfig(() =>
        {
            _config.HOUR_MODE = v;
            _config.HOUR_FORMAT = v == "12h" ? "%I" : "%H";
        }));
        _colonCharacter = CreateTextBox(_config.FONT_COLON_CHARACTER, v => SetConfig(() => _config.FONT_COLON_CHARACTER = string.IsNullOrEmpty(v) ? ":" : v), 4);
        AddSection(panel, "Tempo");
        AddRow(panel, "Layout", _layoutMode);
        AddRow(panel, "Segundos", _showSeconds);
        AddRow(panel, "Separadores", _showSeparators);
        AddRow(panel, "Modo hora", _hourMode);
        AddRow(panel, "Separador texto", _colonCharacter);

        _digitSpacing = CreateIntBox(_config.DIGIT_SPACING, v => SetConfig(() => _config.DIGIT_SPACING = v));
        _separatorSpacing = CreateIntBox(_config.SEPARATOR_SPACING, v => SetConfig(() => _config.SEPARATOR_SPACING = v));
        _groupSpacing = CreateIntBox(_config.GROUP_SPACING, v => SetConfig(() => _config.GROUP_SPACING = v));
        _verticalSpacing = CreateIntBox(_config.VERTICAL_GROUP_SPACING, v => SetConfig(() => _config.VERTICAL_GROUP_SPACING = v));
        _columnWidth = CreateIntBox(_config.VERTICAL_COLUMN_WIDTH, v => SetConfig(() => _config.VERTICAL_COLUMN_WIDTH = v));
        _verticalAlign = CreateCombo(["left", "center", "right"], _config.VERTICAL_ALIGN, v => SetConfig(() => _config.VERTICAL_ALIGN = v));
        AddSection(panel, "Espacamento");
        AddRow(panel, "Espaco digitos", _digitSpacing);
        AddRow(panel, "Espaco sep.", _separatorSpacing);
        AddRow(panel, "Espaco grupos", _groupSpacing);
        AddRow(panel, "Espaco vertical", _verticalSpacing);
        AddRow(panel, "Largura coluna", _columnWidth);
        AddRow(panel, "Alinh. coluna", _verticalAlign);

        _animationEasing = CreateCombo(["InOutQuad", "Linear", "OutCubic", "InCubic", "OutBack"], _config.ANIMATION_EASING, v => SetConfig(() => _config.ANIMATION_EASING = v));
        _updateInterval = CreateIntBox(_config.UPDATE_INTERVAL_MS, v => SetConfig(() => _config.UPDATE_INTERVAL_MS = v));
        AddSection(panel, "Transicao");
        AddRow(panel, "Animacao curva", _animationEasing);
        AddRow(panel, "Atualizacao ms", _updateInterval);

        _fontOutlineWidth = CreateIntBox(_config.FONT_OUTLINE_WIDTH, v => SetConfig(() => _config.FONT_OUTLINE_WIDTH = v));
        _fontPaddingX = CreateIntBox(_config.FONT_PADDING_X, v => SetConfig(() => _config.FONT_PADDING_X = v));
        _fontPaddingY = CreateIntBox(_config.FONT_PADDING_Y, v => SetConfig(() => _config.FONT_PADDING_Y = v));
        _fontOffsetX = CreateIntBox(_config.FONT_OFFSET_X, v => SetConfig(() => _config.FONT_OFFSET_X = v));
        _fontOffsetY = CreateIntBox(_config.FONT_OFFSET_Y, v => SetConfig(() => _config.FONT_OFFSET_Y = v));
        AddSection(panel, "Fonte");
        AddRow(panel, "Largura contorno", _fontOutlineWidth);
        AddRow(panel, "Padding X", _fontPaddingX);
        AddRow(panel, "Padding Y", _fontPaddingY);
        AddRow(panel, "Offset X", _fontOffsetX);
        AddRow(panel, "Offset Y", _fontOffsetY);

        (_windowOpacity, _windowOpacityValue) = CreateOpacitySlider(_config.WINDOW_OPACITY, v => SetConfig(() => _config.WINDOW_OPACITY = v));
        _clickThrough = CreateCheck(_config.CLICK_THROUGH, v => SetConfig(() => _config.CLICK_THROUGH = v));
        _alwaysOnTop = CreateCheck(_config.ALWAYS_ON_TOP, v => SetAlwaysOnTop(v));
        _alwaysOnBottom = CreateCheck(_config.ALWAYS_ON_BOTTOM, v => SetAlwaysOnBottom(v));
        _showInTaskbar = CreateCheck(_config.SHOW_IN_TASKBAR, v => SetConfig(() => _config.SHOW_IN_TASKBAR = v));
        _startWithWindows = CreateCheck(_config.START_WITH_WINDOWS, v => SetConfig(() => _config.START_WITH_WINDOWS = v));
        AddSection(panel, "Janela");
        AddRow(panel, "Opacidade global", BuildSliderControl(_windowOpacity, _windowOpacityValue));
        AddRow(panel, "Ignorar clique", _clickThrough);
        AddRow(panel, "Sempre acima", _alwaysOnTop);
        AddRow(panel, "Sempre abaixo", _alwaysOnBottom);
        AddRow(panel, "Na barra de tarefas", _showInTaskbar);
        AddRow(panel, "Iniciar com Windows", _startWithWindows);
    }

    private void BuildItemTab(StackPanel panel)
    {
        var picker = new Grid { Margin = new Thickness(6, 6, 6, 10) };
        picker.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        picker.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        picker.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var previous = new Button { Content = "<", Width = 34, Padding = new Thickness(4) };
        previous.Click += (_, _) => ShiftSlot(-1);
        picker.Children.Add(previous);

        _selectedLabel = new TextBlock
        {
            TextAlignment = TextAlignment.Center,
            Padding = new Thickness(8),
            Background = MediaBrushes.WhiteSmoke,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(_selectedLabel, 1);
        picker.Children.Add(_selectedLabel);

        var next = new Button { Content = ">", Width = 34, Padding = new Thickness(4) };
        next.Click += (_, _) => ShiftSlot(1);
        Grid.SetColumn(next, 2);
        picker.Children.Add(next);
        panel.Children.Add(picker);

        var allHeader = new Grid { Margin = new Thickness(6, 0, 6, 2) };
        allHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(LabelWidth) });
        allHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        allHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(ApplyColumnWidth) });
        var allText = new TextBlock { Text = "Todos", HorizontalAlignment = WpfHorizontalAlignment.Right, FontWeight = FontWeights.SemiBold };
        Grid.SetColumn(allText, 2);
        allHeader.Children.Add(allText);
        panel.Children.Add(allHeader);

        _slotFont = CreateFontCombo("", SetSlotFont);
        _slotSize = CreateIntBox(1, SetSlotSize);
        _slotBold = CreateCheck(false, SetSlotBold);
        _slotItalic = CreateCheck(false, SetSlotItalic);
        _slotColor = CreateColorButton("#FFFFFF", "Cor do item", SetSlotColor);
        _slotRenderMode = CreateCombo(["filled", "outline", "filled_outline"], "filled", SetSlotRenderMode);
        (_slotOpacity, _slotOpacityValue) = CreateOpacitySlider(1.0, SetSlotOpacity);
        _slotAnimationDuration = CreateIntBox(130, SetSlotAnimationDuration);
        _slotWidth = CreateIntBox(1, SetSlotWidth);
        _slotHeight = CreateIntBox(1, SetSlotHeight);
        _slotTextX = CreateIntBox(0, v => SetSlotTextOffset("x", v));
        _slotTextY = CreateIntBox(0, v => SetSlotTextOffset("y", v));
        _slotPosX = CreateIntBox(0, v => SetSlotPositionOffset("x", v));
        _slotPosY = CreateIntBox(0, v => SetSlotPositionOffset("y", v));

        AddItemRow(panel, "font", "Fonte", _slotFont);
        AddItemRow(panel, "size", "Tamanho texto", _slotSize);
        AddItemRow(panel, "bold", "Negrito", _slotBold);
        AddItemRow(panel, "italic", "Italico", _slotItalic);
        AddItemRow(panel, "color", "Cor", _slotColor);
        AddItemRow(panel, "render", "Modo render", _slotRenderMode);
        AddItemRow(panel, "opacity", "Opacidade", BuildSliderControl(_slotOpacity, _slotOpacityValue));
        AddItemRow(panel, "animation", "Animacao ms", _slotAnimationDuration);
        AddItemRow(panel, "width", "Largura", _slotWidth);
        AddItemRow(panel, "height", "Altura", _slotHeight);
        AddItemRow(panel, "text_x", "Texto X", _slotTextX);
        AddItemRow(panel, "text_y", "Texto Y", _slotTextY);
        AddItemRow(panel, "pos_x", "Posicao X", _slotPosX);
        AddItemRow(panel, "pos_y", "Posicao Y", _slotPosY);
    }

    private void AddSection(StackPanel parent, string title)
    {
        parent.Children.Add(new TextBlock
        {
            Text = title,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(6, 12, 6, 4),
        });
    }

    private void AddRow(StackPanel parent, string label, FrameworkElement control)
    {
        parent.Children.Add(BuildRow(label, control, null));
    }

    private void AddItemRow(StackPanel parent, string key, string label, FrameworkElement control)
    {
        var all = new CheckBox
        {
            HorizontalAlignment = WpfHorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
        };
        all.Checked += (_, _) => ApplyCurrentToAll(key);
        _applyAll[key] = all;
        parent.Children.Add(BuildRow(label, control, all));
    }

    private static Grid BuildRow(string label, FrameworkElement control, CheckBox? all)
    {
        var grid = new Grid { Margin = new Thickness(6, 4, 6, 4) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(LabelWidth) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(ApplyColumnWidth) });

        var labelBlock = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(labelBlock, 0);
        grid.Children.Add(labelBlock);

        control.MinHeight = 25;
        Grid.SetColumn(control, 1);
        grid.Children.Add(control);

        if (control is CheckBox check)
        {
            ConfigureCheckRow(grid, labelBlock, check, label);
        }

        if (all is not null)
        {
            Grid.SetColumn(all, 2);
            grid.Children.Add(all);
        }

        return grid;
    }

    private static void ConfigureCheckRow(Grid grid, TextBlock labelBlock, CheckBox check, string label)
    {
        grid.MinHeight = 32;
        grid.Cursor = WpfCursors.Hand;
        labelBlock.Cursor = WpfCursors.Hand;
        check.MinWidth = 26;
        check.MinHeight = 26;
        check.HorizontalAlignment = WpfHorizontalAlignment.Left;
        check.VerticalAlignment = VerticalAlignment.Center;
        check.ToolTip = label;
        AutomationProperties.SetName(check, label);

        grid.MouseLeftButtonUp += (sender, e) =>
        {
            if (IsDescendantOf(e.OriginalSource as DependencyObject, check))
            {
                return;
            }

            check.IsChecked = check.IsChecked != true;
            e.Handled = true;
        };
    }

    private static bool IsDescendantOf(DependencyObject? source, DependencyObject target)
    {
        while (source is not null)
        {
            if (ReferenceEquals(source, target))
            {
                return true;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return false;
    }

    private TextBox CreateTextBox(string value, Action<string> callback, int maxLength = 0)
    {
        var box = new TextBox { Text = value };
        if (maxLength > 0)
        {
            box.MaxLength = maxLength;
        }

        box.TextChanged += (_, _) =>
        {
            if (!_loading)
            {
                callback(box.Text);
            }
        };
        return box;
    }

    private TextBox CreateIntBox(int value, Action<int> callback)
    {
        var box = new TextBox { Text = value.ToString(CultureInfo.InvariantCulture) };
        box.TextChanged += (_, _) =>
        {
            if (_loading)
            {
                return;
            }

            if (int.TryParse(box.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                callback(parsed);
            }
        };
        return box;
    }

    private TextBox CreateDoubleBox(double value, Action<double> callback)
    {
        var box = new TextBox { Text = value.ToString("0.##", CultureInfo.InvariantCulture) };
        box.TextChanged += (_, _) =>
        {
            if (_loading)
            {
                return;
            }

            if (double.TryParse(box.Text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            {
                callback(parsed);
            }
        };
        return box;
    }

    private (Slider Slider, TextBlock ValueText) CreateOpacitySlider(double value, Action<double> callback)
    {
        var valueText = new TextBlock
        {
            MinWidth = 48,
            TextAlignment = TextAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var slider = new Slider
        {
            Minimum = 0,
            Maximum = 100,
            TickFrequency = 5,
            IsSnapToTickEnabled = false,
            VerticalAlignment = VerticalAlignment.Center,
            Value = ConfigService.Clamp(value, 0.0, 1.0) * 100,
        };
        UpdateOpacityText(valueText, slider.Value);
        slider.ValueChanged += (_, _) =>
        {
            UpdateOpacityText(valueText, slider.Value);
            if (!_loading)
            {
                callback(ConfigService.Clamp(slider.Value / 100.0, 0.0, 1.0));
            }
        };

        return (slider, valueText);
    }

    private static Grid BuildSliderControl(Slider slider, TextBlock valueText)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        slider.Margin = new Thickness(0, 0, 8, 0);
        Grid.SetColumn(slider, 0);
        grid.Children.Add(slider);
        Grid.SetColumn(valueText, 1);
        grid.Children.Add(valueText);
        return grid;
    }

    private static void UpdateOpacityText(TextBlock text, double value)
    {
        text.Text = $"{Math.Round(value):0}%";
    }

    private CheckBox CreateCheck(bool value, Action<bool> callback)
    {
        var check = new CheckBox
        {
            IsChecked = value,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = WpfHorizontalAlignment.Left,
        };
        check.Checked += (_, _) =>
        {
            if (!_loading)
            {
                callback(true);
            }
        };
        check.Unchecked += (_, _) =>
        {
            if (!_loading)
            {
                callback(false);
            }
        };
        return check;
    }

    private ComboBox CreateCombo(IEnumerable<string> values, string value, Action<string> callback)
    {
        var combo = new ComboBox { ItemsSource = values.ToArray(), IsEditable = false, SelectedItem = value };
        combo.SelectionChanged += (_, _) =>
        {
            if (!_loading && combo.SelectedItem is string selected)
            {
                callback(selected);
            }
        };
        return combo;
    }

    private ComboBox CreateFontCombo(string value, Action<string> callback)
    {
        var combo = new ComboBox
        {
            ItemsSource = _fonts,
            IsEditable = true,
            IsTextSearchEnabled = true,
            Text = value,
        };
        combo.SelectionChanged += (_, _) =>
        {
            if (!_loading)
            {
                callback(combo.SelectedItem?.ToString() ?? combo.Text);
            }
        };
        combo.LostKeyboardFocus += (_, _) =>
        {
            if (!_loading)
            {
                callback(combo.Text);
            }
        };
        return combo;
    }

    private Button CreateColorButton(string color, string title, Action<string> callback)
    {
        var button = new Button { Padding = new Thickness(8, 3, 8, 3) };
        SetButtonColor(button, color);
        button.Click += (_, _) =>
        {
            var picked = AskColor((string)(button.Tag ?? color), title);
            if (picked is null)
            {
                return;
            }

            callback(picked);
        };
        return button;
    }

    private static string? AskColor(string current, string title)
    {
        var mediaColor = ConfigService.ParseMediaColor(current);
        using var dialog = new Forms.ColorDialog
        {
            Color = System.Drawing.Color.FromArgb(mediaColor.R, mediaColor.G, mediaColor.B),
            FullOpen = true,
            AnyColor = true,
            SolidColorOnly = false,
        };

        return dialog.ShowDialog() == Forms.DialogResult.OK
            ? $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}"
            : null;
    }

    private static void SetButtonColor(Button button, string color)
    {
        color = ConfigService.NormalizeHex(color, "#FFFFFF");
        var mediaColor = ConfigService.ParseMediaColor(color);
        button.Tag = color;
        button.Content = color;
        button.Background = new SolidColorBrush(mediaColor);
        var luminance = (0.2126 * mediaColor.R) + (0.7152 * mediaColor.G) + (0.0722 * mediaColor.B);
        button.Foreground = luminance > 150 ? MediaBrushes.Black : MediaBrushes.White;
    }

    private void SetConfig(Action mutate)
    {
        if (_loading)
        {
            return;
        }

        mutate();
        _config = ConfigService.EnsureShape(_config);
        ScheduleLivePreview();
    }

    private void SetAlwaysOnTop(bool value)
    {
        SetConfig(() =>
        {
            _config.ALWAYS_ON_TOP = value;
            if (value)
            {
                _config.ALWAYS_ON_BOTTOM = false;
                _alwaysOnBottom.IsChecked = false;
            }
        });
    }

    private void SetAlwaysOnBottom(bool value)
    {
        SetConfig(() =>
        {
            _config.ALWAYS_ON_BOTTOM = value;
            if (value)
            {
                _config.ALWAYS_ON_TOP = false;
                _alwaysOnTop.IsChecked = false;
            }
        });
    }

    private void ShiftSlot(int direction)
    {
        var index = Array.IndexOf(ClockConfig.SlotIds, _selectedSlot);
        if (index < 0)
        {
            index = 0;
        }

        var next = (index + direction + ClockConfig.SlotIds.Length) % ClockConfig.SlotIds.Length;
        SelectSlot(ClockConfig.SlotIds[next]);
    }

    private void SelectSlot(string slotId)
    {
        _selectedSlot = slotId;
        RefreshSelectedControls();
        ScheduleLivePreview();
    }

    private IEnumerable<string> SlotTargets(string key)
    {
        return _applyAll.TryGetValue(key, out var check) && check.IsChecked == true
            ? ClockConfig.SlotIds
            : [_selectedSlot];
    }

    private void SetSlotFont(string family)
    {
        if (_loading)
        {
            return;
        }

        family = string.IsNullOrWhiteSpace(family) ? _config.FONT_FAMILY : family.Trim();
        foreach (var slotId in SlotTargets("font"))
        {
            _config.SLOT_FONT_FAMILIES[slotId] = family;
        }

        RefreshSelectedControls();
        ScheduleLivePreview();
    }

    private void SetSlotSize(int value)
    {
        if (_loading)
        {
            return;
        }

        foreach (var slotId in SlotTargets("size"))
        {
            _config.SLOT_FONT_PIXEL_SIZES[slotId] = value;
            GrowSlotBoxForFont(slotId);
        }

        RefreshSelectedControls();
        ScheduleLivePreview();
    }

    private void SetSlotBold(bool value)
    {
        if (_loading)
        {
            return;
        }

        foreach (var slotId in SlotTargets("bold"))
        {
            _config.SLOT_FONT_BOLD[slotId] = value;
        }

        RefreshSelectedControls();
        ScheduleLivePreview();
    }

    private void SetSlotItalic(bool value)
    {
        if (_loading)
        {
            return;
        }

        foreach (var slotId in SlotTargets("italic"))
        {
            _config.SLOT_FONT_ITALIC[slotId] = value;
        }

        RefreshSelectedControls();
        ScheduleLivePreview();
    }

    private void SetSlotColor(string color)
    {
        if (_loading)
        {
            return;
        }

        color = ConfigService.NormalizeHex(color, _config.FONT_COLOR);
        foreach (var slotId in SlotTargets("color"))
        {
            _config.SLOT_FONT_COLORS[slotId] = color;
        }

        RefreshSelectedControls();
        ScheduleLivePreview();
    }

    private void SetSlotRenderMode(string mode)
    {
        if (_loading)
        {
            return;
        }

        foreach (var slotId in SlotTargets("render"))
        {
            _config.SLOT_RENDER_MODES[slotId] = mode;
        }

        RefreshSelectedControls();
        ScheduleLivePreview();
    }

    private void SetSlotOpacity(double opacity)
    {
        if (_loading)
        {
            return;
        }

        foreach (var slotId in SlotTargets("opacity"))
        {
            _config.SLOT_OPACITIES[slotId] = ConfigService.Clamp(opacity, 0.0, 1.0);
        }

        RefreshSelectedControls();
        ScheduleLivePreview();
    }

    private void SetSlotAnimationDuration(int value)
    {
        if (_loading)
        {
            return;
        }

        foreach (var slotId in SlotTargets("animation"))
        {
            _config.SLOT_ANIMATION_DURATIONS_MS[slotId] = ConfigService.Clamp(value, 0, 5000);
        }

        RefreshSelectedControls();
        ScheduleLivePreview();
    }

    private void SetSlotWidth(int value)
    {
        if (_loading)
        {
            return;
        }

        foreach (var slotId in SlotTargets("width"))
        {
            _config.SLOT_WIDTHS[slotId] = value;
        }

        ScheduleLivePreview();
    }

    private void SetSlotHeight(int value)
    {
        if (_loading)
        {
            return;
        }

        foreach (var slotId in SlotTargets("height"))
        {
            _config.SLOT_HEIGHTS[slotId] = value;
        }

        ScheduleLivePreview();
    }

    private void SetSlotTextOffset(string axis, int value)
    {
        if (_loading)
        {
            return;
        }

        foreach (var slotId in SlotTargets(axis == "x" ? "text_x" : "text_y"))
        {
            var offset = _config.SLOT_TEXT_OFFSETS[slotId];
            if (axis == "x")
            {
                offset.x = value;
            }
            else
            {
                offset.y = value;
            }
        }

        ScheduleLivePreview();
    }

    private void SetSlotPositionOffset(string axis, int value)
    {
        if (_loading)
        {
            return;
        }

        foreach (var slotId in SlotTargets(axis == "x" ? "pos_x" : "pos_y"))
        {
            var offset = _config.SLOT_POSITION_OFFSETS[slotId];
            if (axis == "x")
            {
                offset.x = value;
            }
            else
            {
                offset.y = value;
            }
        }

        ScheduleLivePreview();
    }

    private void ApplyCurrentToAll(string key)
    {
        if (_loading)
        {
            return;
        }

        switch (key)
        {
            case "font":
                CopyToAll(slotId => _config.SLOT_FONT_FAMILIES[slotId] = _config.SLOT_FONT_FAMILIES[_selectedSlot]);
                break;
            case "size":
                CopyToAll(slotId =>
                {
                    _config.SLOT_FONT_PIXEL_SIZES[slotId] = _config.SLOT_FONT_PIXEL_SIZES[_selectedSlot];
                    GrowSlotBoxForFont(slotId);
                });
                break;
            case "bold":
                CopyToAll(slotId => _config.SLOT_FONT_BOLD[slotId] = _config.SLOT_FONT_BOLD[_selectedSlot]);
                break;
            case "italic":
                CopyToAll(slotId => _config.SLOT_FONT_ITALIC[slotId] = _config.SLOT_FONT_ITALIC[_selectedSlot]);
                break;
            case "color":
                CopyToAll(slotId => _config.SLOT_FONT_COLORS[slotId] = _config.SLOT_FONT_COLORS[_selectedSlot]);
                break;
            case "render":
                CopyToAll(slotId => _config.SLOT_RENDER_MODES[slotId] = _config.SLOT_RENDER_MODES[_selectedSlot]);
                break;
            case "opacity":
                CopyToAll(slotId => _config.SLOT_OPACITIES[slotId] = _config.SLOT_OPACITIES[_selectedSlot]);
                break;
            case "animation":
                CopyToAll(slotId => _config.SLOT_ANIMATION_DURATIONS_MS[slotId] = _config.SLOT_ANIMATION_DURATIONS_MS[_selectedSlot]);
                break;
            case "width":
                CopyToAll(slotId => _config.SLOT_WIDTHS[slotId] = _config.SLOT_WIDTHS[_selectedSlot]);
                break;
            case "height":
                CopyToAll(slotId => _config.SLOT_HEIGHTS[slotId] = _config.SLOT_HEIGHTS[_selectedSlot]);
                break;
            case "text_x":
                CopyToAll(slotId => _config.SLOT_TEXT_OFFSETS[slotId].x = _config.SLOT_TEXT_OFFSETS[_selectedSlot].x);
                break;
            case "text_y":
                CopyToAll(slotId => _config.SLOT_TEXT_OFFSETS[slotId].y = _config.SLOT_TEXT_OFFSETS[_selectedSlot].y);
                break;
            case "pos_x":
                CopyToAll(slotId => _config.SLOT_POSITION_OFFSETS[slotId].x = _config.SLOT_POSITION_OFFSETS[_selectedSlot].x);
                break;
            case "pos_y":
                CopyToAll(slotId => _config.SLOT_POSITION_OFFSETS[slotId].y = _config.SLOT_POSITION_OFFSETS[_selectedSlot].y);
                break;
        }

        RefreshSelectedControls();
        ScheduleLivePreview();
    }

    private static void CopyToAll(Action<string> copy)
    {
        foreach (var slotId in ClockConfig.SlotIds)
        {
            copy(slotId);
        }
    }

    private void GrowSlotBoxForFont(string slotId)
    {
        var fontSize = _config.SLOT_FONT_PIXEL_SIZES.GetValueOrDefault(slotId, _config.FONT_PIXEL_SIZE);
        _config.SLOT_WIDTHS[slotId] = Math.Max(_config.SLOT_WIDTHS.GetValueOrDefault(slotId, 1), ConfigService.MinimumSlotWidth(slotId, fontSize));
        _config.SLOT_HEIGHTS[slotId] = Math.Max(_config.SLOT_HEIGHTS.GetValueOrDefault(slotId, 1), ConfigService.MinimumSlotHeight(fontSize));
    }

    private void RefreshAllControls()
    {
        _loading = true;
        SetText(_centerX, _config.CENTER_X);
        SetText(_centerY, _config.CENTER_Y);
        SetText(_screenIndex, _config.SCREEN_INDEX);
        _layoutMode.SelectedItem = _config.LAYOUT_MODE;
        _showSeconds.IsChecked = _config.SHOW_SECONDS;
        _showSeparators.IsChecked = _config.SHOW_SEPARATORS;
        _hourMode.SelectedItem = _config.HOUR_MODE;
        SetText(_colonCharacter, _config.FONT_COLON_CHARACTER);
        SetText(_digitSpacing, _config.DIGIT_SPACING);
        SetText(_separatorSpacing, _config.SEPARATOR_SPACING);
        SetText(_groupSpacing, _config.GROUP_SPACING);
        SetText(_verticalSpacing, _config.VERTICAL_GROUP_SPACING);
        SetText(_columnWidth, _config.VERTICAL_COLUMN_WIDTH);
        _verticalAlign.SelectedItem = _config.VERTICAL_ALIGN;
        _animationEasing.SelectedItem = _config.ANIMATION_EASING;
        SetText(_updateInterval, _config.UPDATE_INTERVAL_MS);
        SetText(_fontOutlineWidth, _config.FONT_OUTLINE_WIDTH);
        SetText(_fontPaddingX, _config.FONT_PADDING_X);
        SetText(_fontPaddingY, _config.FONT_PADDING_Y);
        SetText(_fontOffsetX, _config.FONT_OFFSET_X);
        SetText(_fontOffsetY, _config.FONT_OFFSET_Y);
        SetSlider(_windowOpacity, _windowOpacityValue, _config.WINDOW_OPACITY);
        _clickThrough.IsChecked = _config.CLICK_THROUGH;
        _alwaysOnTop.IsChecked = _config.ALWAYS_ON_TOP;
        _alwaysOnBottom.IsChecked = _config.ALWAYS_ON_BOTTOM;
        _showInTaskbar.IsChecked = _config.SHOW_IN_TASKBAR;
        _startWithWindows.IsChecked = _config.START_WITH_WINDOWS;
        _loading = false;

        RefreshSelectedControls();
    }

    private void SystemEvents_UserPreferenceChanged(object sender, Microsoft.Win32.UserPreferenceChangedEventArgs e)
    {
        Dispatcher.BeginInvoke(ApplyTheme);
    }

    private void ApplyTheme()
    {
        var palette = ThemeService.CurrentPalette();
        ThemeService.ApplyWindowFrame(this, palette);
        ThemeService.ApplyControlTree(this, palette);
        _selectedLabel.Background = new SolidColorBrush(palette.Panel);
        _selectedLabel.Foreground = new SolidColorBrush(palette.Text);
        RefreshColorButtons();
    }

    private void RefreshColorButtons()
    {
        if (_slotColor is not null && _slotColor.Tag is string slotColor)
        {
            SetButtonColor(_slotColor, slotColor);
        }
    }

    private void RefreshSelectedControls()
    {
        _loading = true;
        var index = Array.IndexOf(ClockConfig.SlotIds, _selectedSlot) + 1;
        _selectedLabel.Text = $"{index}/{ClockConfig.SlotIds.Length} - {ClockConfig.SlotLabels.GetValueOrDefault(_selectedSlot, _selectedSlot)}";
        _slotFont.Text = _config.SLOT_FONT_FAMILIES[_selectedSlot];
        SetText(_slotSize, _config.SLOT_FONT_PIXEL_SIZES[_selectedSlot]);
        _slotBold.IsChecked = _config.SLOT_FONT_BOLD[_selectedSlot];
        _slotItalic.IsChecked = _config.SLOT_FONT_ITALIC[_selectedSlot];
        SetButtonColor(_slotColor, _config.SLOT_FONT_COLORS[_selectedSlot]);
        _slotRenderMode.SelectedItem = _config.SLOT_RENDER_MODES[_selectedSlot];
        SetSlider(_slotOpacity, _slotOpacityValue, _config.SLOT_OPACITIES[_selectedSlot]);
        SetText(_slotAnimationDuration, _config.SLOT_ANIMATION_DURATIONS_MS[_selectedSlot]);
        SetText(_slotWidth, _config.SLOT_WIDTHS[_selectedSlot]);
        SetText(_slotHeight, _config.SLOT_HEIGHTS[_selectedSlot]);
        SetText(_slotTextX, _config.SLOT_TEXT_OFFSETS[_selectedSlot].x);
        SetText(_slotTextY, _config.SLOT_TEXT_OFFSETS[_selectedSlot].y);
        SetText(_slotPosX, _config.SLOT_POSITION_OFFSETS[_selectedSlot].x);
        SetText(_slotPosY, _config.SLOT_POSITION_OFFSETS[_selectedSlot].y);
        _loading = false;
    }

    private static void SetText(TextBox box, object value)
    {
        box.Text = value switch
        {
            double number => number.ToString("0.##", CultureInfo.InvariantCulture),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "",
        };
    }

    private static void SetSlider(Slider slider, TextBlock valueText, double value)
    {
        slider.Value = ConfigService.Clamp(value, 0.0, 1.0) * 100;
        UpdateOpacityText(valueText, slider.Value);
    }

    private void ResetSlotDefaults()
    {
        var slotId = _selectedSlot;
        _config.SLOT_FONT_FAMILIES[slotId] = _defaults.SLOT_FONT_FAMILIES[slotId];
        _config.SLOT_FONT_PIXEL_SIZES[slotId] = _defaults.SLOT_FONT_PIXEL_SIZES[slotId];
        _config.SLOT_FONT_BOLD[slotId] = _defaults.SLOT_FONT_BOLD[slotId];
        _config.SLOT_FONT_ITALIC[slotId] = _defaults.SLOT_FONT_ITALIC[slotId];
        _config.SLOT_FONT_COLORS[slotId] = _defaults.SLOT_FONT_COLORS[slotId];
        _config.SLOT_RENDER_MODES[slotId] = _defaults.SLOT_RENDER_MODES[slotId];
        _config.SLOT_OPACITIES[slotId] = _defaults.SLOT_OPACITIES[slotId];
        _config.SLOT_ANIMATION_DURATIONS_MS[slotId] = _defaults.SLOT_ANIMATION_DURATIONS_MS[slotId];
        _config.SLOT_WIDTHS[slotId] = _defaults.SLOT_WIDTHS[slotId];
        _config.SLOT_HEIGHTS[slotId] = _defaults.SLOT_HEIGHTS[slotId];
        _config.SLOT_TEXT_OFFSETS[slotId] = _defaults.SLOT_TEXT_OFFSETS[slotId].Clone();
        _config.SLOT_POSITION_OFFSETS[slotId] = _defaults.SLOT_POSITION_OFFSETS[slotId].Clone();
        RefreshSelectedControls();
        ScheduleLivePreview();
    }

    private void RestoreDefaults()
    {
        _config = ClockConfig.CreateDefault();
        RefreshAllControls();
        ScheduleLivePreview();
    }

    private void ApplyToClock()
    {
        try
        {
            ConfigService.SaveConfig(_configPath, _config, includePreview: false);
            StartupService.SyncWindowsStartupRegistration(_config, _configPath);
            _status.Text = $"Aplicado em {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex)
        {
            ConfigService.LogError(ex);
            Forms.MessageBox.Show(ex.Message, "Erro ao aplicar", Forms.MessageBoxButtons.OK, Forms.MessageBoxIcon.Error);
        }
    }

    private void ScheduleLivePreview()
    {
        if (_loading)
        {
            return;
        }

        _previewTimer.Stop();
        _previewTimer.Start();
    }

    private void UpdateLivePreview()
    {
        try
        {
            var preview = _config.Clone();
            preview.PREVIEW_SELECTED_SLOT = _selectedSlot;
            preview.PREVIEW_SHOW_SELECTION = true;
            ConfigService.SaveConfig(_previewPath, preview, includePreview: true);
            EnsurePreviewRunning();
            _status.Text = "Previa atualizada";
        }
        catch (Exception ex)
        {
            ConfigService.LogError(ex);
            _status.Text = "Erro na previa";
        }
    }

    private void EnsurePreviewRunning()
    {
        if (_previewProcess is { HasExited: false })
        {
            return;
        }

        var exePath = StartupService.ResolveExecutablePath();
        var info = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = $"--clock --config \"{_previewPath}\" --preview",
            WorkingDirectory = ConfigService.RootDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        _previewProcess = Process.Start(info);
    }

    private void StopPreview()
    {
        var process = _previewProcess;
        _previewProcess = null;
        if (process is null || process.HasExited)
        {
            return;
        }

        try
        {
            process.CloseMainWindow();
            if (!process.WaitForExit(800))
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // The preview is best-effort and should not block editor shutdown.
        }
    }

    private void DeletePreviewConfig()
    {
        try
        {
            if (File.Exists(_previewPath))
            {
                File.Delete(_previewPath);
            }
        }
        catch
        {
            // Temporary preview cleanup is best-effort.
        }
    }
}
