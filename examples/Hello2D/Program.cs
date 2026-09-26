using System.Numerics;
using Khefest.Core.Configuration;
using Khefest.Core.Errors;
using Khefest.Core.Logging;
using Khefest.Graphics.Camera;
using Khefest.Graphics.LowLevel;
using Khefest.Graphics.Texturing;
using Khefest.Graphics.TwoD;
using Khefest.Input;
using Khefest.Windows.Application;
using Khefest.Windows.Timing;

namespace Khefest.Examples.Hello2D;

public sealed class Hello2DGame : Game
{
    private static readonly ILogger Logger = LogManager.GetLogger(KhefestSubsystem.Core, "Hello2D");

    private SpriteBatch? _spriteBatch;
    private Camera2D? _camera;
    private Texture2D? _checkerTexture;
    private Texture2D? _characterTexture;

    private readonly List<RotatingSprite> _sprites = new();
    private float _globalTime;
    private int _frameCount;
    private float _fpsTimer;
    private int _currentFps;

    private sealed class RotatingSprite
    {
        public Vector2 BasePosition;
        public float Speed;
        public float Radius;
        public float Angle;
        public Color4 Color;
        public Vector2 Scale;
    }

    public override void Initialize()
    {
        Logger.Info("Initializing Khefest Phase 5 2D Engine Example...");

        _camera = new Camera2D(Window.Width, Window.Height);
        _spriteBatch = new SpriteBatch(GpuDevice, Resources);

        // 1. Generate procedural 64x64 checkerboard texture
        var checkerPixels = new byte[64 * 64 * 4];
        for (int y = 0; y < 64; y++)
        {
            for (int x = 0; x < 64; x++)
            {
                bool isWhite = ((x / 8) + (y / 8)) % 2 == 0;
                int idx = (y * 64 + x) * 4;
                byte col = isWhite ? (byte)220 : (byte)60;
                checkerPixels[idx + 0] = col;
                checkerPixels[idx + 1] = col;
                checkerPixels[idx + 2] = col;
                checkerPixels[idx + 3] = 255;
            }
        }
        _checkerTexture = Texture2D.FromRgba(GpuDevice, "Checkerboard", 64, 64, checkerPixels, Resources);

        // 2. Generate procedural 32x32 character sprite (gem/diamond)
        var charPixels = new byte[32 * 32 * 4];
        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                int dx = Math.Abs(x - 16);
                int dy = Math.Abs(y - 16);
                int idx = (y * 32 + x) * 4;
                if (dx + dy <= 14)
                {
                    charPixels[idx + 0] = (byte)(100 + dx * 10);
                    charPixels[idx + 1] = (byte)(200 + dy * 3);
                    charPixels[idx + 2] = 255;
                    charPixels[idx + 3] = 255;
                }
                else
                {
                    charPixels[idx + 0] = 0;
                    charPixels[idx + 1] = 0;
                    charPixels[idx + 2] = 0;
                    charPixels[idx + 3] = 0;
                }
            }
        }
        _characterTexture = Texture2D.FromRgba(GpuDevice, "DiamondSprite", 32, 32, charPixels, Resources);

        // 3. Spawn swirling sprites
        var rng = new Random(42);
        for (int i = 0; i < 48; i++)
        {
            _sprites.Add(new RotatingSprite
            {
                BasePosition = new Vector2(0, 0),
                Speed = 0.5f + (float)rng.NextDouble() * 1.5f,
                Radius = 80f + (float)rng.NextDouble() * 350f,
                Angle = (float)rng.NextDouble() * MathF.Tau,
                Color = new Color4((float)rng.NextDouble(), (float)rng.NextDouble(), (float)rng.NextDouble(), 0.9f),
                Scale = new Vector2(0.8f + (float)rng.NextDouble() * 1.2f)
            });
        }

        Logger.Info("Hello2D initialized successfully. WASD/Arrows to pan, Z/X to zoom, Q/E to rotate, R to reset.");
    }

    public override void Update(GameTime time)
    {
        float deltaTime = time.DeltaTimeSeconds;
        _globalTime += deltaTime;

        // FPS tracking
        _frameCount++;
        _fpsTimer += deltaTime;
        if (_fpsTimer >= 1.0f)
        {
            _currentFps = _frameCount;
            _frameCount = 0;
            _fpsTimer -= 1.0f;
        }

        // Camera resize tracking
        if (_camera != null && (_camera.ViewportWidth != Window.Width || _camera.ViewportHeight != Window.Height))
        {
            _camera.ViewportWidth = Window.Width;
            _camera.ViewportHeight = Window.Height;
            _camera.Origin = new Vector2(Window.Width * 0.5f, Window.Height * 0.5f);
        }

        // Camera Keyboard Controls
        if (_camera != null)
        {
            float panSpeed = 400.0f * deltaTime / _camera.Zoom;

            if (Input.IsKeyDown(Key.W) || Input.IsKeyDown(Key.Up))
                _camera.Move(new Vector2(0, -panSpeed));
            if (Input.IsKeyDown(Key.S) || Input.IsKeyDown(Key.Down))
                _camera.Move(new Vector2(0, panSpeed));
            if (Input.IsKeyDown(Key.A) || Input.IsKeyDown(Key.Left))
                _camera.Move(new Vector2(-panSpeed, 0));
            if (Input.IsKeyDown(Key.D) || Input.IsKeyDown(Key.Right))
                _camera.Move(new Vector2(panSpeed, 0));

            // Zoom
            if (Input.IsKeyDown(Key.Z) || Input.IsKeyDown(Key.PageUp))
                _camera.ZoomBy(1.0f + 1.5f * deltaTime);
            if (Input.IsKeyDown(Key.X) || Input.IsKeyDown(Key.PageDown))
                _camera.ZoomBy(1.0f - 1.5f * deltaTime);

            // Rotation
            if (Input.IsKeyDown(Key.Q))
                _camera.Rotate(-1.5f * deltaTime);
            if (Input.IsKeyDown(Key.E))
                _camera.Rotate(1.5f * deltaTime);

            // Reset Camera
            if (Input.IsKeyPressed(Key.R))
            {
                _camera.Position = Vector2.Zero;
                _camera.Zoom = 1.0f;
                _camera.Rotation = 0.0f;
            }

            // Exit on Escape
            if (Input.IsKeyPressed(Key.Escape))
            {
                Exit();
                return;
            }
        }

        // Update rotating sprites
        foreach (var s in _sprites)
        {
            s.Angle += s.Speed * deltaTime;
        }
    }

    public override void Render(GameTime time)
    {
        float deltaTime = time.DeltaTimeSeconds;

        if (_spriteBatch == null || _camera == null || _checkerTexture == null || _characterTexture == null)
            return;

        var backBuffer = SwapChain.CurrentBackBuffer;

        // Pass 1: Clear screen to a dark night backdrop
        var clearPass = new RenderPassDesc
        {
            ColorTarget = backBuffer,
            ClearColor = new Color4(0.08f, 0.09f, 0.14f, 1.0f),
            ClearColorTarget = true
        };
        var clearRecorder = GpuDevice.CreateCommandRecorder();
        clearRecorder.BeginPass(clearPass);
        clearRecorder.EndPass();
        GpuDevice.Submit(clearRecorder);
        clearRecorder.Dispose();

        // -------------------------------------------------------------
        // Pass 2: World-Space 2D Scene with Camera2D transformation
        // -------------------------------------------------------------
        _spriteBatch.Begin(backBuffer, _camera, SpriteSortMode.Deferred);

        // Draw large background tiled ground using checkerboard
        for (int y = -4; y < 4; y++)
        {
            for (int x = -4; x < 4; x++)
            {
                _spriteBatch.Draw(
                    _checkerTexture,
                    new Vector2(x * 64, y * 64),
                    new Vector2(64, 64),
                    new Color4(0.6f, 0.6f, 0.8f, 0.35f));
            }
        }

        // Draw coordinate axes and reference vector shapes
        _spriteBatch.DrawLine(new Vector2(-300, 0), new Vector2(300, 0), Color4.Red, 2.0f);
        _spriteBatch.DrawLine(new Vector2(0, -300), new Vector2(0, 300), Color4.Green, 2.0f);

        // Draw concentric circles in world space
        _spriteBatch.DrawCircle(Vector2.Zero, 100.0f, Color4.Cyan, 32, 2.0f);
        _spriteBatch.DrawCircle(Vector2.Zero, 200.0f, Color4.Yellow, 48, 1.5f);
        _spriteBatch.DrawCircle(Vector2.Zero, 300.0f, Color4.Magenta, 64, 1.0f);

        // Draw rotating swirling sprites
        foreach (var s in _sprites)
        {
            var pos = s.BasePosition + new Vector2(MathF.Cos(s.Angle) * s.Radius, MathF.Sin(s.Angle) * s.Radius);
            _spriteBatch.Draw(
                _characterTexture,
                pos,
                new Vector2(32 * s.Scale.X, 32 * s.Scale.Y),
                s.Color,
                s.Angle * 2.0f,
                new Vector2(16, 16),
                SpriteEffects.None);
        }

        // Draw an animated central beacon
        float pulse = 0.5f + 0.5f * MathF.Sin(_globalTime * 3.0f);
        _spriteBatch.FillCircle(Vector2.Zero, 20.0f + pulse * 10.0f, new Color4(1.0f, 0.8f, 0.2f, 0.8f), 24);
        _spriteBatch.DrawRectangle(new Vector2(-40, -40), new Vector2(80, 80), Color4.White, 2.0f);

        _spriteBatch.End();

        // -------------------------------------------------------------
        // Pass 3: Screen-Space HUD / UI (No Camera Zoom or Pan)
        // -------------------------------------------------------------
        _spriteBatch.Begin(backBuffer, camera: null, SpriteSortMode.Deferred);

        // Top-left HUD Card
        _spriteBatch.FillRectangle(new Vector2(16, 16), new Vector2(400, 190), new Color4(0.0f, 0.0f, 0.0f, 0.75f));
        _spriteBatch.DrawRectangle(new Vector2(16, 16), new Vector2(400, 190), Color4.CornflowerBlue, 2.0f);

        _spriteBatch.DrawString("KHEFEST 2D ENGINE (PHASE 5)", new Vector2(32, 28), Color4.Yellow, 1.5f);
        _spriteBatch.DrawString($"FPS: {_currentFps} | Delta: {deltaTime * 1000f:F1} ms", new Vector2(32, 54), Color4.White, 1.2f);
        _spriteBatch.DrawString($"Camera Pos: ({_camera.Position.X:F0}, {_camera.Position.Y:F0})", new Vector2(32, 74), Color4.LightGray, 1.1f);
        _spriteBatch.DrawString($"Zoom: {_camera.Zoom:F2}x | Rot: {_camera.Rotation * 180f / MathF.PI:F0} deg", new Vector2(32, 94), Color4.LightGray, 1.1f);

        _spriteBatch.DrawString("Controls:", new Vector2(32, 120), Color4.Cyan, 1.1f);
        _spriteBatch.DrawString("  [WASD / Arrows] Pan Camera", new Vector2(32, 138), Color4.White, 1.0f);
        _spriteBatch.DrawString("  [Z / X] Zoom In / Out | [Q / E] Rotate", new Vector2(32, 154), Color4.White, 1.0f);
        _spriteBatch.DrawString("  [R] Reset Camera Position & Zoom", new Vector2(32, 170), Color4.White, 1.0f);

        // Bottom right badge
        _spriteBatch.FillRectangle(new Vector2(Window.Width - 280, Window.Height - 48), new Vector2(264, 32), new Color4(0.1f, 0.2f, 0.3f, 0.85f));
        _spriteBatch.DrawString("Powered by Direct3D 11 & .NET 10", new Vector2(Window.Width - 270, Window.Height - 38), Color4.White, 1.0f);

        _spriteBatch.End();
    }

    public override void Shutdown()
    {
        _characterTexture?.Dispose();
        _checkerTexture?.Dispose();
        _spriteBatch?.Dispose();
        base.Shutdown();
    }
}

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var game = new Hello2DGame();
        KhefestApp.Run(game);
    }
}
