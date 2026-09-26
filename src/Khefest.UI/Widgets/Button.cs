using System.Numerics;
using Khefest.Graphics.LowLevel;
using Khefest.UI.Core;
using Khefest.UI.Rendering;
using Khefest.UI.Styling;

namespace Khefest.UI.Widgets;

/// <summary>
/// Interactive push button control responding to pointer hover, click, and activation events.
/// </summary>
public sealed class Button : Widget
{
    private string _text = string.Empty;
    public string Text
    {
        get => _text;
        set => _text = value ?? string.Empty;
    }

    public Color4? NormalColor { get; set; }
    public Color4? HoverColor { get; set; }
    public Color4? PressedColor { get; set; }
    public Color4? TextColor { get; set; }
    public float FontScale { get; set; } = 1.0f;

    public event Action<Button>? Clicked;

    public Button()
    {
        Focusable = true;
        Padding = new Thickness(12f, 6f);
        Height = 32f;
        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Center;

        PointerReleased += (_, _) =>
        {
            if (IsEnabled && IsHovered)
            {
                Clicked?.Invoke(this);
            }
        };
    }

    public Button(string text) : this()
    {
        Text = text;
    }

    protected override Vector2 MeasureContent(Vector2 availableSize)
    {
        float charWidth = 9f * FontScale;
        float lineHeight = 14f * FontScale;

        float textWidth = _text.Length * charWidth;
        return new Vector2(textWidth, lineHeight);
    }

    protected override void RenderBackground(UIRenderer renderer, UITheme theme)
    {
        Color4 bg;
        if (!IsEnabled)
        {
            bg = theme.ButtonDisabled;
        }
        else if (IsPressed)
        {
            bg = PressedColor ?? theme.ButtonPressed;
        }
        else if (IsHovered)
        {
            bg = HoverColor ?? theme.ButtonHover;
        }
        else
        {
            bg = NormalColor ?? theme.ButtonNormal;
        }

        renderer.FillRectangle(Bounds, bg);

        // Border
        Color4 border = IsFocused ? theme.Accent : theme.PanelBorder;
        renderer.DrawRectangle(Bounds, border, IsFocused ? 2f : theme.BorderThickness);
    }

    protected override void RenderForeground(UIRenderer renderer, UITheme theme)
    {
        if (string.IsNullOrEmpty(_text)) return;

        var textCol = !IsEnabled ? theme.TextDisabled : (TextColor ?? theme.TextPrimary);

        // Center text within button bounds
        float charWidth = 9f * FontScale;
        float textWidth = _text.Length * charWidth;
        float textHeight = 12f * FontScale;

        float textX = Bounds.X + MathF.Max(0f, (Bounds.Width - textWidth) * 0.5f);
        float textY = Bounds.Y + MathF.Max(0f, (Bounds.Height - textHeight) * 0.5f);

        renderer.DrawText(_text, new Vector2(textX, textY), textCol, FontScale);
    }
}
