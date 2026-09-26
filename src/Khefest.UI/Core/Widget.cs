using System.Numerics;
using Khefest.Input;
using Khefest.UI.Rendering;
using Khefest.UI.Styling;

namespace Khefest.UI.Core;

/// <summary>
/// Root abstract base class for all user interface elements and controls.
/// Supports hierarchical composition, layout measuring/arranging, hit testing, styling, and event handling.
/// </summary>
public abstract class Widget
{
    private readonly List<Widget> _children = [];

    public Widget? Parent { get; internal set; }
    public IReadOnlyList<Widget> Children => _children;

    // Layout configuration
    public float? Width { get; set; }
    public float? Height { get; set; }
    public Thickness Margin { get; set; } = Thickness.Zero;
    public Thickness Padding { get; set; } = Thickness.Zero;
    public HorizontalAlignment HorizontalAlignment { get; set; } = HorizontalAlignment.Stretch;
    public VerticalAlignment VerticalAlignment { get; set; } = VerticalAlignment.Stretch;

    // Computed layout
    public Vector2 DesiredSize { get; protected set; }
    public UIRect Bounds { get; internal set; }
    public float ActualWidth => Bounds.Width;
    public float ActualHeight => Bounds.Height;

    // State
    public bool IsVisible { get; set; } = true;
    public bool IsEnabled { get; set; } = true;
    public bool IsHovered { get; internal set; }
    public bool IsPressed { get; internal set; }
    public bool IsFocused { get; internal set; }
    public bool Focusable { get; set; }

    // Events
    public event Action<Widget>? PointerEntered;
    public event Action<Widget>? PointerExited;
    public event Action<Widget, Vector2>? PointerPressed;
    public event Action<Widget, Vector2>? PointerReleased;
    public event Action<Widget, Vector2>? PointerMoved;
    public event Action<Widget, Key>? KeyDown;
    public event Action<Widget, Key>? KeyUp;
    public event Action<Widget, char>? CharInput;
    public event Action<Widget>? FocusGained;
    public event Action<Widget>? FocusLost;

    public void AddChild(Widget child)
    {
        ArgumentNullException.ThrowIfNull(child);
        if (child.Parent != null)
        {
            child.Parent.RemoveChild(child);
        }
        child.Parent = this;
        _children.Add(child);
    }

    public bool RemoveChild(Widget child)
    {
        ArgumentNullException.ThrowIfNull(child);
        if (_children.Remove(child))
        {
            child.Parent = null;
            return true;
        }
        return false;
    }

    public void ClearChildren()
    {
        foreach (var child in _children)
        {
            child.Parent = null;
        }
        _children.Clear();
    }

    /// <summary>
    /// Measures the size requirements of the element and its children.
    /// </summary>
    public virtual Vector2 Measure(Vector2 availableSize)
    {
        float targetW = Width ?? 0f;
        float targetH = Height ?? 0f;

        Vector2 contentSize = MeasureContent(new Vector2(
            MathF.Max(0f, availableSize.X - Margin.Horizontal - Padding.Horizontal),
            MathF.Max(0f, availableSize.Y - Margin.Vertical - Padding.Vertical)));

        float finalW = Width ?? (contentSize.X + Padding.Horizontal);
        float finalH = Height ?? (contentSize.Y + Padding.Vertical);

        DesiredSize = new Vector2(finalW + Margin.Horizontal, finalH + Margin.Vertical);
        return DesiredSize;
    }

    protected virtual Vector2 MeasureContent(Vector2 availableSize)
    {
        float maxW = 0f;
        float maxH = 0f;

        for (int i = 0; i < _children.Count; i++)
        {
            if (!_children[i].IsVisible) continue;
            var childDesired = _children[i].Measure(availableSize);
            if (childDesired.X > maxW) maxW = childDesired.X;
            if (childDesired.Y > maxH) maxH = childDesired.Y;
        }

        return new Vector2(maxW, maxH);
    }

    /// <summary>
    /// Positions and dimensions the element within the parent allocated slot.
    /// </summary>
    public virtual void Arrange(in UIRect finalSlot)
    {
        // Subtract margins
        var contentSlot = finalSlot.Deflate(Margin);

        float w = Width ?? contentSlot.Width;
        float h = Height ?? contentSlot.Height;

        if (HorizontalAlignment != HorizontalAlignment.Stretch)
        {
            w = MathF.Min(w, DesiredSize.X - Margin.Horizontal);
        }
        if (VerticalAlignment != VerticalAlignment.Stretch)
        {
            h = MathF.Min(h, DesiredSize.Y - Margin.Vertical);
        }

        float x = HorizontalAlignment switch
        {
            HorizontalAlignment.Center => contentSlot.X + (contentSlot.Width - w) * 0.5f,
            HorizontalAlignment.Right => contentSlot.Right - w,
            _ => contentSlot.X
        };

        float y = VerticalAlignment switch
        {
            VerticalAlignment.Center => contentSlot.Y + (contentSlot.Height - h) * 0.5f,
            VerticalAlignment.Bottom => contentSlot.Bottom - h,
            _ => contentSlot.Y
        };

        Bounds = new UIRect(x, y, MathF.Max(0f, w), MathF.Max(0f, h));

        var innerSlot = Bounds.Deflate(Padding);
        ArrangeChildren(innerSlot);
    }

    protected virtual void ArrangeChildren(in UIRect innerSlot)
    {
        for (int i = 0; i < _children.Count; i++)
        {
            if (!_children[i].IsVisible) continue;
            _children[i].Arrange(innerSlot);
        }
    }

    /// <summary>
    /// Recursively draws this widget and its children.
    /// </summary>
    public virtual void Render(UIRenderer renderer, UITheme theme)
    {
        if (!IsVisible) return;

        RenderBackground(renderer, theme);

        for (int i = 0; i < _children.Count; i++)
        {
            if (_children[i].IsVisible)
            {
                _children[i].Render(renderer, theme);
            }
        }

        RenderForeground(renderer, theme);
    }

    protected virtual void RenderBackground(UIRenderer renderer, UITheme theme) { }
    protected virtual void RenderForeground(UIRenderer renderer, UITheme theme) { }

    /// <summary>
    /// Performs recursive hit-testing to identify the topmost widget at the given coordinate.
    /// </summary>
    public virtual Widget? HitTest(Vector2 point)
    {
        if (!IsVisible || !IsEnabled || !Bounds.Contains(point))
        {
            return null;
        }

        // Test children in reverse (top-to-bottom visual order)
        for (int i = _children.Count - 1; i >= 0; i--)
        {
            var hit = _children[i].HitTest(point);
            if (hit != null) return hit;
        }

        return this;
    }

    // Internal event dispatchers invoked by UISystem
    internal void OnPointerEntered() => PointerEntered?.Invoke(this);
    internal void OnPointerExited() => PointerExited?.Invoke(this);
    internal void OnPointerPressed(Vector2 pos) => PointerPressed?.Invoke(this, pos);
    internal void OnPointerReleased(Vector2 pos) => PointerReleased?.Invoke(this, pos);
    internal void OnPointerMoved(Vector2 pos) => PointerMoved?.Invoke(this, pos);
    internal void OnKeyDown(Key key) => KeyDown?.Invoke(this, key);
    internal void OnKeyUp(Key key) => KeyUp?.Invoke(this, key);
    internal void OnCharInput(char c) => CharInput?.Invoke(this, c);
    internal void OnFocusGained() => FocusGained?.Invoke(this);
    internal void OnFocusLost() => FocusLost?.Invoke(this);
}
