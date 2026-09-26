using System.Numerics;
using Khefest.Graphics.LowLevel;
using Khefest.UI.Core;
using Khefest.UI.Rendering;
using Khefest.UI.Styling;

namespace Khefest.UI.Widgets;

/// <summary>
/// Displays read-only text with configurable styling and scaling.
/// </summary>
public sealed class Label : Widget
{
    private string _text = string.Empty;
    public string Text
    {
        get => _text;
        set => _text = value ?? string.Empty;
    }

    public Color4? TextColor { get; set; }
    public float FontScale { get; set; } = 1.0f;

    public Label()
    {
        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Center;
    }

    public Label(string text) : this()
    {
        Text = text;
    }

    protected override Vector2 MeasureContent(Vector2 availableSize)
    {
        // Default font estimation: ~9px per char width, 14px height
        float charWidth = 9f * FontScale;
        float lineHeight = 14f * FontScale;

        string[] lines = _text.Split('\n');
        float maxW = 0f;
        for (int i = 0; i < lines.Length; i++)
        {
            float w = lines[i].TrimEnd('\r').Length * charWidth;
            if (w > maxW) maxW = w;
        }

        return new Vector2(maxW, lines.Length * lineHeight);
    }

    protected override void RenderForeground(UIRenderer renderer, UITheme theme)
    {
        if (string.IsNullOrEmpty(_text)) return;

        var color = !IsEnabled ? theme.TextDisabled : (TextColor ?? theme.TextPrimary);
        renderer.DrawText(_text, Bounds.Position, color, FontScale);
    }
}
