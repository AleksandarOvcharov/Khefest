using System.Numerics;
using Khefest.Core.Errors;
using Khefest.Core.Logging;
using Khefest.Core.Profiling;
using Khefest.Graphics.Camera;
using Khefest.Graphics.Diagnostics;
using Khefest.Graphics.LowLevel;
using Khefest.Graphics.Texturing;
using Khefest.Graphics.ThreeD;
using Khefest.Graphics.ThreeD.Lighting;
using Khefest.Graphics.TwoD;
using Khefest.Input;
using Khefest.Windows.Application;
using Khefest.Windows.Timing;

namespace Khefest.Examples.Hello3D;

public sealed class Hello3DGame : Game
{
    private static readonly ILogger Logger = LogManager.GetLogger(KhefestSubsystem.Core, "Hello3D");

    private IGpuTexture? _depthBuffer;
    private MeshRenderer? _meshRenderer;
    private SpriteBatch? _spriteBatch;
    private PerspectiveCamera? _camera;

    private Mesh? _cubeMesh;
    private Mesh? _sphereMesh;
    private Mesh? _planeMesh;

    private Texture2D? _crateTexture;
    private Material? _cubeMaterial;
    private Material? _sphereMaterial;
    private Material? _planeMaterial;

    private DirectionalLight _dirLight;
    private AmbientLight _ambientLight;

    private float _cubeRotation;
    private float _sphereOrbit;
    private bool _autoRotate = true;

    // Phase 8: Diagnostics & Profiling
    private readonly FrameStatistics _frameStats = new();
    private readonly RenderStatistics _renderStats = new();
    private DebugOverlay? _debugOverlay;

    public override void Initialize()
    {
        Logger.Info("Initializing Khefest Phase 6 & Phase 8 3D Engine and Diagnostics Example...");

        CreateDepthBuffer(Window.Width, Window.Height);

        _camera = new PerspectiveCamera(
            position: new Vector3(0f, 3.5f, -6f),
            target: new Vector3(0f, 0.5f, 0f),
            aspectRatio: (float)Window.Width / Window.Height,
            fovDegrees: 60f);

        _meshRenderer = new MeshRenderer(GpuDevice, Resources);
        _spriteBatch = new SpriteBatch(GpuDevice, Resources);

        // Generate procedural 3D geometries
        _cubeMesh = MeshPrimitives.CreateCube(GpuDevice, 1.4f, Resources);
        _sphereMesh = MeshPrimitives.CreateSphere(GpuDevice, 0.6f, segments: 32, rings: 20, Resources);
        _planeMesh = MeshPrimitives.CreatePlane(GpuDevice, 16f, 16f, Resources);

        // Procedural crate texture (64x64 metallic grid pattern)
        _crateTexture = CreateCrateTexture(GpuDevice, 64, 64);
        _cubeMaterial = new Material("CrateMaterial", _crateTexture.GpuTexture, Vector4.One, specularPower: 16f, specularIntensity: 0.35f, Resources);

        // Sphere material (gold/amber specular reflection)
        _sphereMaterial = new Material("SphereMaterial", null, new Vector4(1f, 0.75f, 0.2f, 1f), specularPower: 64f, specularIntensity: 1.0f, Resources);

        // Ground plane material (slate blue diffuse)
        _planeMaterial = new Material("PlaneMaterial", null, new Vector4(0.2f, 0.25f, 0.35f, 1f), specularPower: 8f, specularIntensity: 0.15f, Resources);

        _dirLight = new DirectionalLight(new Vector3(-0.6f, -1.0f, 0.5f), new Vector3(1.0f, 0.96f, 0.88f), 1.2f);
        _ambientLight = new AmbientLight(new Vector3(0.2f, 0.25f, 0.35f), 0.35f);

        // Phase 8: Diagnostics Overlay
        _debugOverlay = new DebugOverlay(_frameStats, _renderStats, Resources) { Visible = false };

        Window.Resized += OnWindowResized;
    }

    private void CreateDepthBuffer(int width, int height)
    {
        _depthBuffer?.Dispose();
        _depthBuffer = GpuDevice.CreateTexture(
            "MainDepthBuffer",
            Math.Max(1, width),
            Math.Max(1, height),
            GpuFormat.D24_UNorm_S8_UInt,
            TextureUsage.DepthStencil);
    }

    private void OnWindowResized(int width, int height)
    {
        if (width <= 0 || height <= 0 || _camera == null) return;
        CreateDepthBuffer(width, height);
        _camera.AspectRatio = (float)width / height;
    }

    public override void Update(GameTime time)
    {
        Profiler.BeginFrame();

        using (Profiler.Scope("GameUpdate"))
        {
            float dt = time.DeltaTimeSeconds;

            if (Input.IsKeyPressed(Key.Escape))
            {
                Exit();
                return;
            }

            if (Input.IsKeyPressed(Key.Space))
            {
                _autoRotate = !_autoRotate;
            }

            // F3 toggles the Phase 8 Diagnostics Overlay
            if (Input.IsKeyPressed(Key.F3) && _debugOverlay != null)
            {
                _debugOverlay.Visible = !_debugOverlay.Visible;
            }

            // Camera orbit navigation
            float orbitSpeed = 1.8f * dt;
            if (Input.IsKeyDown(Key.Left) || Input.IsKeyDown(Key.A))
                _camera?.Orbit(orbitSpeed, 0f, 0f);
            if (Input.IsKeyDown(Key.Right) || Input.IsKeyDown(Key.D))
                _camera?.Orbit(-orbitSpeed, 0f, 0f);
            if (Input.IsKeyDown(Key.Up) || Input.IsKeyDown(Key.W))
                _camera?.Orbit(0f, orbitSpeed, 0f);
            if (Input.IsKeyDown(Key.Down) || Input.IsKeyDown(Key.S))
                _camera?.Orbit(0f, -orbitSpeed, 0f);
            if (Input.IsKeyDown(Key.Q) || Input.IsKeyDown(Key.PageUp))
                _camera?.Orbit(0f, 0f, 3.5f * dt);
            if (Input.IsKeyDown(Key.E) || Input.IsKeyDown(Key.PageDown))
                _camera?.Orbit(0f, 0f, -3.5f * dt);

            if (_autoRotate)
            {
                _cubeRotation += 1.0f * dt;
                _sphereOrbit += 1.5f * dt;
            }
        }
    }

    public override void Render(GameTime time)
    {
        if (_meshRenderer == null || _spriteBatch == null || _camera == null ||
            _cubeMesh == null || _sphereMesh == null || _planeMesh == null)
        {
            return;
        }

        _renderStats.Reset();
        var backBuffer = SwapChain.CurrentBackBuffer;

        // -------------------------------------------------------------
        // Pass 1: 3D Scene Pass with Depth Stencil and Lighting
        // -------------------------------------------------------------
        using (Profiler.Scope("Render3DScene"))
        {
            _renderStats.RecordRenderPass();
            _meshRenderer.Begin(
                backBuffer,
                _depthBuffer,
                _camera,
                _dirLight,
                _ambientLight,
                clearColor: true,
                clearDepth: true,
                clearColorValue: new Color4(0.08f, 0.10f, 0.14f, 1.0f));

            // Draw Ground Plane
            var planeWorld = Matrix4x4.CreateTranslation(0f, -0.8f, 0f);
            _meshRenderer.Draw(_planeMesh, _planeMaterial, in planeWorld);
            _renderStats.RecordDrawIndexed(_planeMesh.IndexCount, _planeMesh.VertexCount);

            // Draw Rotating Center Cube
            var cubeWorld = Matrix4x4.CreateRotationY(_cubeRotation) *
                            Matrix4x4.CreateRotationX(0.2f) *
                            Matrix4x4.CreateTranslation(0f, 0.6f, 0f);
            _meshRenderer.Draw(_cubeMesh, _cubeMaterial, in cubeWorld);
            _renderStats.RecordDrawIndexed(_cubeMesh.IndexCount, _cubeMesh.VertexCount);
            _renderStats.RecordTextureBind();

            // Draw Orbiting Sphere
            float orbitRadius = 2.4f;
            float sx = MathF.Cos(_sphereOrbit) * orbitRadius;
            float sz = MathF.Sin(_sphereOrbit) * orbitRadius;
            float sy = 0.6f + MathF.Sin(_sphereOrbit * 2f) * 0.4f;
            var sphereWorld = Matrix4x4.CreateTranslation(sx, sy, sz);
            _meshRenderer.Draw(_sphereMesh, _sphereMaterial, in sphereWorld);
            _renderStats.RecordDrawIndexed(_sphereMesh.IndexCount, _sphereMesh.VertexCount);

            _meshRenderer.End();
        }

        // -------------------------------------------------------------
        // Pass 2: 2D HUD Overlay (Rendered directly onto back buffer)
        // -------------------------------------------------------------
        using (Profiler.Scope("RenderHUD"))
        {
            _renderStats.RecordRenderPass();
            _spriteBatch.Begin(backBuffer);

            _spriteBatch.FillRectangle(new Vector2(16, 16), new Vector2(430, 165), new Color4(0f, 0f, 0f, 0.75f));
            _spriteBatch.DrawRectangle(new Vector2(16, 16), new Vector2(430, 165), new Color4(0.3f, 0.6f, 1.0f, 0.8f), 2f);

            _spriteBatch.DrawString("KHEFEST 3D ENGINE (PHASE 6 & 8)", new Vector2(28, 24), Color4.Yellow, 1.3f);
            _spriteBatch.DrawString($"FPS: {_frameStats.FramesPerSecond:F1} | Frame: {_frameStats.FrameTimeMilliseconds:F1} ms", new Vector2(28, 46), Color4.White, 1.05f);
            _spriteBatch.DrawString("Meshes: Cube, Sphere, Plane (Depth Buffer Active)", new Vector2(28, 64), Color4.Cyan, 1.05f);
            _spriteBatch.DrawString("Lighting: Blinn-Phong + Directional + Ambient", new Vector2(28, 82), Color4.Green, 1.05f);
            _spriteBatch.DrawString("Controls:", new Vector2(28, 104), Color4.Yellow, 1.0f);
            _spriteBatch.DrawString("  [WASD / Arrows] Orbit | [Q / E] Zoom", new Vector2(28, 120), Color4.White, 1.0f);
            _spriteBatch.DrawString("  [F3] Toggle Full Diagnostics HUD", new Vector2(28, 136), Color4.Green, 1.0f);
            _spriteBatch.DrawString("  [Space] Pause Spin | [Esc] Exit", new Vector2(28, 152), Color4.White, 1.0f);

            // If F3 is active, render the full diagnostics breakdown
            if (_debugOverlay != null && _debugOverlay.Visible)
            {
                _spriteBatch.FillRectangle(new Vector2(16, 195), new Vector2(500, 260), new Color4(0f, 0f, 0f, 0.88f));
                _spriteBatch.DrawRectangle(new Vector2(16, 195), new Vector2(500, 260), new Color4(0.2f, 0.8f, 0.4f, 0.85f), 2f);
                _debugOverlay.Draw(_spriteBatch, new Vector2(28, 205));
            }

            // Bottom right badge
            _spriteBatch.FillRectangle(new Vector2(Window.Width - 300, Window.Height - 48), new Vector2(284, 32), new Color4(0.1f, 0.2f, 0.3f, 0.85f));
            _spriteBatch.DrawString("Powered by Direct3D 11 & .NET 10", new Vector2(Window.Width - 290, Window.Height - 38), Color4.White, 1.0f);

            _spriteBatch.End();
            _renderStats.RecordDrawIndexed(SpriteBatch.MaxBatchIndices, SpriteBatch.MaxBatchVertices);
        }

        Profiler.EndFrame();
        _frameStats.Update(time.DeltaTimeSeconds);
    }

    private Texture2D CreateCrateTexture(IGpuDevice device, int width, int height)
    {
        var pixels = new byte[width * height * 4];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool isBorder = x < 4 || x >= width - 4 || y < 4 || y >= height - 4 ||
                                Math.Abs(x - y) < 3 || Math.Abs(x - (height - 1 - y)) < 3;
                bool isCross = (x > width / 2 - 2 && x < width / 2 + 2) || (y > height / 2 - 2 && y < height / 2 + 2);

                int idx = (y * width + x) * 4;
                if (isBorder || isCross)
                {
                    pixels[idx + 0] = 0x59; // R
                    pixels[idx + 1] = 0x42; // G
                    pixels[idx + 2] = 0x2A; // B
                    pixels[idx + 3] = 0xFF; // A
                }
                else
                {
                    bool checker = ((x / 8) + (y / 8)) % 2 == 0;
                    byte col = checker ? (byte)0x8C : (byte)0x60;
                    pixels[idx + 0] = col;
                    pixels[idx + 1] = (byte)(col * 0.85f);
                    pixels[idx + 2] = (byte)(col * 0.7f);
                    pixels[idx + 3] = 0xFF;
                }
            }
        }

        return Texture2D.FromRgba(device, "CrateTexture", width, height, pixels, Resources);
    }

    public override void Shutdown()
    {
        Window.Resized -= OnWindowResized;

        _depthBuffer?.Dispose();
        _cubeMesh?.Dispose();
        _sphereMesh?.Dispose();
        _planeMesh?.Dispose();
        _crateTexture?.Dispose();
        _cubeMaterial?.Dispose();
        _sphereMaterial?.Dispose();
        _planeMaterial?.Dispose();
        _meshRenderer?.Dispose();
        _spriteBatch?.Dispose();

        base.Shutdown();
    }
}

public static class Program
{
    [STAThread]
    public static void Main()
    {
        var game = new Hello3DGame();
        KhefestApp.Run(game);
    }
}
