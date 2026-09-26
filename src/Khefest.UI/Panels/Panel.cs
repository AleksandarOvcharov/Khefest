using Khefest.Graphics.LowLevel;
using Khefest.UI.Core;
using Khefest.UI.Rendering;
using Khefest.UI.Styling;

namespace Khefest.UI.Panels;

/// <summary>
/// Container widget with styled background fill, border outline, and internal padding.
/// </summary>
public class Panel : Widget
{
    public Color4? BackgroundColor { get; set; }
    public Color4? BorderColor { get; set; }
    public float BorderThickness { get; set; } = 1.0f;

    protected override void RenderBackground(UIRenderer renderer, UITheme theme)
    {
        var bg = BackgroundColor ?? theme.PanelBackground;
        renderer.FillRectangle(Bounds, bg);

        if (BorderThickness > 0)
        {
            var border = BorderColor ?? theme.PanelBorder;
            renderer.DrawRectangle(Bounds, border, BorderThickness);
        }
    }
}
