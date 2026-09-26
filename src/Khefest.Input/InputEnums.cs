namespace Khefest.Input;

/// <summary>
/// Virtual key codes for keyboard input.
/// </summary>
public enum Key
{
    None = 0,

    // Alphabetic
    A, B, C, D, E, F, G, H, I, J, K, L, M,
    N, O, P, Q, R, S, T, U, V, W, X, Y, Z,

    // Digits
    D0, D1, D2, D3, D4, D5, D6, D7, D8, D9,

    // Numpad
    NumPad0, NumPad1, NumPad2, NumPad3, NumPad4,
    NumPad5, NumPad6, NumPad7, NumPad8, NumPad9,
    Add, Subtract, Multiply, Divide, Decimal,

    // Function keys
    F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12,

    // Navigation and editing
    Escape,
    Enter,
    Space,
    Tab,
    Backspace,
    Insert,
    Delete,
    Home,
    End,
    PageUp,
    PageDown,

    // Arrow keys
    Up,
    Down,
    Left,
    Right,

    // Modifiers
    LeftShift,
    RightShift,
    LeftControl,
    RightControl,
    LeftAlt,
    RightAlt,
    LeftSuper,
    RightSuper,

    // Locks and symbols
    CapsLock,
    ScrollLock,
    NumLock,
    PrintScreen,
    Pause,
    Semicolon,
    Equals,
    Comma,
    Minus,
    Period,
    Slash,
    Backslash,
    LeftBracket,
    RightBracket,
    Apostrophe,
    Grave
}

/// <summary>
/// Mouse buttons.
/// </summary>
public enum MouseButton
{
    Left = 0,
    Right = 1,
    Middle = 2,
    Button4 = 3,
    Button5 = 4
}
