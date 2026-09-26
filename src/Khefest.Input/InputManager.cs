using System.Numerics;

namespace Khefest.Input;

/// <summary>
/// Default thread-safe implementation of <see cref="IInputService"/>.
/// </summary>
public sealed class InputManager : IInputService
{
    private const int MaxKeyCount = 256;
    private const int MaxMouseButtonCount = 8;

    private readonly bool[] _currentKeys = new bool[MaxKeyCount];
    private readonly bool[] _previousKeys = new bool[MaxKeyCount];

    private readonly bool[] _currentMouseButtons = new bool[MaxMouseButtonCount];
    private readonly bool[] _previousMouseButtons = new bool[MaxMouseButtonCount];

    private int _mouseX;
    private int _mouseY;
    private int _prevMouseX;
    private int _prevMouseY;

    private float _scrollDeltaAccumulator;
    private float _scrollDeltaCurrent;

    private readonly Lock _lock = new();

    public event Action<char>? CharTyped;
    public event Action<Key>? KeyDown;
    public event Action<Key>? KeyUp;

    public Vector2 MousePosition
    {
        get
        {
            lock (_lock)
            {
                return new Vector2(_mouseX, _mouseY);
            }
        }
    }

    public Vector2 MouseDelta
    {
        get
        {
            lock (_lock)
            {
                return new Vector2(_mouseX - _prevMouseX, _mouseY - _prevMouseY);
            }
        }
    }

    public (int X, int Y) MousePositionPixels
    {
        get
        {
            lock (_lock)
            {
                return (_mouseX, _mouseY);
            }
        }
    }

    public (int DeltaX, int DeltaY) MouseDeltaPixels
    {
        get
        {
            lock (_lock)
            {
                return (_mouseX - _prevMouseX, _mouseY - _prevMouseY);
            }
        }
    }

    public float ScrollDelta
    {
        get
        {
            lock (_lock)
            {
                return _scrollDeltaCurrent;
            }
        }
    }

    public bool IsKeyDown(Key key)
    {
        var idx = (int)key;
        if (idx < 0 || idx >= MaxKeyCount) return false;
        lock (_lock)
        {
            return _currentKeys[idx];
        }
    }

    public bool IsKeyPressed(Key key)
    {
        var idx = (int)key;
        if (idx < 0 || idx >= MaxKeyCount) return false;
        lock (_lock)
        {
            return _currentKeys[idx] && !_previousKeys[idx];
        }
    }

    public bool IsKeyReleased(Key key)
    {
        var idx = (int)key;
        if (idx < 0 || idx >= MaxKeyCount) return false;
        lock (_lock)
        {
            return !_currentKeys[idx] && _previousKeys[idx];
        }
    }

    public bool IsMouseButtonDown(MouseButton button)
    {
        var idx = (int)button;
        if (idx < 0 || idx >= MaxMouseButtonCount) return false;
        lock (_lock)
        {
            return _currentMouseButtons[idx];
        }
    }

    public bool IsMouseButtonPressed(MouseButton button)
    {
        var idx = (int)button;
        if (idx < 0 || idx >= MaxMouseButtonCount) return false;
        lock (_lock)
        {
            return _currentMouseButtons[idx] && !_previousMouseButtons[idx];
        }
    }

    public bool IsMouseButtonReleased(MouseButton button)
    {
        var idx = (int)button;
        if (idx < 0 || idx >= MaxMouseButtonCount) return false;
        lock (_lock)
        {
            return !_currentMouseButtons[idx] && _previousMouseButtons[idx];
        }
    }

    public void Update()
    {
        lock (_lock)
        {
            Array.Copy(_currentKeys, _previousKeys, MaxKeyCount);
            Array.Copy(_currentMouseButtons, _previousMouseButtons, MaxMouseButtonCount);

            _prevMouseX = _mouseX;
            _prevMouseY = _mouseY;

            _scrollDeltaCurrent = _scrollDeltaAccumulator;
            _scrollDeltaAccumulator = 0.0f;
        }
    }

    public void OnKeyDown(Key key)
    {
        var idx = (int)key;
        if (idx >= 0 && idx < MaxKeyCount)
        {
            lock (_lock)
            {
                _currentKeys[idx] = true;
            }
            KeyDown?.Invoke(key);
        }
    }

    public void OnKeyUp(Key key)
    {
        var idx = (int)key;
        if (idx >= 0 && idx < MaxKeyCount)
        {
            lock (_lock)
            {
                _currentKeys[idx] = false;
            }
            KeyUp?.Invoke(key);
        }
    }

    public void OnChar(char c)
    {
        CharTyped?.Invoke(c);
    }

    public void OnMouseDown(MouseButton button)
    {
        var idx = (int)button;
        if (idx >= 0 && idx < MaxMouseButtonCount)
        {
            lock (_lock)
            {
                _currentMouseButtons[idx] = true;
            }
        }
    }

    public void OnMouseUp(MouseButton button)
    {
        var idx = (int)button;
        if (idx >= 0 && idx < MaxMouseButtonCount)
        {
            lock (_lock)
            {
                _currentMouseButtons[idx] = false;
            }
        }
    }

    public void OnMouseMove(int x, int y)
    {
        lock (_lock)
        {
            _mouseX = x;
            _mouseY = y;
        }
    }

    public void OnMouseScroll(float delta)
    {
        lock (_lock)
        {
            _scrollDeltaAccumulator += delta;
        }
    }
}
