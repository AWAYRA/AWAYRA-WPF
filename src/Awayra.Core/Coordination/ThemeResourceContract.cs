namespace Awayra.Core.Coordination;

public static class ThemeResourceContract
{
    public static IReadOnlyList<string> RequiredBrushKeys { get; } =
    [
        "PrimaryTextBrush",
        "SecondaryTextBrush",
        "MutedTextBrush",
        "DarkSurfaceBrush",
        "DarkSurfaceElevatedBrush",
        "DarkBorderBrush",
        "AccentBrush",
        "AccentHoverBrush",
        "AccentPressedBrush",
        "AccentTextBrush",
        "DisabledTextBrush",
        "LightWindowBackgroundBrush",
        "LightPrimaryTextBrush",
        "LightInputBackgroundBrush",
        "LightInputTextBrush",
        "ValidationBrush",
        "OverlayScrimBrush"
    ];

    public static IReadOnlyList<string> RequiredStyleKeys { get; } =
    [
        "Awayra.LightChromeWindow",
        "Awayra.Card",
        "Awayra.CardContent",
        "Awayra.DarkSurfaceResources",
        "Awayra.MutedText",
        "Awayra.InputTextBox",
        "Awayra.InputComboBox",
        "Awayra.PrimaryButton",
        "Awayra.SecondaryButton"
    ];

    public static IReadOnlyList<string> ForbiddenStyleSetterProperties { get; } =
    [
        "Resources"
    ];
}
