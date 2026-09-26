using System.Numerics;

namespace Khefest.Graphics.Mathematics;

/// <summary>
/// Represents a 2D axis-aligned floating-point rectangle.
/// </summary>
public readonly record struct Rect2D(float X, float Y, float Width, float Height)
{
    public static readonly Rect2D Empty = new(0f, 0f, 0f, 0f);

    public float Left => X;
    public float Top => Y;
    public float Right => X + Width;
    public float Bottom => Y + Height;

    public Vector2 Position => new(X, Y);
    public Vector2 Size => new(Width, Height);
    public Vector2 Center => new(X + Width * 0.5f, Y + Height * 0.5f);

    public Vector2 TopLeft => new(Left, Top);
    public Vector2 TopRight => new(Right, Top);
    public Vector2 BottomLeft => new(Left, Bottom);
    public Vector2 BottomRight => new(Right, Bottom);

    public bool IsEmpty => Width <= 0f || Height <= 0f;

    public Rect2D(Vector2 position, Vector2 size) : this(position.X, position.Y, size.X, size.Y)
    {
    }

    /// <summary>
    /// Creates a rectangle centered at a point with given width and height.
    /// </summary>
    public static Rect2D FromCenter(Vector2 center, Vector2 size)
    {
        return new Rect2D(center.X - size.X * 0.5f, center.Y - size.Y * 0.5f, size.X, size.Y);
    }

    /// <summary>
    /// Creates a rectangle bounding two corner points.
    /// </summary>
    public static Rect2D FromPoints(Vector2 min, Vector2 max)
    {
        float x = Math.Min(min.X, max.X);
        float y = Math.Min(min.Y, max.Y);
        float w = Math.Abs(max.X - min.X);
        float h = Math.Abs(max.Y - min.Y);
        return new Rect2D(x, y, w, h);
    }

    /// <summary>
    /// Determines whether the rectangle contains the specified point coordinates.
    /// </summary>
    public bool Contains(float px, float py)
    {
        return px >= Left && px <= Right && py >= Top && py <= Bottom;
    }

    /// <summary>
    /// Determines whether the rectangle contains the specified vector point.
    /// </summary>
    public bool Contains(Vector2 point)
    {
        return Contains(point.X, point.Y);
    }

    /// <summary>
    /// Determines whether this rectangle completely encloses another rectangle.
    /// </summary>
    public bool Contains(in Rect2D other)
    {
        return other.Left >= Left && other.Right <= Right && other.Top >= Top && other.Bottom <= Bottom;
    }

    /// <summary>
    /// Determines whether this rectangle overlaps another rectangle.
    /// </summary>
    public bool Intersects(in Rect2D other)
    {
        return Left < other.Right && Right > other.Left && Top < other.Bottom && Bottom > other.Top;
    }

    /// <summary>
    /// Returns the overlapping rectangle between this and another rectangle.
    /// </summary>
    public Rect2D Intersect(in Rect2D other) => Intersect(in this, in other);

    /// <summary>
    /// Returns the minimal rectangle bounding this and another rectangle.
    /// </summary>
    public Rect2D Union(in Rect2D other) => Union(in this, in other);

    /// <summary>
    /// Returns the overlapping rectangle between two intersecting rectangles.
    /// </summary>
    public static Rect2D Intersect(in Rect2D a, in Rect2D b)
    {
        float x = Math.Max(a.Left, b.Left);
        float y = Math.Max(a.Top, b.Top);
        float right = Math.Min(a.Right, b.Right);
        float bottom = Math.Min(a.Bottom, b.Bottom);

        if (right < x || bottom < y)
        {
            return Empty;
        }

        return new Rect2D(x, y, right - x, bottom - y);
    }

    /// <summary>
    /// Returns the minimal rectangle bounding both specified rectangles.
    /// </summary>
    public static Rect2D Union(in Rect2D a, in Rect2D b)
    {
        if (a.IsEmpty) return b;
        if (b.IsEmpty) return a;

        float left = Math.Min(a.Left, b.Left);
        float top = Math.Min(a.Top, b.Top);
        float right = Math.Max(a.Right, b.Right);
        float bottom = Math.Max(a.Bottom, b.Bottom);

        return new Rect2D(left, top, right - left, bottom - top);
    }

    /// <summary>
    /// Returns a new rectangle enlarged or shrunk by the specified horizontal and vertical amounts.
    /// </summary>
    public Rect2D Inflate(float horizontalAmount, float verticalAmount)
    {
        return new Rect2D(
            X - horizontalAmount,
            Y - verticalAmount,
            Width + horizontalAmount * 2f,
            Height + verticalAmount * 2f);
    }

    /// <summary>
    /// Returns a new rectangle offset by the specified distances.
    /// </summary>
    public Rect2D Offset(float offsetX, float offsetY)
    {
        return new Rect2D(X + offsetX, Y + offsetY, Width, Height);
    }

    /// <summary>
    /// Returns a new rectangle offset by the specified 2D vector.
    /// </summary>
    public Rect2D Offset(Vector2 offset)
    {
        return new Rect2D(X + offset.X, Y + offset.Y, Width, Height);
    }
}
