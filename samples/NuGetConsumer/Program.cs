using System.Numerics;
using Khefest.Core.Configuration;
using Khefest.Graphics.Camera;
using Khefest.Graphics.LowLevel;
using Khefest.Graphics.TwoD;
using Khefest.Input;
using Khefest.Windows.Application;
using Khefest.Windows.Timing;

namespace NuGetConsumer;

/// <summary>
/// A real game project consuming Khefest exclusively via the published NuGet package.
/// Validates package dependency resolution, assembly references, and public API consumption
/// from an external project context.
/// </summary>
public sealed class PackageConsumerGame : Game
{
    private SpriteBatch? _spriteBatch;
    private Camera2D? _camera;
    private float _circleAnim;

    public override void Initialize()
    {
        _spriteBatch = new SpriteBatch(GpuDevice);
        _camera = new Camera2D(Window.Width, Window.Height);
    }

    public override void OnResize(int width, int height)
    {
        _camera?.Resize(width, height);
    }

    public override void Update(GameTime gameTime)
    {
        if (Input.IsKeyDown(Key.Escape))
            Exit();

        _circleAnim += gameTime.DeltaTime * 2.0f;

        // Smooth camera movement using Vector2 input queries
        if (Input.IsKeyDown(Key.D))
            _camera?.Move(new Vector2(150f * gameTime.DeltaTime, 0));
        if (Input.IsKeyDown(Key.A))
            _camera?.Move(new Vector2(-150f * gameTime.DeltaTime, 0));
    }

    public override void Render(GameTime gameTime)
    {
        var backBuffer = SwapChain.CurrentBackBuffer;

        // Clear screen using ScopedRenderPass
        var clearPass = new RenderPassDesc
        {
            ColorTargets = [backBuffer],
            ClearColor = new Color4(0.08f, 0.10f, 0.15f, 1.0f),
            ClearColorTarget = true
        };

        using var recorder = GpuDevice.CreateCommandRecorder();
        using (recorder.BeginScopedPass(clearPass))
        {
            // Scoped pass clears color target
        }
        GpuDevice.Submit(recorder);

        // Draw sprites and vector primitives via SpriteBatch
        _spriteBatch?.Begin(backBuffer, _camera);

        var radius = 40f + MathF.Sin(_circleAnim) * 10f;
        _spriteBatch?.FillCircle(new Vector2(400, 300), radius, Color4.RoyalBlue);
        _spriteBatch?.DrawCircle(new Vector2(400, 300), radius + 8f, Color4.LimeGreen, thickness: 2f);

        _spriteBatch?.DrawString("Khefest NuGet Package Consumer Active!", new Vector2(50, 50), Color4.Gold);
        _spriteBatch?.DrawString($"FPS: {gameTime.Fps:F0} | DeltaTime: {gameTime.DeltaTime:F4}s", new Vector2(50, 80), Color4.White);

        _spriteBatch?.End();
    }

    public override void Shutdown()
    {
        _spriteBatch?.Dispose();
        base.Shutdown();
    }
}

public static class Program
{
    [STAThread]
    public static void Main()
    {
        var config = new KhefestConfigBuilder()
            .ConfigureWindow(w => w with
            {
                Title = "Khefest NuGet Consumer Sample",
                Width = 1024,
                Height = 640,
                VSync = true
            })
            .Build();

        var game = new PackageConsumerGame();
        KhefestApp.Run(game, config);
    }
}
