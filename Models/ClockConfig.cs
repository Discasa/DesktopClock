using System.Text.Json;

namespace DesktopClock.Models;

public sealed class ClockConfig
{
    public int CENTER_X { get; set; } = 960;
    public int CENTER_Y { get; set; } = 340;
    public int SCREEN_INDEX { get; set; } = 0;
    public bool SHOW_SECONDS { get; set; } = true;
    public bool SHOW_SEPARATORS { get; set; } = false;
    public string LAYOUT_MODE { get; set; } = "vertical";
    public string HOUR_MODE { get; set; } = "12h";
    public string HOUR_FORMAT { get; set; } = "%I";

    public int DIGIT_WIDTH { get; set; } = 56;
    public int DIGIT_HEIGHT { get; set; } = 80;
    public int SEPARATOR_WIDTH { get; set; } = 34;
    public int SEPARATOR_HEIGHT { get; set; } = 112;
    public int DIGIT_SPACING { get; set; } = 2;
    public int SEPARATOR_SPACING { get; set; } = 10;
    public int GROUP_SPACING { get; set; } = 18;
    public int VERTICAL_GROUP_SPACING { get; set; } = 6;
    public int VERTICAL_COLUMN_WIDTH { get; set; } = 114;
    public string VERTICAL_ALIGN { get; set; } = "center";
    public Dictionary<string, string> VERTICAL_GROUP_ALIGNMENTS { get; set; } = new();
    public Dictionary<string, int> VERTICAL_GROUP_OFFSETS_X { get; set; } = new();

    public string FONT_FAMILY { get; set; } = "Segoe UI";
    public string FONT_FALLBACK_FAMILY { get; set; } = "Segoe UI";
    public string FONT_COLOR { get; set; } = "#C1C1C1";
    public bool FONT_BOLD { get; set; } = false;
    public bool FONT_ITALIC { get; set; } = false;
    public string FONT_RENDER_MODE { get; set; } = "filled";
    public int FONT_OUTLINE_WIDTH { get; set; } = 1;
    public int FONT_PADDING_X { get; set; } = 0;
    public int FONT_PADDING_Y { get; set; } = 0;
    public int FONT_OFFSET_X { get; set; } = 0;
    public int FONT_OFFSET_Y { get; set; } = 0;
    public string FONT_COLON_CHARACTER { get; set; } = ":";
    public int FONT_PIXEL_SIZE { get; set; } = 60;

    public Dictionary<string, int> SLOT_FONT_PIXEL_SIZES { get; set; } = new();
    public Dictionary<string, string> SLOT_FONT_FAMILIES { get; set; } = new();
    public Dictionary<string, string> SLOT_FONT_COLORS { get; set; } = new();
    public Dictionary<string, string> SLOT_RENDER_MODES { get; set; } = new();
    public Dictionary<string, double> SLOT_OPACITIES { get; set; } = new();
    public Dictionary<string, int> SLOT_WIDTHS { get; set; } = new();
    public Dictionary<string, int> SLOT_HEIGHTS { get; set; } = new();
    public Dictionary<string, PointOffset> SLOT_TEXT_OFFSETS { get; set; } = new();
    public Dictionary<string, PointOffset> SLOT_POSITION_OFFSETS { get; set; } = new();

    public int ANIMATION_DURATION_MS { get; set; } = 130;
    public string ANIMATION_EASING { get; set; } = "InOutQuad";
    public int UPDATE_INTERVAL_MS { get; set; } = 1000;
    public double WINDOW_OPACITY { get; set; } = 1.0;
    public bool CLICK_THROUGH { get; set; } = true;
    public bool ALWAYS_ON_TOP { get; set; } = false;
    public bool ALWAYS_ON_BOTTOM { get; set; } = true;
    public bool SHOW_IN_TASKBAR { get; set; } = false;
    public bool START_WITH_WINDOWS { get; set; } = true;
    public string WINDOWS_STARTUP_NAME { get; set; } = "Desktop Clock";
    public int CONFIG_RELOAD_INTERVAL_MS { get; set; } = 250;

    public string PREVIEW_SELECTED_SLOT { get; set; } = "";
    public bool PREVIEW_SHOW_SELECTION { get; set; } = true;
    public string PREVIEW_SELECTION_COLOR { get; set; } = "#3DA5FF";

    public static readonly string[] SlotIds =
    [
        "hour_tens",
        "hour_ones",
        "separator_1",
        "minute_tens",
        "minute_ones",
        "separator_2",
        "second_tens",
        "second_ones",
    ];

    public static readonly IReadOnlyDictionary<string, string> SlotLabels = new Dictionary<string, string>
    {
        ["hour_tens"] = "Primeiro digito da hora",
        ["hour_ones"] = "Segundo digito da hora",
        ["separator_1"] = "Separador hora/minuto",
        ["minute_tens"] = "Primeiro digito do minuto",
        ["minute_ones"] = "Segundo digito do minuto",
        ["separator_2"] = "Separador minuto/segundo",
        ["second_tens"] = "Primeiro digito do segundo",
        ["second_ones"] = "Segundo digito do segundo",
    };

    public static readonly ClockGroup[] Groups =
    [
        new("hour", ["hour_tens", "hour_ones"]),
        new("minute", ["minute_tens", "minute_ones"]),
        new("second", ["second_tens", "second_ones"]),
    ];

    public static ClockConfig CreateDefault()
    {
        var config = new ClockConfig
        {
            CENTER_X = 960,
            CENTER_Y = 340,
            SCREEN_INDEX = 0,
            SHOW_SECONDS = true,
            SHOW_SEPARATORS = false,
            LAYOUT_MODE = "vertical",
            HOUR_MODE = "12h",
            HOUR_FORMAT = "%I",
            DIGIT_SPACING = 2,
            SEPARATOR_SPACING = 10,
            GROUP_SPACING = 18,
            VERTICAL_GROUP_SPACING = 6,
            VERTICAL_COLUMN_WIDTH = 114,
            VERTICAL_ALIGN = "center",
            FONT_FAMILY = "Segoe UI",
            FONT_FALLBACK_FAMILY = "Segoe UI",
            FONT_COLOR = "#C1C1C1",
            FONT_RENDER_MODE = "filled",
            FONT_PIXEL_SIZE = 60,
            ANIMATION_DURATION_MS = 130,
            ANIMATION_EASING = "InOutQuad",
            UPDATE_INTERVAL_MS = 1000,
            WINDOW_OPACITY = 1.0,
            CLICK_THROUGH = true,
            ALWAYS_ON_TOP = false,
            ALWAYS_ON_BOTTOM = true,
            SHOW_IN_TASKBAR = false,
            START_WITH_WINDOWS = true,
            WINDOWS_STARTUP_NAME = "Desktop Clock",
            CONFIG_RELOAD_INTERVAL_MS = 250,
        };

        foreach (var group in new[] { "hour", "minute", "second" })
        {
            config.VERTICAL_GROUP_ALIGNMENTS[group] = "center";
            config.VERTICAL_GROUP_OFFSETS_X[group] = 0;
        }

        foreach (var slotId in SlotIds)
        {
            var isSeparator = slotId.StartsWith("separator", StringComparison.OrdinalIgnoreCase);
            config.SLOT_FONT_PIXEL_SIZES[slotId] = 138;
            config.SLOT_FONT_FAMILIES[slotId] = "Segoe UI Black";
            config.SLOT_FONT_COLORS[slotId] = "#C1C1C1";
            config.SLOT_RENDER_MODES[slotId] = "filled";
            config.SLOT_OPACITIES[slotId] = 1.0;
            config.SLOT_WIDTHS[slotId] = isSeparator ? 91 : 127;
            config.SLOT_HEIGHTS[slotId] = isSeparator ? 156 : 152;
            config.SLOT_TEXT_OFFSETS[slotId] = new PointOffset();
            config.SLOT_POSITION_OFFSETS[slotId] = new PointOffset();
        }

        return config;
    }

    public ClockConfig Clone()
    {
        return JsonSerializer.Deserialize<ClockConfig>(JsonSerializer.Serialize(this)) ?? CreateDefault();
    }
}

public sealed record ClockGroup(string Name, string[] Slots);

public sealed class PointOffset
{
    public int x { get; set; }
    public int y { get; set; }

    public PointOffset Clone()
    {
        return new PointOffset { x = x, y = y };
    }
}
