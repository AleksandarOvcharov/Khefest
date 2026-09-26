using Khefest.Graphics.LowLevel;

namespace Khefest.UI.Styling;

/// <summary>
/// Configurable UI color and visual styling palette.
/// </summary>
public sealed class UITheme
{
    public Color4 Background { get; set; } = new(0.08f, 0.09f, 0.11f, 1.0f);
    public Color4 PanelBackground { get; set; } = new(0.12f, 0.14f, 0.18f, 0.92f);
    public Color4 PanelBorder { get; set; } = new(0.24f, 0.28f, 0.35f, 1.0f);

    public Color4 ButtonNormal { get; set; } = new(0.18f, 0.22f, 0.28f, 1.0f);
    public Color4 ButtonHover { get; set; } = new(0.25f, 0.32f, 0.42f, 1.0f);
    public Color4 ButtonPressed { get; set; } = new(0.14f, 0.18f, 0.24f, 1.0f);
    public Color4 ButtonDisabled { get; set; } = new(0.12f, 0.13f, 0.15f, 0.6f);

    public Color4 TextPrimary { get; set; } = Color4.White;
    public Color4 TextSecondary { get; set; } = new(0.70f, 0.75f, 0.82f, 1.0f);
    public Color4 TextDisabled { get; set; } = new(0.40f, 0.42f, 0.46f, 1.0f);

    public Color4 Accent { get; set; } = new(0.18f, 0.54f, 0.94f, 1.0f);
    public Color4 AccentHover { get; set; } = new(0.25f, 0.62f, 1.0f, 1.0f);
    public Color4 AccentActive { get; set; } = new(0.12f, 0.45f, 0.82f, 1.0f);

    public Color4 InputBackground { get; set; } = new(0.10f, 0.11f, 0.14f, 1.0f);
    public Color4 InputBorder { get; set; } = new(0.22f, 0.26f, 0.32f, 1.0f);
    public Color4 InputFocusedBorder { get; set; } = new(0.25f, 0.62f, 1.0f, 1.0f);

    public float BorderThickness { get; set; } = 1.0f;
    public float DefaultFontSize { get; set; } = 1.0f;

    public static UITheme Dark { get; } = new();

    public static UITheme Light { get; } = new()
    {
        Background = new(0.92f, 0.94f, 0.96f, 1.0f),
        PanelBackground = new(1.0f, 1.0f, 1.0f, 0.96f),
        PanelBorder = new(0.80f, 0.83f, 0.88f, 1.0f),
        ButtonNormal = new(0.88f, 0.90f, 0.94f, 1.0f),
        ButtonHover = new(0.80f, 0.85f, 0.92f, 1.0f),
        ButtonPressed = new(0.72f, 0.78f, 0.86f, 1.0f),
        TextPrimary = new(0.10f, 0.12f, 0.15f, 1.0f),
        TextSecondary = new(0.35f, 0.38f, 0.45f, 1.0f),
        Accent = new(0.12f, 0.45f, 0.85f, 1.0f),
        InputBackground = new(0.98f, 0.98f, 1.0f, 1.0f),
        InputBorder = new(0.75f, 0.78f, 0.84f, 1.0f)
    };
}
