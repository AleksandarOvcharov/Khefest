using System.Numerics;
using System.Runtime.CompilerServices;
using Khefest.UI.Core;

namespace Khefest.UI.Panels;

/// <summary>
/// Defines an area within which you can explicitly position child elements by coordinates relative to the Canvas area.
/// </summary>
public sealed class Canvas : Panel
{
    private static readonly ConditionalWeakTable<Widget, CanvasPosition> Positions = new();

    private sealed class CanvasPosition
    {
        public float X;
        public float Y;
    }

    public static void SetPosition(Widget widget, Vector2 position)
    {
        ArgumentNullException.ThrowIfNull(widget);
        var entry = Positions.GetOrCreateValue(widget);
        entry.X = position.X;
        entry.Y = position.Y;
    }

    public static void SetLeft(Widget widget, float left)
    {
        ArgumentNullException.ThrowIfNull(widget);
        Positions.GetOrCreateValue(widget).X = left;
    }

    public static void SetTop(Widget widget, float top)
    {
        ArgumentNullException.ThrowIfNull(widget);
        Positions.GetOrCreateValue(widget).Y = top;
    }

    public static Vector2 GetPosition(Widget widget)
    {
        ArgumentNullException.ThrowIfNull(widget);
        return Positions.TryGetValue(widget, out var entry) ? new Vector2(entry.X, entry.Y) : Vector2.Zero;
    }

    protected override Vector2 MeasureContent(Vector2 availableSize)
    {
        float maxRight = 0f;
        float maxBottom = 0f;

        for (int i = 0; i < Children.Count; i++)
        {
            var child = Children[i];
            if (!child.IsVisible) continue;

            var childDesired = child.Measure(availableSize);
            var pos = GetPosition(child);

            float right = pos.X + childDesired.X;
            float bottom = pos.Y + childDesired.Y;

            if (right > maxRight) maxRight = right;
            if (bottom > maxBottom) maxBottom = bottom;
        }

        return new Vector2(maxRight, maxBottom);
    }

    protected override void ArrangeChildren(in UIRect innerSlot)
    {
        for (int i = 0; i < Children.Count; i++)
        {
            var child = Children[i];
            if (!child.IsVisible) continue;

            var pos = GetPosition(child);
            float w = child.Width ?? child.DesiredSize.X;
            float h = child.Height ?? child.DesiredSize.Y;

            var slot = new UIRect(innerSlot.X + pos.X, innerSlot.Y + pos.Y, w, h);
            child.Arrange(slot);
        }
    }
}
