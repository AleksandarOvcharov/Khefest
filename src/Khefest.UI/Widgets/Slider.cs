using System.Numerics;
using Khefest.Graphics.LowLevel;
using Khefest.UI.Core;
using Khefest.UI.Rendering;
using Khefest.UI.Styling;

namespace Khefest.UI.Widgets;

/// <summary>
/// Interactive horizontal slider allowing continuous or discrete numeric input within a range.
/// </summary>
public sealed class Slider : Widget
{
    private float _minimum = 0f;
    private float _maximum = 1f;
    private float _value = 0f;

    public float Minimum
    {
        get => _minimum;
        set
        {
            _minimum = value;
            Value = Math.Clamp(_value, _minimum, _maximum);
        }
    }

    public float Maximum
    {
        get => _maximum;
        set
        {
            _maximum = value;
            Value = Math.Clamp(_value, _minimum, _maximum);
        }
    }

    public float Value
    {
        get => _value;
        set
        {
            float clamped = Math.Clamp(value, _minimum, _maximum);
            if (Math.Abs(_value - clamped) > 0.00001f)
            {
                _value = clamped;
                ValueChanged?.Invoke(this, _value);
            }
        }
    }

    public float NormalizedValue
    {
        get
        {
            float range = _maximum - _minimum;
            return range > 0f ? (_value - _minimum) / range : 0f;
        }
    }

    public float TrackHeight { get; set; } = 6f;
    public float ThumbWidth { get; set; } = 14f;
    public float ThumbHeight { get; set; } = 18f;

    public event Action<Slider, float>? ValueChanged;

    public Slider(float minimum = 0f, float maximum = 1f, float value = 0f)
    {
        Focusable = true;
        Height = 24f;
        _minimum = minimum;
        _maximum = maximum;
        _value = Math.Clamp(value, minimum, maximum);

        PointerPressed += (_, pos) => UpdateValueFromPosition(pos.X);
        PointerMoved += (_, pos) =>
        {
            if (IsPressed)
            {
                UpdateValueFromPosition(pos.X);
            }
        };
    }

    private void UpdateValueFromPosition(float mouseX)
    {
        float availableTrack = Bounds.Width - ThumbWidth;
        if (availableTrack <= 0f) return;

        float relativeX = mouseX - Bounds.X - (ThumbWidth * 0.5f);
        float norm = Math.Clamp(relativeX / availableTrack, 0f, 1f);
        Value = _minimum + norm * (_maximum - _minimum);
    }

    protected override Vector2 MeasureContent(Vector2 availableSize)
    {
        return new Vector2(120f, MathF.Max(TrackHeight, ThumbHeight));
    }

    protected override void RenderBackground(UIRenderer renderer, UITheme theme)
    {
        // Track background
        float trackY = Bounds.Y + (Bounds.Height - TrackHeight) * 0.5f;
        var trackRect = new UIRect(Bounds.X, trackY, Bounds.Width, TrackHeight);
        renderer.FillRectangle(trackRect, theme.InputBackground);
        renderer.DrawRectangle(trackRect, theme.InputBorder, 1f);

        // Filled active track portion
        float availableTrack = Bounds.Width - ThumbWidth;
        float thumbX = Bounds.X + (NormalizedValue * availableTrack);
        if (thumbX > Bounds.X)
        {
            var fillRect = new UIRect(Bounds.X, trackY, thumbX - Bounds.X, TrackHeight);
            renderer.FillRectangle(fillRect, theme.Accent);
        }

        // Draggable thumb handle
        float thumbY = Bounds.Y + (Bounds.Height - ThumbHeight) * 0.5f;
        var thumbRect = new UIRect(thumbX, thumbY, ThumbWidth, ThumbHeight);

        var thumbBg = IsPressed ? theme.AccentActive : (IsHovered ? theme.AccentHover : theme.Accent);
        renderer.FillRectangle(thumbRect, thumbBg);
        renderer.DrawRectangle(thumbRect, Color4.White, 1f);
    }
}
