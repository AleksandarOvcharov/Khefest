using Khefest.Core.Configuration;
using Khefest.Core.Resources;
using Khefest.Graphics.LowLevel;
using Khefest.Input;
using Khefest.Windowing;
using Khefest.Windows.Timing;

namespace Khefest.Windows.Application;

/// <summary>
/// Base class for all Khefest applications and games.
/// Provides lifecycle hooks (Initialize, Update, Render, Shutdown) and access to core services.
/// </summary>
public abstract class Game
{
    public IWindow Window { get; internal set; } = null!;
    public IInputService Input { get; internal set; } = null!;
    public IDisplayService Displays { get; internal set; } = null!;
    public KhefestConfig Config { get; internal set; } = null!;
    public ResourceManager Resources { get; internal set; } = null!;
    public IGpuDevice GpuDevice { get; internal set; } = null!;
    public ISwapChain SwapChain { get; internal set; } = null!;

    public bool IsRunning { get; internal set; }

    /// <summary>
    /// Invoked once upon application startup.
    /// Load assets, create GPU resources, and setup initial game state here.
    /// </summary>
    public virtual void Initialize() { }

    /// <summary>
    /// Invoked each frame to advance simulation, handle input, and update game logic.
    /// </summary>
    public virtual void Update(GameTime time) { }

    /// <summary>
    /// Invoked each frame to execute rendering commands.
    /// </summary>
    public virtual void Render(GameTime time) { }

    /// <summary>
    /// Invoked when the window or display client area is resized.
    /// </summary>
    public virtual void OnResize(int width, int height) { }

    /// <summary>
    /// Invoked upon application exit. Clean up user resources here.
    /// </summary>
    public virtual void Shutdown() { }

    /// <summary>
    /// Requests the application loop to terminate cleanly.
    /// </summary>
    public void Exit()
    {
        Window?.RequestClose();
    }
}
