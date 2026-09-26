using Khefest.Core.Configuration;

namespace Khefest.Windowing;

/// <summary>
/// Abstraction for a platform display window.
/// </summary>
public interface IWindow : IDisposable
{
    /// <summary>
    /// Native window handle (HWND on Windows).
    /// </summary>
    nint Handle { get; }

    string Title { get; set; }

    int X { get; }
    int Y { get; }
    int Width { get; }
    int Height { get; }
    int ClientWidth { get; }
    int ClientHeight { get; }

    WindowMode Mode { get; }
    bool IsVisible { get; }
    bool IsFocused { get; }
    bool ShouldClose { get; }

    void Show();
    void Hide();
    void SetSize(int width, int height);
    void SetPosition(int x, int y);
    void SetMode(WindowMode mode);
    void RequestClose();
    void PollEvents();

    event Action<int, int>? Resized;
    event Action<int, int>? Moved;
    event Action<bool>? FocusChanged;
    event Action? CloseRequested;
}
