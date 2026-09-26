using System.Numerics;
using Khefest.Graphics.LowLevel;
using Khefest.UI.Core;
using Khefest.UI.Rendering;
using Khefest.UI.Styling;

namespace Khefest.UI.Widgets;

/// <summary>
/// Displays the progress of an operation ranging from 0.0 to 1.0.
/// </summary>
public sealed class ProgressBar : Widget
{
    private float _value;
    public float Value
    {
        get => _value;
        set => _value = Math.Clamp(value, 0.0f, 1.0f);
    }

    public Color4? FillColor { get; set; }
    public Color4? TrackColor { get; set; }

    public ProgressBar(float value = 0.0f)
    {
        _value = Math.Clamp(value, 0f, 1f);
        Height = 16f;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Center;
    }

    protected override Vector2 MeasureContent(Vector2 availableSize)
    {
        return new Vector2(100f, Height ?? 16f);
    }

    protected override void RenderBackground(UIRenderer renderer, UITheme theme)
    {
        // Track
        var trackBg = TrackColor ?? theme.InputBackground;
        renderer.FillRectangle(Bounds, trackBg);
        renderer.DrawRectangle(Bounds, theme.InputBorder, 1f);

        // Filled progress
        if (_value > 0f)
        {
            float fillW = Bounds.Width * _value;
            var fillRect = new UIRect(Bounds.X, Bounds.Y, fillW, Bounds.Height);
            var fillBg = FillColor ?? theme.Accent;
            renderer.FillRectangle(fillRect, fillBg);
        }
    }
}
