using System.Globalization;

namespace DesktopClock.Services;

public static class LocalizationService
{
    private static readonly Dictionary<string, Dictionary<string, string>> Values = new()
    {
        ["en"] = new()
        {
            ["EditorTitle"] = "Desktop Clock Editor",
            ["General"] = "General",
            ["Item"] = "Item",
            ["LivePreviewActive"] = "Live preview active",
            ["PreviewUpdated"] = "Preview updated",
            ["PreviewError"] = "Preview error",
            ["AppliedAt"] = "Applied at {0}",
            ["ApplyErrorTitle"] = "Apply error",
            ["ResetItem"] = "Reset item",
            ["ResetAll"] = "Reset all",
            ["Apply"] = "Apply",
            ["Position"] = "Position",
            ["CenterX"] = "Center X",
            ["CenterY"] = "Center Y",
            ["Monitor"] = "Monitor",
            ["Language"] = "Language",
            ["SystemLanguage"] = "System",
            ["English"] = "English",
            ["Portuguese"] = "Portuguese",
            ["Time"] = "Time",
            ["Layout"] = "Layout",
            ["Seconds"] = "Seconds",
            ["Separators"] = "Separators",
            ["HourMode"] = "Hour mode",
            ["SeparatorText"] = "Separator text",
            ["Spacing"] = "Spacing",
            ["DigitSpacing"] = "Digit spacing",
            ["SeparatorSpacing"] = "Separator spacing",
            ["GroupSpacing"] = "Group spacing",
            ["VerticalSpacing"] = "Vertical spacing",
            ["ColumnWidth"] = "Column width",
            ["ColumnAlign"] = "Column align",
            ["Transition"] = "Transition",
            ["AnimationCurve"] = "Animation curve",
            ["UpdateMs"] = "Update ms",
            ["Font"] = "Font",
            ["OutlineWidth"] = "Outline width",
            ["PaddingX"] = "Padding X",
            ["PaddingY"] = "Padding Y",
            ["OffsetX"] = "Offset X",
            ["OffsetY"] = "Offset Y",
            ["Window"] = "Window",
            ["ClickThrough"] = "Click through",
            ["AlwaysOnTop"] = "Always on top",
            ["AlwaysOnBottom"] = "Always on bottom",
            ["Taskbar"] = "Show in taskbar",
            ["StartWithWindows"] = "Start with Windows",
            ["KeepInTray"] = "Keep editor in tray",
            ["All"] = "All",
            ["TextSize"] = "Text size",
            ["Bold"] = "Bold",
            ["Italic"] = "Italic",
            ["Color"] = "Color",
            ["ItemColor"] = "Item color",
            ["RenderMode"] = "Render mode",
            ["Opacity"] = "Opacity",
            ["AnimationMs"] = "Animation ms",
            ["Width"] = "Width",
            ["Height"] = "Height",
            ["TextX"] = "Text X",
            ["TextY"] = "Text Y",
            ["PositionX"] = "Position X",
            ["PositionY"] = "Position Y",
            ["ShowEditor"] = "Show editor",
            ["Exit"] = "Exit",
            ["TrayText"] = "Desktop Clock Editor",
            ["SlotHourTens"] = "First hour digit",
            ["SlotHourOnes"] = "Second hour digit",
            ["SlotSeparator1"] = "Hour/minute separator",
            ["SlotMinuteTens"] = "First minute digit",
            ["SlotMinuteOnes"] = "Second minute digit",
            ["SlotSeparator2"] = "Minute/second separator",
            ["SlotSecondTens"] = "First second digit",
            ["SlotSecondOnes"] = "Second second digit",
        },
        ["pt-BR"] = new()
        {
            ["EditorTitle"] = "Editor do Desktop Clock",
            ["General"] = "Geral",
            ["Item"] = "Item",
            ["LivePreviewActive"] = "Previa ao vivo ativa",
            ["PreviewUpdated"] = "Previa atualizada",
            ["PreviewError"] = "Erro na previa",
            ["AppliedAt"] = "Aplicado em {0}",
            ["ApplyErrorTitle"] = "Erro ao aplicar",
            ["ResetItem"] = "Restaurar item",
            ["ResetAll"] = "Restaurar tudo",
            ["Apply"] = "Aplicar",
            ["Position"] = "Posicao",
            ["CenterX"] = "Centro X",
            ["CenterY"] = "Centro Y",
            ["Monitor"] = "Monitor",
            ["Language"] = "Idioma",
            ["SystemLanguage"] = "Sistema",
            ["English"] = "Ingles",
            ["Portuguese"] = "Portugues",
            ["Time"] = "Tempo",
            ["Layout"] = "Layout",
            ["Seconds"] = "Segundos",
            ["Separators"] = "Separadores",
            ["HourMode"] = "Modo hora",
            ["SeparatorText"] = "Separador texto",
            ["Spacing"] = "Espacamento",
            ["DigitSpacing"] = "Espaco digitos",
            ["SeparatorSpacing"] = "Espaco sep.",
            ["GroupSpacing"] = "Espaco grupos",
            ["VerticalSpacing"] = "Espaco vertical",
            ["ColumnWidth"] = "Largura coluna",
            ["ColumnAlign"] = "Alinh. coluna",
            ["Transition"] = "Transicao",
            ["AnimationCurve"] = "Animacao curva",
            ["UpdateMs"] = "Atualizacao ms",
            ["Font"] = "Fonte",
            ["OutlineWidth"] = "Largura contorno",
            ["PaddingX"] = "Padding X",
            ["PaddingY"] = "Padding Y",
            ["OffsetX"] = "Offset X",
            ["OffsetY"] = "Offset Y",
            ["Window"] = "Janela",
            ["ClickThrough"] = "Ignorar clique",
            ["AlwaysOnTop"] = "Sempre acima",
            ["AlwaysOnBottom"] = "Sempre abaixo",
            ["Taskbar"] = "Na barra de tarefas",
            ["StartWithWindows"] = "Iniciar com Windows",
            ["KeepInTray"] = "Manter no tray",
            ["All"] = "Todos",
            ["TextSize"] = "Tamanho texto",
            ["Bold"] = "Negrito",
            ["Italic"] = "Italico",
            ["Color"] = "Cor",
            ["ItemColor"] = "Cor do item",
            ["RenderMode"] = "Modo render",
            ["Opacity"] = "Opacidade",
            ["AnimationMs"] = "Animacao ms",
            ["Width"] = "Largura",
            ["Height"] = "Altura",
            ["TextX"] = "Texto X",
            ["TextY"] = "Texto Y",
            ["PositionX"] = "Posicao X",
            ["PositionY"] = "Posicao Y",
            ["ShowEditor"] = "Mostrar editor",
            ["Exit"] = "Sair",
            ["TrayText"] = "Editor do Desktop Clock",
            ["SlotHourTens"] = "Primeiro digito da hora",
            ["SlotHourOnes"] = "Segundo digito da hora",
            ["SlotSeparator1"] = "Separador hora/minuto",
            ["SlotMinuteTens"] = "Primeiro digito do minuto",
            ["SlotMinuteOnes"] = "Segundo digito do minuto",
            ["SlotSeparator2"] = "Separador minuto/segundo",
            ["SlotSecondTens"] = "Primeiro digito do segundo",
            ["SlotSecondOnes"] = "Segundo digito do segundo",
        },
    };

    public static string NormalizeLanguage(string? language)
    {
        if (string.Equals(language, "pt-BR", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(language, "pt", StringComparison.OrdinalIgnoreCase))
        {
            return "pt-BR";
        }

        return string.Equals(language, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "system";
    }

    public static string ResolveLanguage(string? language)
    {
        var normalized = NormalizeLanguage(language);
        if (normalized != "system")
        {
            return normalized;
        }

        return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("pt", StringComparison.OrdinalIgnoreCase)
            ? "pt-BR"
            : "en";
    }

    public static string Get(string language, string key)
    {
        var resolved = ResolveLanguage(language);
        return Values.TryGetValue(resolved, out var values) && values.TryGetValue(key, out var value)
            ? value
            : Values["en"].GetValueOrDefault(key, key);
    }

    public static IReadOnlyDictionary<string, string> SlotLabels(string language) => new Dictionary<string, string>
    {
        ["hour_tens"] = Get(language, "SlotHourTens"),
        ["hour_ones"] = Get(language, "SlotHourOnes"),
        ["separator_1"] = Get(language, "SlotSeparator1"),
        ["minute_tens"] = Get(language, "SlotMinuteTens"),
        ["minute_ones"] = Get(language, "SlotMinuteOnes"),
        ["separator_2"] = Get(language, "SlotSeparator2"),
        ["second_tens"] = Get(language, "SlotSecondTens"),
        ["second_ones"] = Get(language, "SlotSecondOnes"),
    };
}
