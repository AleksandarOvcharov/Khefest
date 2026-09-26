using Khefest.Core.Configuration;
using Khefest.Input;
using Khefest.Windowing;
using Khefest.Windows.Windowing;
using Xunit;

namespace Khefest.Tests;

public class Win32WindowTests
{
    [Fact]
    public void Window_CreatesWithValidHandleAndTracksProperties()
    {
        var config = new WindowConfig
        {
            Title = "Test Window",
            Width = 800,
            Height = 600,
            Mode = WindowMode.Windowed
        };

        var input = new InputManager();
        using var window = new Win32Window(config, input);

        Assert.NotEqual(nint.Zero, window.Handle);
        Assert.Equal("Test Window", window.Title);
        Assert.Equal(WindowMode.Windowed, window.Mode);
        Assert.True(window.ClientWidth > 0);
        Assert.True(window.ClientHeight > 0);

        // Test title update
        window.Title = "Updated Title";
        Assert.Equal("Updated Title", window.Title);

        // Test polling
        window.PollEvents();
        Assert.False(window.ShouldClose);

        // Test mode change to Borderless
        window.SetMode(WindowMode.BorderlessFullscreen);
        Assert.Equal(WindowMode.BorderlessFullscreen, window.Mode);

        // Switch back to Windowed
        window.SetMode(WindowMode.Windowed);
        Assert.Equal(WindowMode.Windowed, window.Mode);
    }
}
