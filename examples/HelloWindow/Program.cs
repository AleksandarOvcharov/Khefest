using Khefest.Core.Configuration;
using Khefest.Core.Errors;
using Khefest.Core.Logging;
using Khefest.Input;
using Khefest.Windows.Application;
using Khefest.Windows.Timing;

namespace HelloWindow;

public sealed class HelloWindowGame : Game
{
    private readonly ILogger _logger = LogManager.GetLogger(KhefestSubsystem.Core, "HelloWindow");
    private double _titleUpdateAccumulator;

    public override void Initialize()
    {
        _logger.Info("HelloWindow initialized!");

        var primaryDisplay = Displays.GetPrimaryMonitor();
        if (primaryDisplay != null)
        {
            _logger.Info($"Primary display: {primaryDisplay.DeviceName} ({primaryDisplay.Width}x{primaryDisplay.Height} @ {primaryDisplay.RefreshRate}Hz)");
        }
    }

    public override void Update(GameTime time)
    {
        // Toggle fullscreen with F11
        if (Input.IsKeyPressed(Key.F11))
        {
            var newMode = Window.Mode == WindowMode.Windowed
                ? WindowMode.BorderlessFullscreen
                : WindowMode.Windowed;
            Window.SetMode(newMode);
            _logger.Info($"Toggled window mode to: {newMode}");
        }

        // Space key logs input information
        if (Input.IsKeyPressed(Key.Space))
        {
            var (mx, my) = Input.MousePosition;
            var (dx, dy) = Input.MouseDelta;
            _logger.Info($"Space pressed! Mouse: ({mx}, {my}), Delta: ({dx}, {dy})");
        }

        // Escape key requests exit
        if (Input.IsKeyPressed(Key.Escape))
        {
            _logger.Info("Escape pressed, exiting application...");
            Exit();
        }

        // Periodically update window title with FPS
        _titleUpdateAccumulator += time.DeltaTimeSeconds;
        if (_titleUpdateAccumulator >= 0.25)
        {
            _titleUpdateAccumulator = 0;
            Window.Title = $"Khefest - HelloWindow | FPS: {time.Fps:F1} | Frame: {time.FrameCount} | Mode: {Window.Mode}";
        }
    }

    public override void Render(GameTime time)
    {
        // In Phase 2, rendering pipeline is not yet attached.
        // Screen will be drawn by the GPU layer in Phase 4.
    }

    public override void Shutdown()
    {
        _logger.Info("HelloWindow game shutting down.");
    }
}

internal static class Program
{
    private static void Main()
    {
        var config = new KhefestConfigBuilder()
            .ConfigureWindow(w => w with
            {
                Title = "Khefest - HelloWindow",
                Width = 1280,
                Height = 720,
                Mode = WindowMode.Windowed,
                Resizable = true,
                VSync = true
            })
            .ConfigureLogging(l => l with
            {
                MinimumLevel = LogLevel.Info,
                EnableConsoleSink = true
            })
            .Build();

        KhefestApp.Run(new HelloWindowGame(), config);
    }
}
