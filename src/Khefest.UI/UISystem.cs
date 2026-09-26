using System.Numerics;
using Khefest.Graphics.Text;
using Khefest.Graphics.TwoD;
using Khefest.Input;
using Khefest.UI.Core;
using Khefest.UI.Panels;
using Khefest.UI.Rendering;
using Khefest.UI.Styling;
using Khefest.UI.Widgets;

namespace Khefest.UI;

/// <summary>
/// Root UI subsystem managing the widget visual hierarchy, input event dispatching, layout lifecycle, and rendering.
/// </summary>
public sealed class UISystem : IDisposable
{
    private Widget _root;
    private Widget? _hoveredWidget;
    private Widget? _pressedWidget;
    private Widget? _focusedWidget;
    private IInputService? _attachedInput;

    public Widget Root
    {
        get => _root;
        set
        {
            _root = value ?? throw new ArgumentNullException(nameof(value));
            _hoveredWidget = null;
            _pressedWidget = null;
            _focusedWidget = null;
        }
    }

    public UITheme Theme { get; set; } = UITheme.Dark;
    public Widget? FocusedWidget => _focusedWidget;
    public Widget? HoveredWidget => _hoveredWidget;

    public UISystem(Widget? root = null, UITheme? theme = null, IInputService? input = null)
    {
        _root = root ?? new Canvas();
        if (theme != null) Theme = theme;
        if (input != null) AttachInput(input);
    }

    /// <summary>
    /// Attaches an input service to automatically route keyboard events and text typing to focused widgets.
    /// </summary>
    public void AttachInput(IInputService input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (_attachedInput == input) return;

        DetachInput();

        _attachedInput = input;
        _attachedInput.KeyDown += HandleKeyDown;
        _attachedInput.KeyUp += HandleKeyUp;
        _attachedInput.CharTyped += HandleCharInput;
    }

    /// <summary>
    /// Detaches the currently attached input service.
    /// </summary>
    public void DetachInput()
    {
        if (_attachedInput != null)
        {
            _attachedInput.KeyDown -= HandleKeyDown;
            _attachedInput.KeyUp -= HandleKeyUp;
            _attachedInput.CharTyped -= HandleCharInput;
            _attachedInput = null;
        }
    }

    /// <summary>
    /// Updates layout, widget animations, and blinking carets for the current frame.
    /// </summary>
    public void Update(float deltaTime, Vector2 viewportSize)
    {
        // 1. Measure and Arrange root hierarchy to fit viewport
        _root.Measure(viewportSize);
        _root.Arrange(new UIRect(0, 0, viewportSize.X, viewportSize.Y));

        // 2. Update time-dependent widgets (e.g. TextBox cursor)
        UpdateWidgetHierarchy(_root, deltaTime);
    }

    private void UpdateWidgetHierarchy(Widget widget, float deltaTime)
    {
        if (widget is TextBox tb)
        {
            tb.Update(deltaTime);
        }

        for (int i = 0; i < widget.Children.Count; i++)
        {
            UpdateWidgetHierarchy(widget.Children[i], deltaTime);
        }
    }

    /// <summary>
    /// Processes continuous input state (mouse movement and buttons) from Khefest <see cref="IInputService"/>.
    /// </summary>
    public void ProcessInput(IInputService input)
    {
        ArgumentNullException.ThrowIfNull(input);

        // Auto-attach if not yet attached
        if (_attachedInput != input)
        {
            AttachInput(input);
        }

        var mousePos = new Vector2(input.MousePosition.X, input.MousePosition.Y);
        HandlePointerMove(mousePos);

        if (input.IsMouseButtonPressed(MouseButton.Left))
        {
            HandlePointerDown(mousePos, MouseButton.Left);
        }
        else if (input.IsMouseButtonReleased(MouseButton.Left))
        {
            HandlePointerUp(mousePos, MouseButton.Left);
        }
    }

    public void HandlePointerMove(Vector2 position)
    {
        var hit = _root.HitTest(position);

        if (_hoveredWidget != hit)
        {
            if (_hoveredWidget != null)
            {
                _hoveredWidget.IsHovered = false;
                _hoveredWidget.OnPointerExited();
            }

            _hoveredWidget = hit;

            if (_hoveredWidget != null)
            {
                _hoveredWidget.IsHovered = true;
                _hoveredWidget.OnPointerEntered();
            }
        }

        _hoveredWidget?.OnPointerMoved(position);
        if (_pressedWidget != null && _pressedWidget != _hoveredWidget)
        {
            _pressedWidget.OnPointerMoved(position);
        }
    }

    public void HandlePointerDown(Vector2 position, MouseButton button)
    {
        var hit = _root.HitTest(position);

        // Update focus
        if (hit != null && hit.Focusable)
        {
            SetFocus(hit);
        }
        else
        {
            SetFocus(null);
        }

        if (hit != null)
        {
            _pressedWidget = hit;
            hit.IsPressed = true;
            hit.OnPointerPressed(position);
        }
    }

    public void HandlePointerUp(Vector2 position, MouseButton button)
    {
        if (_pressedWidget != null)
        {
            _pressedWidget.IsPressed = false;
            _pressedWidget.OnPointerReleased(position);
            _pressedWidget = null;
        }
    }

    public void HandleKeyDown(Key key)
    {
        _focusedWidget?.OnKeyDown(key);
    }

    public void HandleKeyUp(Key key)
    {
        _focusedWidget?.OnKeyUp(key);
    }

    public void HandleCharInput(char c)
    {
        _focusedWidget?.OnCharInput(c);
    }

    public void SetFocus(Widget? widget)
    {
        if (_focusedWidget == widget) return;

        if (_focusedWidget != null)
        {
            _focusedWidget.IsFocused = false;
            _focusedWidget.OnFocusLost();
        }

        _focusedWidget = widget;

        if (_focusedWidget != null)
        {
            _focusedWidget.IsFocused = true;
            _focusedWidget.OnFocusGained();
        }
    }

    /// <summary>
    /// Renders the entire UI hierarchy using the specified <see cref="SpriteBatch"/>.
    /// </summary>
    public void Render(SpriteBatch spriteBatch, BitmapFont? font = null)
    {
        ArgumentNullException.ThrowIfNull(spriteBatch);
        var renderer = new UIRenderer(spriteBatch, font);
        _root.Render(renderer, Theme);
    }

    public void Dispose()
    {
        DetachInput();
    }
}
