using System.Numerics;

namespace Khefest.UI.Core;

/// <summary>
/// Specifies horizontal alignment for UI elements.
/// </summary>
public enum HorizontalAlignment
{
    Left,
    Center,
    Right,
    Stretch
}

/// <summary>
/// Specifies vertical alignment for UI elements.
/// </summary>
public enum VerticalAlignment
{
    Top,
    Center,
    Bottom,
    Stretch
}

/// <summary>
/// Specifies layout orientation for stacked elements.
/// </summary>
public enum Orientation
{
    Horizontal,
    Vertical
}

/// <summary>
/// Defines margin or padding thickness around an element.
/// </summary>
public readonly record struct Thickness(float Left, float Top, float Right, float Bottom)
{
    public float Horizontal => Left + Right;
    public float Vertical => Top + Bottom;

    public Thickness(float uniform) : this(uniform, uniform, uniform, uniform) { }
    public Thickness(float horizontal, float vertical) : this(horizontal, vertical, horizontal, vertical) { }

    public static readonly Thickness Zero = new(0f);
}

/// <summary>
/// Lightweight 2D floating-point rectangle for UI bounds and hit testing.
/// </summary>
public readonly record struct UIRect(float X, float Y, float Width, float Height)
{
    public float Left => X;
    public float Top => Y;
    public float Right => X + Width;
    public float Bottom => Y + Height;
    public Vector2 Position => new(X, Y);
    public Vector2 Size => new(Width, Height);

    public bool Contains(Vector2 point)
    {
        return point.X >= X && point.X <= Right &&
               point.Y >= Y && point.Y <= Bottom;
    }

    public UIRect Deflate(Thickness thickness)
    {
        float x = X + thickness.Left;
        float y = Top + thickness.Top;
        float w = MathF.Max(0f, Width - thickness.Horizontal);
        float h = MathF.Max(0f, Height - thickness.Vertical);
        return new UIRect(x, y, w, h);
    }

    public static readonly UIRect Zero = new(0, 0, 0, 0);
}
