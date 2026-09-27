using System.Numerics;
using Khefest.Graphics.Camera;
using Khefest.Graphics.LowLevel;
using Khefest.Graphics.TwoD;
using Khefest.Input;
using Khefest.Windows.Application;
using Khefest.Windows.Timing;

namespace KhefestTemplate;

/// <summary>
/// Main game simulation and rendering class.
/// </summary>
public sealed class MyGame : Game
{
    private SpriteBatch? _spriteBatch;
    private Camera2D? _camera;
    private Vector2 _position = Vector2.Zero;

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
        if (Input.IsKeyPressed(Key.Escape))
        {
            Exit();
            return;
        }

        float dt = gameTime.DeltaTime;
        float speed = 350f;

        // Smooth 4-way keyboard movement
        if (Input.IsKeyDown(Key.W) || Input.IsKeyDown(Key.Up))    _position.Y -= speed * dt;
        if (Input.IsKeyDown(Key.S) || Input.IsKeyDown(Key.Down))  _position.Y += speed * dt;
        if (Input.IsKeyDown(Key.A) || Input.IsKeyDown(Key.Left))  _position.X -= speed * dt;
        if (Input.IsKeyDown(Key.D) || Input.IsKeyDown(Key.Right)) _position.X += speed * dt;
    }

    public override void Render(GameTime gameTime)
    {
        var backBuffer = SwapChain.CurrentBackBuffer;

        // Clear screen with RAII scoped pass
        var clearPass = new RenderPassDesc
        {
            ColorTargets = [backBuffer],
            ClearColor = new Color4(0.10f, 0.12f, 0.16f, 1.0f),
            ClearColorTarget = true
        };

        using var recorder = GpuDevice.CreateCommandRecorder();
        using (recorder.BeginScopedPass(clearPass))
        {
            // Scoped pass clears the backbuffer deterministically
        }
        GpuDevice.Submit(recorder);

        // Render 2D scene
        _spriteBatch?.Begin(backBuffer, _camera);

        // Background reference grid & boundary
        for (float x = -600; x <= 600; x += 100)
            _spriteBatch?.DrawLine(new Vector2(x, -350), new Vector2(x, 350), new Color4(0.15f, 0.18f, 0.24f, 1f));
        for (float y = -350; y <= 350; y += 100)
            _spriteBatch?.DrawLine(new Vector2(-600, y), new Vector2(600, y), new Color4(0.15f, 0.18f, 0.24f, 1f));

        _spriteBatch?.DrawRectangle(new Vector2(-600, -350), new Vector2(1200, 700), new Color4(0.3f, 0.4f, 0.5f, 1f), thickness: 2f);

        // Player entity (centered at origin)
        _spriteBatch?.FillCircle(_position, 28f, Color4.RoyalBlue);
        _spriteBatch?.DrawCircle(_position, 34f, Color4.White, thickness: 2f);
        _spriteBatch?.End();

        // Screen-space HUD (camera: null)
        _spriteBatch?.Begin(backBuffer, camera: null);
        _spriteBatch?.DrawString("Welcome to Khefest!", new Vector2(24, 24), Color4.Gold, scale: 1.2f);
        _spriteBatch?.DrawString($"FPS: {gameTime.Fps:F0} | Pos: ({_position.X:F0}, {_position.Y:F0}) | Use WASD / Arrows to move | ESC to exit", new Vector2(24, 60), Color4.White);
        _spriteBatch?.End();
    }

    public override void Shutdown()
    {
        _spriteBatch?.Dispose();
        base.Shutdown();
    }
}
