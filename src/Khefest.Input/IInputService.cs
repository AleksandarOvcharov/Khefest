using System.Numerics;

namespace Khefest.Input;

/// <summary>
/// Service providing keyboard and mouse input states.
/// </summary>
public interface IInputService
{
    // Keyboard
    bool IsKeyDown(Key key);
    bool IsKeyUp(Key key) => !IsKeyDown(key);
    bool IsKeyPressed(Key key);
    bool IsKeyReleased(Key key);

    // Mouse
    bool IsMouseButtonDown(MouseButton button);
    bool IsMouseButtonUp(MouseButton button) => !IsMouseButtonDown(button);
    bool IsMouseButtonPressed(MouseButton button);
    bool IsMouseButtonReleased(MouseButton button);

    /// <summary>
    /// Current mouse position in window coordinates as a 2D floating-point vector.
    /// </summary>
    Vector2 MousePosition { get; }

    /// <summary>
    /// Current mouse movement delta this frame as a 2D floating-point vector.
    /// </summary>
    Vector2 MouseDelta { get; }

    /// <summary>
    /// Current mouse position in integer pixel coordinates.
    /// </summary>
    (int X, int Y) MousePositionPixels { get; }

    /// <summary>
    /// Current mouse delta in integer pixel coordinates.
    /// </summary>
    (int DeltaX, int DeltaY) MouseDeltaPixels { get; }

    float ScrollDelta { get; }

    /// <summary>
    /// Event triggered when a printable character or text symbol is typed.
    /// </summary>
    event Action<char>? CharTyped;

    /// <summary>
    /// Event triggered when a key is pressed down.
    /// </summary>
    event Action<Key>? KeyDown;

    /// <summary>
    /// Event triggered when a key is released.
    /// </summary>
    event Action<Key>? KeyUp;

    /// <summary>
    /// Prepares input buffers for the next frame, shifting current states to previous states.
    /// </summary>
    void Update();

    // Direct event dispatchers for input providers / window hooks
    void OnKeyDown(Key key);
    void OnKeyUp(Key key);
    void OnChar(char c);
    void OnMouseDown(MouseButton button);
    void OnMouseUp(MouseButton button);
    void OnMouseMove(int x, int y);
    void OnMouseScroll(float delta);
}
