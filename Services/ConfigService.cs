using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using DesktopClock.Models;
using MediaColor = System.Windows.Media.Color;

namespace DesktopClock.Services;

public static class ConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    public static string RootDirectory { get; } = ResolveRootDirectory();

    public static string DefaultConfigPath => Path.Combine(RootDirectory, "desktop-image-clock.json");

    public static string PreviewConfigPath => Path.Combine(RootDirectory, ".desktop-image-clock.preview.json");

    public static string ErrorLogPath => Path.Combine(RootDirectory, "clock-error.log");

    public static ClockConfig LoadConfig(string? path = null)
    {
        path ??= DefaultConfigPath;
        try
        {
            if (File.Exists(path))
            {
                var json = ReadAllTextShared(path);
                var loaded = JsonSerializer.Deserialize<ClockConfig>(json, JsonOptions);
                return EnsureShape(loaded);
            }
        }
        catch (Exception ex)
        {
            LogError(ex);
        }

        return ClockConfig.CreateDefault();
    }

    public static void SaveConfig(string path, ClockConfig config, bool includePreview)
    {
        var normalized = EnsureShape(config.Clone());
        var node = JsonSerializer.SerializeToNode(normalized, JsonOptions) ?? new JsonObject();
        if (!includePreview && node is JsonObject obj)
        {
            obj.Remove(nameof(ClockConfig.PREVIEW_SELECTED_SLOT));
            obj.Remove(nameof(ClockConfig.PREVIEW_SHOW_SELECTION));
            obj.Remove(nameof(ClockConfig.PREVIEW_SELECTION_COLOR));
        }

        var tempPath = path + ".tmp";
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? RootDirectory);
        File.WriteAllText(tempPath, node.ToJsonString(JsonOptions));
        if (File.Exists(path))
        {
            File.Replace(tempPath, path, null);
        }
        else
        {
            File.Move(tempPath, path);
        }
    }

    public static ClockConfig EnsureShape(ClockConfig? incoming)
    {
        var defaults = ClockConfig.CreateDefault();
        var config = incoming ?? defaults.Clone();

        config.HOUR_MODE = NormalizeChoice(config.HOUR_MODE, ["12h", "24h"], defaults.HOUR_MODE);
        config.HOUR_FORMAT = config.HOUR_MODE == "12h" ? "%I" : "%H";
        config.LAYOUT_MODE = NormalizeChoice(config.LAYOUT_MODE, ["horizontal", "vertical"], defaults.LAYOUT_MODE);
        config.VERTICAL_ALIGN = NormalizeChoice(config.VERTICAL_ALIGN, ["left", "center", "right"], defaults.VERTICAL_ALIGN);
        config.FONT_RENDER_MODE = NormalizeRenderMode(config.FONT_RENDER_MODE, defaults.FONT_RENDER_MODE);
        config.ANIMATION_EASING = NormalizeChoice(
            config.ANIMATION_EASING,
            ["InOutQuad", "Linear", "OutCubic", "InCubic", "OutBack"],
            defaults.ANIMATION_EASING);

        config.FONT_FAMILY = DefaultIfBlank(config.FONT_FAMILY, defaults.FONT_FAMILY);
        config.FONT_FALLBACK_FAMILY = DefaultIfBlank(config.FONT_FALLBACK_FAMILY, defaults.FONT_FALLBACK_FAMILY);
        config.FONT_COLOR = NormalizeHex(config.FONT_COLOR, defaults.FONT_COLOR);
        config.FONT_COLON_CHARACTER = string.IsNullOrEmpty(config.FONT_COLON_CHARACTER) ? ":" : config.FONT_COLON_CHARACTER;
        config.WINDOWS_STARTUP_NAME = DefaultIfBlank(config.WINDOWS_STARTUP_NAME, defaults.WINDOWS_STARTUP_NAME);
        if (string.Equals(config.WINDOWS_STARTUP_NAME, "DesktopImageClock", StringComparison.OrdinalIgnoreCase))
        {
            config.WINDOWS_STARTUP_NAME = defaults.WINDOWS_STARTUP_NAME;
        }

        config.CENTER_X = Clamp(config.CENTER_X, -10000, 10000);
        config.CENTER_Y = Clamp(config.CENTER_Y, -10000, 10000);
        config.SCREEN_INDEX = Clamp(config.SCREEN_INDEX, 0, 20);
        config.DIGIT_SPACING = Clamp(config.DIGIT_SPACING, -300, 300);
        config.SEPARATOR_SPACING = Clamp(config.SEPARATOR_SPACING, -300, 300);
        config.GROUP_SPACING = Clamp(config.GROUP_SPACING, -300, 500);
        config.VERTICAL_GROUP_SPACING = Clamp(config.VERTICAL_GROUP_SPACING, -300, 500);
        config.VERTICAL_COLUMN_WIDTH = Math.Max(1, config.VERTICAL_COLUMN_WIDTH);
        config.FONT_PIXEL_SIZE = Clamp(config.FONT_PIXEL_SIZE, 1, 1000);
        config.FONT_OUTLINE_WIDTH = Clamp(config.FONT_OUTLINE_WIDTH, 1, 80);
        config.FONT_PADDING_X = Clamp(config.FONT_PADDING_X, 0, 500);
        config.FONT_PADDING_Y = Clamp(config.FONT_PADDING_Y, 0, 500);
        config.FONT_OFFSET_X = Clamp(config.FONT_OFFSET_X, -1000, 1000);
        config.FONT_OFFSET_Y = Clamp(config.FONT_OFFSET_Y, -1000, 1000);
        config.ANIMATION_DURATION_MS = Clamp(config.ANIMATION_DURATION_MS, 0, 5000);
        config.UPDATE_INTERVAL_MS = Clamp(config.UPDATE_INTERVAL_MS, 20, 60000);
        config.WINDOW_OPACITY = Clamp(config.WINDOW_OPACITY, 0.0, 1.0);
        config.CONFIG_RELOAD_INTERVAL_MS = Clamp(config.CONFIG_RELOAD_INTERVAL_MS, 100, 10000);

        config.VERTICAL_GROUP_ALIGNMENTS ??= new Dictionary<string, string>();
        config.VERTICAL_GROUP_OFFSETS_X ??= new Dictionary<string, int>();
        foreach (var group in ClockConfig.Groups)
        {
            config.VERTICAL_GROUP_ALIGNMENTS[group.Name] = NormalizeChoice(
                config.VERTICAL_GROUP_ALIGNMENTS.GetValueOrDefault(group.Name),
                ["left", "center", "right"],
                defaults.VERTICAL_GROUP_ALIGNMENTS[group.Name]);
            config.VERTICAL_GROUP_OFFSETS_X.TryAdd(group.Name, defaults.VERTICAL_GROUP_OFFSETS_X[group.Name]);
        }

        config.SLOT_FONT_PIXEL_SIZES ??= new Dictionary<string, int>();
        config.SLOT_FONT_FAMILIES ??= new Dictionary<string, string>();
        config.SLOT_FONT_COLORS ??= new Dictionary<string, string>();
        config.SLOT_FONT_BOLD ??= new Dictionary<string, bool>();
        config.SLOT_FONT_ITALIC ??= new Dictionary<string, bool>();
        config.SLOT_RENDER_MODES ??= new Dictionary<string, string>();
        config.SLOT_OPACITIES ??= new Dictionary<string, double>();
        config.SLOT_ANIMATION_DURATIONS_MS ??= new Dictionary<string, int>();
        config.SLOT_WIDTHS ??= new Dictionary<string, int>();
        config.SLOT_HEIGHTS ??= new Dictionary<string, int>();
        config.SLOT_TEXT_OFFSETS ??= new Dictionary<string, PointOffset>();
        config.SLOT_POSITION_OFFSETS ??= new Dictionary<string, PointOffset>();

        foreach (var slotId in ClockConfig.SlotIds)
        {
            var defaultSize = defaults.SLOT_FONT_PIXEL_SIZES[slotId];
            var size = config.SLOT_FONT_PIXEL_SIZES.GetValueOrDefault(slotId, defaultSize);
            config.SLOT_FONT_PIXEL_SIZES[slotId] = Clamp(size, 1, 1000);
            config.SLOT_FONT_FAMILIES[slotId] = DefaultIfBlank(
                config.SLOT_FONT_FAMILIES.GetValueOrDefault(slotId),
                defaults.SLOT_FONT_FAMILIES[slotId]);
            config.SLOT_FONT_COLORS[slotId] = NormalizeHex(
                config.SLOT_FONT_COLORS.GetValueOrDefault(slotId),
                defaults.SLOT_FONT_COLORS[slotId]);
            config.SLOT_FONT_BOLD[slotId] = config.SLOT_FONT_BOLD.TryGetValue(slotId, out var bold)
                ? bold
                : config.FONT_BOLD;
            config.SLOT_FONT_ITALIC[slotId] = config.SLOT_FONT_ITALIC.TryGetValue(slotId, out var italic)
                ? italic
                : config.FONT_ITALIC;
            config.SLOT_RENDER_MODES[slotId] = NormalizeRenderMode(
                config.SLOT_RENDER_MODES.GetValueOrDefault(slotId),
                config.FONT_RENDER_MODE);
            config.SLOT_OPACITIES[slotId] = Clamp(config.SLOT_OPACITIES.GetValueOrDefault(slotId, 1.0), 0.0, 1.0);
            config.SLOT_ANIMATION_DURATIONS_MS[slotId] = Clamp(
                config.SLOT_ANIMATION_DURATIONS_MS.GetValueOrDefault(slotId, config.ANIMATION_DURATION_MS),
                0,
                5000);
            config.SLOT_WIDTHS[slotId] = Math.Max(1, config.SLOT_WIDTHS.GetValueOrDefault(slotId, defaults.SLOT_WIDTHS[slotId]));
            config.SLOT_HEIGHTS[slotId] = Math.Max(1, config.SLOT_HEIGHTS.GetValueOrDefault(slotId, defaults.SLOT_HEIGHTS[slotId]));
            config.SLOT_TEXT_OFFSETS[slotId] = NormalizeOffset(config.SLOT_TEXT_OFFSETS.GetValueOrDefault(slotId));
            config.SLOT_POSITION_OFFSETS[slotId] = NormalizeOffset(config.SLOT_POSITION_OFFSETS.GetValueOrDefault(slotId));
        }

        return config;
    }

    public static DateTime? GetLastWriteTimeUtc(string path)
    {
        return File.Exists(path) ? File.GetLastWriteTimeUtc(path) : null;
    }

    public static int MinimumSlotWidth(string slotId, int fontSize)
    {
        fontSize = Math.Max(1, fontSize);
        return slotId.StartsWith("separator", StringComparison.OrdinalIgnoreCase)
            ? Math.Max(1, (int)Math.Round(fontSize * 0.42) + 12)
            : Math.Max(1, (int)Math.Round(fontSize * 0.82) + 12);
    }

    public static int MinimumSlotHeight(int fontSize)
    {
        return Math.Max(1, fontSize + 12);
    }

    public static MediaColor ParseMediaColor(string? value, string fallback = "#FFFFFF")
    {
        var hex = NormalizeHex(value, fallback).TrimStart('#');
        return MediaColor.FromRgb(
            byte.Parse(hex[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(hex.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(hex.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
    }

    public static string NormalizeHex(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var hex = value.Trim();
        if (hex.StartsWith('#'))
        {
            hex = hex[1..];
        }

        if (hex.Length == 3)
        {
            hex = string.Concat(hex.Select(c => $"{c}{c}"));
        }

        if (hex.Length != 6 || !hex.All(Uri.IsHexDigit))
        {
            return fallback;
        }

        return "#" + hex.ToUpperInvariant();
    }

    public static int Clamp(int value, int minimum, int maximum)
    {
        return Math.Max(minimum, Math.Min(maximum, value));
    }

    public static double Clamp(double value, double minimum, double maximum)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return minimum;
        }

        return Math.Max(minimum, Math.Min(maximum, value));
    }

    public static void LogError(Exception ex)
    {
        try
        {
            File.AppendAllText(ErrorLogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}\n");
        }
        catch
        {
            // Logging must never crash the clock.
        }
    }

    private static string ReadAllTextShared(string path)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream);
                return reader.ReadToEnd();
            }
            catch (IOException) when (attempt < 4)
            {
                Thread.Sleep(25 * (attempt + 1));
            }
        }
    }

    private static string ResolveRootDirectory()
    {
        var cwd = Directory.GetCurrentDirectory();
        if (File.Exists(Path.Combine(cwd, "desktop-image-clock.json")) ||
            File.Exists(Path.Combine(cwd, "DesktopClock.csproj")))
        {
            return cwd;
        }

        return AppContext.BaseDirectory;
    }

    private static PointOffset NormalizeOffset(PointOffset? offset)
    {
        return new PointOffset
        {
            x = Clamp(offset?.x ?? 0, -5000, 5000),
            y = Clamp(offset?.y ?? 0, -5000, 5000),
        };
    }

    private static string NormalizeRenderMode(string? value, string fallback)
    {
        return NormalizeChoice(value, ["filled", "outline", "filled_outline"], fallback);
    }

    private static string NormalizeChoice(string? value, string[] allowed, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return allowed.Contains(value, StringComparer.OrdinalIgnoreCase)
            ? allowed.First(x => string.Equals(x, value, StringComparison.OrdinalIgnoreCase))
            : fallback;
    }

    private static string DefaultIfBlank(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }
}
