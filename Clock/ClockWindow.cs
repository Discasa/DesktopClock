using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using DesktopClock.Models;
using DesktopClock.Native;
using DesktopClock.Services;
using Forms = System.Windows.Forms;
using IOPath = System.IO.Path;
using MediaBrushes = System.Windows.Media.Brushes;
using MediaFontFamily = System.Windows.Media.FontFamily;
using ModelClockGroup = DesktopClock.Models.ClockGroup;
using ShapePath = System.Windows.Shapes.Path;
using WpfFlowDirection = System.Windows.FlowDirection;
using WpfPoint = System.Windows.Point;

namespace DesktopClock.Clock;

public sealed class ClockWindow : Window
{
    private readonly string _configPath;
    private readonly bool _previewMode;
    private readonly Canvas _root = new();
    private readonly DispatcherTimer _clockTimer = new();
    private readonly DispatcherTimer _reloadTimer = new();
    private readonly Dictionary<string, string> _currentValuesBySlot = new();
    private readonly Dictionary<string, Canvas> _slotCanvases = new();
    private readonly List<SlotLayout> _slots = [];
    private readonly List<IntPtr> _windowsHiddenByEditor = [];

    private ClockConfig _config;
    private DateTime? _configMtime;
    private IntPtr _hwnd;
    private bool _hiddenByEditor;

    public ClockWindow(string configPath, bool previewMode)
    {
        _configPath = IOPath.GetFullPath(configPath);
        _previewMode = previewMode;
        _config = ConfigService.LoadConfig(_configPath);
        _configMtime = ConfigService.GetLastWriteTimeUtc(_configPath);

        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = MediaBrushes.Transparent;
        ShowActivated = false;
        Focusable = false;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Content = _root;

        _clockTimer.Tick += (_, _) => SafeRun(() =>
        {
            UpdateClock(force: false, animate: true);
            ScheduleClockTimer();
        });
        _reloadTimer.Tick += (_, _) => SafeRun(ReloadConfigIfNeeded);

        SourceInitialized += (_, _) =>
        {
            _hwnd = new WindowInteropHelper(this).Handle;
            ApplyWindowStyles();
            ApplyZOrder();
        };

        Loaded += (_, _) => SafeRun(() =>
        {
            ApplyConfig(initial: true);
            UpdateClock(force: true, animate: false);
            ScheduleClockTimer();
            ScheduleReloadTimer();
            SyncEditorVisibility();
        });
    }

    public void ApplyExternalConfig(ClockConfig config)
    {
        _config = ConfigService.EnsureShape(config.Clone());
        ApplyConfig(initial: false);
        UpdateClock(force: true, animate: false);
    }

    protected override void OnClosed(EventArgs e)
    {
        _clockTimer.Stop();
        _reloadTimer.Stop();
        base.OnClosed(e);
    }

    private void ReloadConfigIfNeeded()
    {
        SyncEditorVisibility();
        var current = ConfigService.GetLastWriteTimeUtc(_configPath);
        if (current != _configMtime)
        {
            _configMtime = current;
            _config = ConfigService.LoadConfig(_configPath);
            ApplyConfig(initial: false);
            UpdateClock(force: true, animate: false);
        }

        ScheduleReloadTimer();
    }

    private void SyncEditorVisibility()
    {
        if (_previewMode)
        {
            return;
        }

        var editorActive = ConfigService.IsEditorActive(_configPath);
        if (editorActive)
        {
            _hiddenByEditor = true;
            HideProcessWindows();
            Hide();
            return;
        }

        if (!editorActive && _hiddenByEditor)
        {
            _hiddenByEditor = false;
            Show();
            RestoreProcessWindows();
            ApplyWindowStyles();
            ApplyZOrder();
        }
    }

    private void HideProcessWindows()
    {
        var currentPid = Environment.ProcessId;
        Win32.EnumWindows((hwnd, _) =>
        {
            if (!Win32.IsWindowVisible(hwnd))
            {
                return true;
            }

            Win32.GetWindowThreadProcessId(hwnd, out var pid);
            if (pid == currentPid)
            {
                Win32.ShowWindow(hwnd, Win32.SW_HIDE);
                if (!_windowsHiddenByEditor.Contains(hwnd))
                {
                    _windowsHiddenByEditor.Add(hwnd);
                }
            }

            return true;
        }, IntPtr.Zero);
    }

    private void RestoreProcessWindows()
    {
        foreach (var hwnd in _windowsHiddenByEditor)
        {
            Win32.ShowWindow(hwnd, Win32.SW_SHOWNA);
        }

        _windowsHiddenByEditor.Clear();
    }

    private void ApplyConfig(bool initial)
    {
        _config = ConfigService.EnsureShape(_config);
        ShowInTaskbar = _config.SHOW_IN_TASKBAR;
        Topmost = _config.ALWAYS_ON_TOP;
        ApplyWindowStyles();

        var layout = BuildLayout();
        _slots.Clear();
        _slots.AddRange(layout.Items);

        Width = Math.Max(1, layout.Width);
        Height = Math.Max(1, layout.Height);
        MinWidth = Width;
        MinHeight = Height;
        MaxWidth = Width;
        MaxHeight = Height;
        _root.Width = Width;
        _root.Height = Height;
        _root.Children.Clear();
        _slotCanvases.Clear();

        foreach (var slot in _slots)
        {
            var canvas = new Canvas
            {
                Width = slot.Width,
                Height = slot.Height,
                ClipToBounds = true,
                IsHitTestVisible = false,
            };
            Canvas.SetLeft(canvas, slot.X);
            Canvas.SetTop(canvas, slot.Y);
            _root.Children.Add(canvas);
            _slotCanvases[slot.Id] = canvas;
        }

        MoveToConfiguredCenter(layout.Width, layout.Height);
        ApplyZOrder();

        if (!initial)
        {
            ScheduleClockTimer();
            ScheduleReloadTimer();
        }
    }

    private ClockLayout BuildLayout()
    {
        return string.Equals(_config.LAYOUT_MODE, "vertical", StringComparison.OrdinalIgnoreCase)
            ? BuildVerticalLayout()
            : BuildHorizontalLayout();
    }

    private ClockLayout BuildHorizontalLayout()
    {
        var items = new List<SlotLayout>();
        double x = 0;
        var activeGroups = ActiveGroups().ToArray();

        for (var groupIndex = 0; groupIndex < activeGroups.Length; groupIndex++)
        {
            var group = activeGroups[groupIndex];
            if (groupIndex > 0)
            {
                if (_config.SHOW_SEPARATORS)
                {
                    x += _config.SEPARATOR_SPACING;
                    var separator = CreateLayoutItem($"separator_{groupIndex}", "separator", x, 0, "", -1);
                    items.Add(separator);
                    x += separator.Width + _config.SEPARATOR_SPACING;
                }
                else
                {
                    x += _config.GROUP_SPACING;
                }
            }

            for (var digitIndex = 0; digitIndex < group.Slots.Length; digitIndex++)
            {
                var slotId = group.Slots[digitIndex];
                if (digitIndex > 0)
                {
                    x += _config.DIGIT_SPACING;
                }

                var digit = CreateLayoutItem(slotId, "digit", x, 0, group.Name, digitIndex);
                items.Add(digit);
                x += digit.Width;
            }
        }

        return NormalizeBounds(items);
    }

    private ClockLayout BuildVerticalLayout()
    {
        var activeGroups = ActiveGroups().ToArray();
        var rowWidths = activeGroups.ToDictionary(group => group.Name, GroupWidth);
        var widestRow = rowWidths.Count == 0 ? 1 : rowWidths.Values.Max();
        var columnWidth = Math.Max(_config.VERTICAL_COLUMN_WIDTH, widestRow);
        var items = new List<SlotLayout>();
        double y = 0;

        foreach (var group in activeGroups)
        {
            var groupWidth = rowWidths[group.Name];
            var x = AlignedX(columnWidth, groupWidth, group.Name);

            for (var digitIndex = 0; digitIndex < group.Slots.Length; digitIndex++)
            {
                var slotId = group.Slots[digitIndex];
                if (digitIndex > 0)
                {
                    x += _config.DIGIT_SPACING;
                }

                var digit = CreateLayoutItem(slotId, "digit", x, y, group.Name, digitIndex);
                items.Add(digit);
                x += digit.Width;
            }

            y += GroupHeight(group) + _config.VERTICAL_GROUP_SPACING;
        }

        var layout = NormalizeBounds(items);
        layout.Width = Math.Max(layout.Width, columnWidth);
        return layout;
    }

    private IEnumerable<ModelClockGroup> ActiveGroups()
    {
        return _config.SHOW_SECONDS ? ClockConfig.Groups : ClockConfig.Groups.Take(2);
    }

    private double GroupWidth(ModelClockGroup group)
    {
        return group.Slots.Sum(slotId => GetSlotSize(slotId).Width) +
               (_config.DIGIT_SPACING * Math.Max(0, group.Slots.Length - 1));
    }

    private double GroupHeight(ModelClockGroup group)
    {
        return group.Slots.Select(slotId => GetSlotSize(slotId).Height).DefaultIfEmpty(1).Max();
    }

    private double AlignedX(double columnWidth, double rowWidth, string groupName)
    {
        var align = _config.VERTICAL_GROUP_ALIGNMENTS.GetValueOrDefault(groupName, _config.VERTICAL_ALIGN);
        var offset = _config.VERTICAL_GROUP_OFFSETS_X.GetValueOrDefault(groupName, 0);
        return align switch
        {
            "left" => offset,
            "right" => columnWidth - rowWidth + offset,
            _ => ((columnWidth - rowWidth) / 2.0) + offset,
        };
    }

    private SlotLayout CreateLayoutItem(string slotId, string kind, double x, double y, string group, int digitIndex)
    {
        var size = GetSlotSize(slotId);
        var offset = _config.SLOT_POSITION_OFFSETS.GetValueOrDefault(slotId, new PointOffset());
        return new SlotLayout(slotId, kind, x + offset.x, y + offset.y, size.Width, size.Height, group, digitIndex);
    }

    private SlotDimensions GetSlotSize(string slotId)
    {
        if (_config.SLOT_WIDTHS.TryGetValue(slotId, out var width) &&
            _config.SLOT_HEIGHTS.TryGetValue(slotId, out var height))
        {
            return new SlotDimensions(Math.Max(1, width), Math.Max(1, height));
        }

        return slotId.StartsWith("separator", StringComparison.OrdinalIgnoreCase)
            ? new SlotDimensions(_config.SEPARATOR_WIDTH, _config.SEPARATOR_HEIGHT)
            : new SlotDimensions(_config.DIGIT_WIDTH, _config.DIGIT_HEIGHT);
    }

    private static ClockLayout NormalizeBounds(List<SlotLayout> items)
    {
        if (items.Count == 0)
        {
            return new ClockLayout([], 1, 1);
        }

        var minX = items.Min(item => item.X);
        var minY = items.Min(item => item.Y);
        var maxX = items.Max(item => item.X + item.Width);
        var maxY = items.Max(item => item.Y + item.Height);

        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            items[index] = item with { X = item.X - minX, Y = item.Y - minY };
        }

        return new ClockLayout(items, Math.Max(1, maxX - minX), Math.Max(1, maxY - minY));
    }

    private void MoveToConfiguredCenter(double width, double height)
    {
        var screens = Forms.Screen.AllScreens;
        var index = ConfigService.Clamp(_config.SCREEN_INDEX, 0, Math.Max(0, screens.Length - 1));
        var bounds = screens.Length == 0 ? Forms.Screen.PrimaryScreen?.Bounds ?? new System.Drawing.Rectangle(0, 0, 1920, 1080) : screens[index].Bounds;

        Left = bounds.Left + _config.CENTER_X - (width / 2.0);
        Top = bounds.Top + _config.CENTER_Y - (height / 2.0);
    }

    private void UpdateClock(bool force, bool animate)
    {
        var values = CurrentValues();

        foreach (var slot in _slots)
        {
            var newValue = SlotValue(slot, values);
            _currentValuesBySlot.TryGetValue(slot.Id, out var oldValue);
            if (!force && oldValue == newValue)
            {
                continue;
            }

            PaintSlot(slot, oldValue, newValue, animate && oldValue is not null && oldValue != newValue);
            _currentValuesBySlot[slot.Id] = newValue;
        }

        ApplyZOrder();
    }

    private Dictionary<string, string> CurrentValues()
    {
        var now = DateTime.Now;
        var hour = _config.HOUR_MODE == "24h" ? now.ToString("HH", CultureInfo.InvariantCulture) : now.ToString("hh", CultureInfo.InvariantCulture);
        return new Dictionary<string, string>
        {
            ["hour"] = hour,
            ["minute"] = now.ToString("mm", CultureInfo.InvariantCulture),
            ["second"] = now.ToString("ss", CultureInfo.InvariantCulture),
        };
    }

    private string SlotValue(SlotLayout slot, IReadOnlyDictionary<string, string> values)
    {
        if (slot.Kind == "separator")
        {
            return _config.FONT_COLON_CHARACTER;
        }

        return values[slot.Group][slot.DigitIndex].ToString(CultureInfo.InvariantCulture);
    }

    private void PaintSlot(SlotLayout slot, string? oldValue, string newValue, bool animate)
    {
        if (!_slotCanvases.TryGetValue(slot.Id, out var canvas))
        {
            return;
        }

        canvas.Children.Clear();
        var targetOpacity = EffectiveOpacity(slot);

        var animationDurationMs = AnimationDurationFor(slot);
        if (animate && animationDurationMs > 0 && oldValue is not null)
        {
            var duration = TimeSpan.FromMilliseconds(animationDurationMs);
            var oldVisual = CreateTextVisual(slot, oldValue);
            oldVisual.Opacity = targetOpacity;
            var newVisual = CreateTextVisual(slot, newValue);
            newVisual.Opacity = 0;
            canvas.Children.Add(oldVisual);
            canvas.Children.Add(newVisual);
            AddPreviewSelection(canvas, slot);

            oldVisual.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(targetOpacity, 0, duration)
            {
                EasingFunction = CreateEasingFunction(),
                FillBehavior = FillBehavior.Stop,
            });

            var fadeIn = new DoubleAnimation(0, targetOpacity, duration)
            {
                EasingFunction = CreateEasingFunction(),
                FillBehavior = FillBehavior.HoldEnd,
            };
            fadeIn.Completed += (_, _) =>
            {
                if (_slotCanvases.TryGetValue(slot.Id, out var currentCanvas) && ReferenceEquals(canvas, currentCanvas))
                {
                    PaintSlot(slot, null, newValue, animate: false);
                }
            };
            newVisual.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            return;
        }

        var visual = CreateTextVisual(slot, newValue);
        visual.Opacity = targetOpacity;
        canvas.Children.Add(visual);
        AddPreviewSelection(canvas, slot);
    }

    private FrameworkElement CreateTextVisual(SlotLayout slot, string text)
    {
        var canvas = new Canvas
        {
            Width = slot.Width,
            Height = slot.Height,
            IsHitTestVisible = false,
            ClipToBounds = true,
        };

        if (string.IsNullOrEmpty(text))
        {
            return canvas;
        }

        var fontFamily = new MediaFontFamily(FontFamilyFor(slot));
        var typeface = new Typeface(
            fontFamily,
            _config.SLOT_FONT_ITALIC.GetValueOrDefault(slot.Id, false) ? FontStyles.Italic : FontStyles.Normal,
            _config.SLOT_FONT_BOLD.GetValueOrDefault(slot.Id, false) ? FontWeights.Bold : FontWeights.Normal,
            FontStretches.Normal);
        var dpi = VisualTreeHelper.GetDpi(this);
        var formatted = new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            WpfFlowDirection.LeftToRight,
            typeface,
            FontSizeFor(slot),
            MediaBrushes.White,
            dpi.PixelsPerDip);

        var geometry = formatted.BuildGeometry(new WpfPoint(0, 0));
        var bounds = geometry.Bounds;
        if (bounds.IsEmpty)
        {
            return canvas;
        }

        var textOffset = TextOffsetFor(slot);
        var paddingX = Math.Max(0, _config.FONT_PADDING_X);
        var paddingY = Math.Max(0, _config.FONT_PADDING_Y);
        var availableWidth = Math.Max(1, slot.Width - (paddingX * 2));
        var availableHeight = Math.Max(1, slot.Height - (paddingY * 2));
        var left = paddingX + ((availableWidth - bounds.Width) / 2.0) - bounds.Left + textOffset.x;
        var top = paddingY + ((availableHeight - bounds.Height) / 2.0) - bounds.Top + textOffset.y;

        var translated = geometry.Clone();
        translated.Transform = new TranslateTransform(left, top);
        var color = ConfigService.ParseMediaColor(ColorFor(slot), "#C1C1C1");
        var brush = new SolidColorBrush(color);
        var mode = RenderModeFor(slot);

        var path = new ShapePath
        {
            Data = translated,
            Fill = mode == "outline" ? null : brush,
            Stroke = mode == "filled" ? null : brush,
            StrokeThickness = Math.Max(1, _config.FONT_OUTLINE_WIDTH),
            StrokeLineJoin = PenLineJoin.Round,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            IsHitTestVisible = false,
        };
        canvas.Children.Add(path);
        return canvas;
    }

    private void AddPreviewSelection(Canvas canvas, SlotLayout slot)
    {
        if (!_previewMode ||
            !_config.PREVIEW_SHOW_SELECTION ||
            !string.Equals(_config.PREVIEW_SELECTED_SLOT, slot.Id, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var color = ConfigService.ParseMediaColor(_config.PREVIEW_SELECTION_COLOR, "#3DA5FF");
        var border = new Border
        {
            Width = slot.Width,
            Height = slot.Height,
            BorderBrush = new SolidColorBrush(color),
            BorderThickness = new Thickness(1),
            Opacity = 0.85,
            IsHitTestVisible = false,
        };
        Canvas.SetLeft(border, 0);
        Canvas.SetTop(border, 0);
        canvas.Children.Add(border);
    }

    private void ScheduleClockTimer()
    {
        _clockTimer.Stop();
        _clockTimer.Interval = TimeSpan.FromMilliseconds(NextClockDelay());
        _clockTimer.Start();
    }

    private void ScheduleReloadTimer()
    {
        _reloadTimer.Stop();
        _reloadTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(100, _config.CONFIG_RELOAD_INTERVAL_MS));
        _reloadTimer.Start();
    }

    private int NextClockDelay()
    {
        var configured = Math.Max(20, _config.UPDATE_INTERVAL_MS);
        if (!_config.SHOW_SECONDS)
        {
            return configured;
        }

        var now = DateTime.Now;
        var untilNextSecond = 1000 - now.Millisecond;
        return Math.Max(20, Math.Min(configured, untilNextSecond + 20));
    }

    private IEasingFunction? CreateEasingFunction()
    {
        return _config.ANIMATION_EASING switch
        {
            "Linear" => null,
            "OutCubic" => new CubicEase { EasingMode = EasingMode.EaseOut },
            "InCubic" => new CubicEase { EasingMode = EasingMode.EaseIn },
            "OutBack" => new BackEase { EasingMode = EasingMode.EaseOut },
            _ => new QuadraticEase { EasingMode = EasingMode.EaseInOut },
        };
    }

    private double EffectiveOpacity(SlotLayout slot)
    {
        var slotOpacity = _config.SLOT_OPACITIES.GetValueOrDefault(slot.Id, 1.0);
        return ConfigService.Clamp(slotOpacity, 0.0, 1.0);
    }

    private double FontSizeFor(SlotLayout slot)
    {
        return Math.Max(1, _config.SLOT_FONT_PIXEL_SIZES.GetValueOrDefault(slot.Id, _config.FONT_PIXEL_SIZE));
    }

    private string FontFamilyFor(SlotLayout slot)
    {
        return _config.SLOT_FONT_FAMILIES.GetValueOrDefault(slot.Id)
            ?? _config.FONT_FAMILY
            ?? _config.FONT_FALLBACK_FAMILY
            ?? "Segoe UI";
    }

    private string ColorFor(SlotLayout slot)
    {
        return _config.SLOT_FONT_COLORS.GetValueOrDefault(slot.Id) ?? _config.FONT_COLOR;
    }

    private string RenderModeFor(SlotLayout slot)
    {
        return _config.SLOT_RENDER_MODES.GetValueOrDefault(slot.Id, _config.FONT_RENDER_MODE);
    }

    private int AnimationDurationFor(SlotLayout slot)
    {
        return ConfigService.Clamp(
            _config.SLOT_ANIMATION_DURATIONS_MS.GetValueOrDefault(slot.Id, 130),
            0,
            5000);
    }

    private PointOffset TextOffsetFor(SlotLayout slot)
    {
        var offset = _config.SLOT_TEXT_OFFSETS.GetValueOrDefault(slot.Id, new PointOffset());
        return new PointOffset
        {
            x = offset.x + _config.FONT_OFFSET_X,
            y = offset.y + _config.FONT_OFFSET_Y,
        };
    }

    private void ApplyWindowStyles()
    {
        if (_hwnd == IntPtr.Zero)
        {
            return;
        }

        var style = Win32.GetWindowLongPtr(_hwnd, Win32.GWL_EXSTYLE).ToInt64();
        style |= Win32.WS_EX_LAYERED;
        style = _config.CLICK_THROUGH ? style | Win32.WS_EX_TRANSPARENT : style & ~Win32.WS_EX_TRANSPARENT;
        if (_config.SHOW_IN_TASKBAR)
        {
            style |= Win32.WS_EX_APPWINDOW;
            style &= ~Win32.WS_EX_TOOLWINDOW;
        }
        else
        {
            style |= Win32.WS_EX_TOOLWINDOW;
            style &= ~Win32.WS_EX_APPWINDOW;
        }

        Win32.SetWindowLongPtr(_hwnd, Win32.GWL_EXSTYLE, new IntPtr(style));
        Win32.SetWindowPos(
            _hwnd,
            IntPtr.Zero,
            0,
            0,
            0,
            0,
            Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE | Win32.SWP_FRAMECHANGED);
    }

    private void ApplyZOrder()
    {
        if (_hwnd == IntPtr.Zero)
        {
            return;
        }

        var insertAfter = _config.ALWAYS_ON_TOP
            ? Win32.HWND_TOPMOST
            : _config.ALWAYS_ON_BOTTOM
                ? Win32.HWND_BOTTOM
                : Win32.HWND_NOTOPMOST;

        Win32.SetWindowPos(
            _hwnd,
            insertAfter,
            0,
            0,
            0,
            0,
            Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE | Win32.SWP_SHOWWINDOW);
    }

    private static void SafeRun(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            ConfigService.LogError(ex);
        }
    }

    private sealed record SlotLayout(
        string Id,
        string Kind,
        double X,
        double Y,
        double Width,
        double Height,
        string Group,
        int DigitIndex);

    private sealed record SlotDimensions(double Width, double Height);

    private sealed class ClockLayout(List<SlotLayout> items, double width, double height)
    {
        public List<SlotLayout> Items { get; } = items;
        public double Width { get; set; } = width;
        public double Height { get; } = height;
    }
}
