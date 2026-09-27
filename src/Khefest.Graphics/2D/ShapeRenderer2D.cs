using System.Numerics;
using Khefest.Graphics.LowLevel;

namespace Khefest.Graphics.TwoD;

/// <summary>
/// High-level 2D shape drawing extension methods and utilities.
/// Automatically integrates with <see cref="SpriteBatch"/> to batch vector shapes with zero state overhead.
/// </summary>
public static class ShapeRenderer2D
{
    /// <summary>
    /// Draws a line between two points with the specified thickness and color.
    /// </summary>
    public static void DrawLine(
        this SpriteBatch batch,
        Vector2 start,
        Vector2 end,
        Color4 color,
        float thickness = 1.0f)
    {
        ArgumentNullException.ThrowIfNull(batch);
        var diff = end - start;
        var length = diff.Length();
        if (length < 0.0001f) return;

        var angle = MathF.Atan2(diff.Y, diff.X);
        batch.Draw(
            batch.WhiteTexture,
            start,
            new Vector2(length, thickness),
            color,
            angle,
            new Vector2(0.0f, thickness * 0.5f),
            SpriteEffects.None);
    }

    /// <summary>
    /// Draws an axis-aligned outline rectangle.
    /// </summary>
    public static void DrawRectangle(
        this SpriteBatch batch,
        Vector2 position,
        Vector2 size,
        Color4 color,
        float thickness = 1.0f)
    {
        ArgumentNullException.ThrowIfNull(batch);
        var halfThick = thickness * 0.5f;

        // Top line
        batch.DrawLine(
            new Vector2(position.X - halfThick, position.Y),
            new Vector2(position.X + size.X + halfThick, position.Y),
            color,
            thickness);

        // Bottom line
        batch.DrawLine(
            new Vector2(position.X - halfThick, position.Y + size.Y),
            new Vector2(position.X + size.X + halfThick, position.Y + size.Y),
            color,
            thickness);

        // Left line
        batch.DrawLine(
            new Vector2(position.X, position.Y),
            new Vector2(position.X, position.Y + size.Y),
            color,
            thickness);

        // Right line
        batch.DrawLine(
            new Vector2(position.X + size.X, position.Y),
            new Vector2(position.X + size.X, position.Y + size.Y),
            color,
            thickness);
    }

    /// <summary>
    /// Draws a filled rectangle with the specified color.
    /// </summary>
    public static void FillRectangle(
        this SpriteBatch batch,
        Vector2 position,
        Vector2 size,
        Color4 color)
    {
        ArgumentNullException.ThrowIfNull(batch);
        batch.Draw(batch.WhiteTexture, position, size, color);
    }

    /// <summary>
    /// Draws an oriented filled rectangle with rotation and origin.
    /// </summary>
    public static void FillRectangle(
        this SpriteBatch batch,
        Vector2 position,
        Vector2 size,
        Color4 color,
        float rotation,
        Vector2 origin)
    {
        ArgumentNullException.ThrowIfNull(batch);
        batch.Draw(
            batch.WhiteTexture,
            position,
            size,
            color,
            rotation,
            origin,
            SpriteEffects.None);
    }

    /// <summary>
    /// Draws an outline circle.
    /// </summary>
    public static void DrawCircle(
        this SpriteBatch batch,
        Vector2 center,
        float radius,
        Color4 color,
        int segments = 32,
        float thickness = 1.0f)
    {
        ArgumentNullException.ThrowIfNull(batch);
        if (segments < 3) segments = 3;

        float step = MathF.Tau / segments;
        Vector2 prev = center + new Vector2(radius, 0.0f);

        for (int i = 1; i <= segments; i++)
        {
            float theta = i * step;
            Vector2 next = center + new Vector2(radius * MathF.Cos(theta), radius * MathF.Sin(theta));
            batch.DrawLine(prev, next, color, thickness);
            prev = next;
        }
    }

    /// <summary>
    /// Draws a filled circle approximated by horizontal scanline slices without polygon overlap artifacts.
    /// </summary>
    public static void FillCircle(
        this SpriteBatch batch,
        Vector2 center,
        float radius,
        Color4 color,
        int segments = 32)
    {
        ArgumentNullException.ThrowIfNull(batch);
        if (radius <= 0.0f) return;

        int slices = Math.Clamp(segments, 8, 256);
        float sliceHeight = (radius * 2.0f) / slices;
        float rSq = radius * radius;

        for (int i = 0; i < slices; i++)
        {
            float yRel = -radius + (i + 0.5f) * sliceHeight;
            float halfWidth = MathF.Sqrt(Math.Max(0.0f, rSq - yRel * yRel));

            var pos = new Vector2(center.X - halfWidth, center.Y + yRel - sliceHeight * 0.5f);
            var size = new Vector2(halfWidth * 2.0f, sliceHeight);
            batch.FillRectangle(pos, size, color);
        }
    }

    /// <summary>
    /// Draws an outline triangle between three points.
    /// </summary>
    public static void DrawTriangle(
        this SpriteBatch batch,
        Vector2 a,
        Vector2 b,
        Vector2 c,
        Color4 color,
        float thickness = 1.0f)
    {
        ArgumentNullException.ThrowIfNull(batch);
        batch.DrawLine(a, b, color, thickness);
        batch.DrawLine(b, c, color, thickness);
        batch.DrawLine(c, a, color, thickness);
    }
}
