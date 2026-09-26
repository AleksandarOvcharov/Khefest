using Khefest.Core.Configuration;
using Khefest.Core.Errors;
using Khefest.Core.Logging;
using Khefest.Core.Resources;
using Khefest.Graphics.LowLevel.Windows;
using Khefest.Input;
using Khefest.Windows.Display;
using Khefest.Windows.Timing;
using Khefest.Windows.Windowing;

namespace Khefest.Windows.Application;

/// <summary>
/// Main framework entrypoint providing lifecycle orchestration and the application loop.
/// </summary>
public static class KhefestApp
{
    private static readonly ILogger Logger = LogManager.GetLogger(KhefestSubsystem.Core, "KhefestApp");

    /// <summary>
    /// Initializes and runs a <see cref="Game"/> instance through the managed application loop.
    /// </summary>
    public static void Run(Game game, KhefestConfig? config = null)
    {
        ArgumentNullException.ThrowIfNull(game);
        config ??= new KhefestConfigBuilder().Build();

        // 1. Configure logging
        LogManager.GlobalMinimumLevel = config.Logging.MinimumLevel;
        if (config.Logging.EnableMemorySink)
        {
            LogManager.AddSink(new MemoryLogSink(config.Logging.MemorySinkCapacity));
        }

        Logger.Info("Starting Khefest Engine...");

        // 2. Initialize Core Subsystems
        using var resources = new ResourceManager(config.Memory);
        var input = new InputManager();
        var displayService = new Win32DisplayService();

        // 3. Create Win32 Window
        using var window = new Win32Window(config.Window, input);

        // 4. Initialize GPU Device and SwapChain
        using var gpuDevice = new WindowsGpuDevice(resources);
        using var swapChain = gpuDevice.CreateSwapChain(
            window,
            window.ClientWidth,
            window.ClientHeight,
            config.Window.VSync);

        // Handle dynamic window resizing
        window.Resized += (w, h) =>
        {
            if (w > 0 && h > 0)
            {
                swapChain.Resize(w, h);
                game.OnResize(w, h);
            }
        };

        // 5. Inject dependencies into Game instance
        game.Window = window;
        game.Input = input;
        game.Displays = displayService;
        game.Config = config;
        game.Resources = resources;
        game.GpuDevice = gpuDevice;
        game.SwapChain = swapChain;
        game.IsRunning = true;

        var timer = new Win32Timer();

        try
        {
            // 6. Initialize Game
            Logger.Info("Initializing application...");
            game.Initialize();

            // Display window once initialized
            window.Show();

            // 7. Application Loop
            Logger.Info("Entering application main loop...");
            while (!window.ShouldClose && game.IsRunning)
            {
                // Input advance (shift previous to current)
                input.Update();

                // Process Win32 OS messages
                window.PollEvents();

                if (window.ShouldClose)
                {
                    break;
                }

                // Advance high-resolution timer
                var gameTime = timer.Tick();

                // Game update
                game.Update(gameTime);

                // Game render
                game.Render(gameTime);

                // Present rendered frame
                swapChain.Present(config.Window.VSync);

                // Optional target FPS throttling (when VSync is disabled or target FPS set)
                if (config.Window.TargetFps > 0 && !config.Window.VSync)
                {
                    var targetFrameTimeMs = 1000.0 / config.Window.TargetFps;
                    var elapsedMs = gameTime.ElapsedTime.TotalMilliseconds;
                    if (elapsedMs < targetFrameTimeMs)
                    {
                        var sleepMs = (int)(targetFrameTimeMs - elapsedMs);
                        if (sleepMs > 0)
                        {
                            Thread.Sleep(sleepMs);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Fatal("Fatal unhandled exception in application loop!", ex);
            throw;
        }
        finally
        {
            Logger.Info("Shutting down application...");
            game.IsRunning = false;

            try
            {
                game.Shutdown();
            }
            catch (Exception ex)
            {
                Logger.Error("Error during game shutdown", ex);
            }

            // Verify memory leaks if configured
            if (resources.Tracker.HasLeaks)
            {
                var leakReport = resources.Tracker.GenerateLeakReport();
                Logger.Warning(leakReport);

                if (!config.Memory.AutoMemManagement)
                {
                    throw new KhefestResourceException(KhefestErrorCode.ResourceLeakDetected, leakReport);
                }
            }

            Logger.Info("Khefest Engine shutdown complete.");
        }
    }
}
