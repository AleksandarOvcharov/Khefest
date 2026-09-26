using System.Numerics;
using Khefest.UI.Core;

namespace Khefest.UI.Panels;

/// <summary>
/// Arranges child elements sequentially in a single horizontal or vertical line with spacing.
/// </summary>
public sealed class StackPanel : Panel
{
    public Orientation Orientation { get; set; } = Orientation.Vertical;
    public float Spacing { get; set; } = 4.0f;

    protected override Vector2 MeasureContent(Vector2 availableSize)
    {
        float totalMajor = 0.0f;
        float maxMinor = 0.0f;
        int visibleCount = 0;

        for (int i = 0; i < Children.Count; i++)
        {
            var child = Children[i];
            if (!child.IsVisible) continue;

            visibleCount++;
            var childDesired = child.Measure(availableSize);

            if (Orientation == Orientation.Vertical)
            {
                totalMajor += childDesired.Y;
                if (childDesired.X > maxMinor) maxMinor = childDesired.X;
            }
            else
            {
                totalMajor += childDesired.X;
                if (childDesired.Y > maxMinor) maxMinor = childDesired.Y;
            }
        }

        if (visibleCount > 1)
        {
            totalMajor += Spacing * (visibleCount - 1);
        }

        return Orientation == Orientation.Vertical
            ? new Vector2(maxMinor, totalMajor)
            : new Vector2(totalMajor, maxMinor);
    }

    protected override void ArrangeChildren(in UIRect innerSlot)
    {
        float currentMajor = Orientation == Orientation.Vertical ? innerSlot.Y : innerSlot.X;

        for (int i = 0; i < Children.Count; i++)
        {
            var child = Children[i];
            if (!child.IsVisible) continue;

            if (Orientation == Orientation.Vertical)
            {
                float childHeight = child.DesiredSize.Y;
                var slot = new UIRect(innerSlot.X, currentMajor, innerSlot.Width, childHeight);
                child.Arrange(slot);
                currentMajor += childHeight + Spacing;
            }
            else
            {
                float childWidth = child.DesiredSize.X;
                var slot = new UIRect(currentMajor, innerSlot.Y, childWidth, innerSlot.Height);
                child.Arrange(slot);
                currentMajor += childWidth + Spacing;
            }
        }
    }
}
