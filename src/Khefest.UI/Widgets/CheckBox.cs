using System.Numerics;
using Khefest.Graphics.LowLevel;
using Khefest.UI.Core;
using Khefest.UI.Rendering;
using Khefest.UI.Styling;

namespace Khefest.UI.Widgets;

/// <summary>
/// Toggle control that allows the user to select or deselect a boolean option.
/// </summary>
public sealed class CheckBox : Widget
{
    private bool _isChecked;
    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            if (_isChecked != value)
            {
                _isChecked = value;
                CheckedChanged?.Invoke(this, _isChecked);
            }
        }
    }

    private string _text = string.Empty;
    public string Text
    {
        get => _text;
        set => _text = value ?? string.Empty;
    }

    public float BoxSize { get; set; } = 18f;
    public float FontScale { get; set; } = 1.0f;
    public event Action<CheckBox, bool>? CheckedChanged;

    public CheckBox()
    {
        Focusable = true;
        Height = 24f;
        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Center;

        PointerReleased += (_, _) =>
        {
            if (IsEnabled && IsHovered)
            {
                IsChecked = !IsChecked;
            }
        };
    }

    public CheckBox(string text, bool isChecked = false) : this()
    {
        Text = text;
        IsChecked = isChecked;
    }

    protected override Vector2 MeasureContent(Vector2 availableSize)
    {
        float textW = _text.Length * 9f * FontScale;
        float spacing = 8f;
        return new Vector2(BoxSize + spacing + textW, MathF.Max(BoxSize, 14f * FontScale));
    }

    protected override void RenderBackground(UIRenderer renderer, UITheme theme)
    {
        float boxY = Bounds.Y + (Bounds.Height - BoxSize) * 0.5f;
        var boxRect = new UIRect(Bounds.X, boxY, BoxSize, BoxSize);

        // Box background
        var bg = IsHovered ? theme.ButtonHover : theme.InputBackground;
        renderer.FillRectangle(boxRect, bg);

        // Box outline
        var border = IsFocused ? theme.Accent : theme.InputBorder;
        renderer.DrawRectangle(boxRect, border, 1f);

        // Checkmark fill
        if (_isChecked)
        {
            var innerRect = boxRect.Deflate(new Thickness(3.5f));
            renderer.FillRectangle(innerRect, theme.Accent);
        }
    }

    protected override void RenderForeground(UIRenderer renderer, UITheme theme)
    {
        if (string.IsNullOrEmpty(_text)) return;

        float textX = Bounds.X + BoxSize + 8f;
        float textY = Bounds.Y + (Bounds.Height - 12f * FontScale) * 0.5f;

        var color = !IsEnabled ? theme.TextDisabled : theme.TextPrimary;
        renderer.DrawText(_text, new Vector2(textX, textY), color, FontScale);
    }
}
