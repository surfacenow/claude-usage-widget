using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FloatingClockWidget.Native.Models;

public sealed class ThemeManifest
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("requiredAppVersion")]
    public string RequiredAppVersion { get; set; } = string.Empty;

    [JsonPropertyName("author")]
    public string Author { get; set; } = string.Empty;

    [JsonPropertyName("preview")]
    public string Preview { get; set; } = string.Empty;

    [JsonPropertyName("entryCss")]
    public string EntryCss { get; set; } = string.Empty;

    [JsonPropertyName("entryTokens")]
    public string EntryTokens { get; set; } = string.Empty;
}

public sealed class ThemeTokens
{
    [JsonPropertyName("colors")]
    public ThemeColors Colors { get; set; } = new();

    [JsonPropertyName("typography")]
    public ThemeTypography Typography { get; set; } = new();

    [JsonPropertyName("radius")]
    public ThemeRadius Radius { get; set; } = new();

    [JsonPropertyName("shadow")]
    public ThemeShadow Shadow { get; set; } = new();

    [JsonPropertyName("blur")]
    public ThemeBlur Blur { get; set; } = new();

    [JsonPropertyName("motion")]
    public ThemeMotion Motion { get; set; } = new();
}

public sealed class ThemeColors
{
    [JsonPropertyName("panel")]
    public string Panel { get; set; } = "#0B0B12DD";

    [JsonPropertyName("textMain")]
    public string TextMain { get; set; } = "#F7FDFF";

    [JsonPropertyName("textSub")]
    public string TextSub { get; set; } = "#9CB6C9";

    [JsonPropertyName("accentA")]
    public string AccentA { get; set; } = "#00E5FF";

    [JsonPropertyName("accentB")]
    public string AccentB { get; set; } = "#FF3DFF";

    [JsonPropertyName("accentC")]
    public string AccentC { get; set; } = "#C8FF3D";
}

public sealed class ThemeTypography
{
    [JsonPropertyName("clockFont")]
    public string ClockFont { get; set; } = "Segoe UI";

    [JsonPropertyName("uiFont")]
    public string UiFont { get; set; } = "Segoe UI";
}

public sealed class ThemeRadius
{
    [JsonPropertyName("panel")]
    public int Panel { get; set; } = 22;
}

public sealed class ThemeShadow
{
    [JsonPropertyName("panelGlow")]
    public string PanelGlow { get; set; } = "0 0 18px rgba(0,229,255,0.20)";
}

public sealed class ThemeBlur
{
    [JsonPropertyName("panelBackdrop")]
    public int PanelBackdrop { get; set; } = 10;
}

public sealed class ThemeMotion
{
    [JsonPropertyName("driftPx")]
    public int DriftPx { get; set; } = 4;

    [JsonPropertyName("driftCycleSec")]
    public double DriftCycleSec { get; set; } = 14;

    [JsonPropertyName("locatorPulseSec")]
    public double LocatorPulseSec { get; set; } = 8;
}

public sealed class ThemeSummary
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public string Author { get; init; } = string.Empty;
    public string DisplayLabel => $"{Name} ({Version})";
}

public sealed class ThemeBundle
{
    public ThemeManifest Manifest { get; init; } = new();
    public ThemeTokens Tokens { get; init; } = new();
}

public sealed class ThemeValidationCheck
{
    public string Id { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public bool Pass { get; init; }
    public string Detail { get; init; } = string.Empty;
}

public sealed class ThemeImportResult
{
    public bool Ok { get; init; }
    public string ErrorCode { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string ThemeId { get; init; } = string.Empty;
    public ThemeManifest? Manifest { get; init; }
    public ThemeTokens? Tokens { get; init; }
    public IReadOnlyList<ThemeValidationCheck> Checks { get; init; } = [];
}
