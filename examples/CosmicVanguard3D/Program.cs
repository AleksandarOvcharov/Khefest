using System.Numerics;
using Khefest.Core.Configuration;
using Khefest.Core.Errors;
using Khefest.Core.Logging;
using Khefest.Graphics.Camera;
using Khefest.Graphics.Diagnostics;
using Khefest.Graphics.LowLevel;
using Khefest.Graphics.Mathematics;
using Khefest.Graphics.Texturing;
using Khefest.Graphics.ThreeD;
using Khefest.Graphics.ThreeD.Lighting;
using Khefest.Graphics.TwoD;
using Khefest.Input;
using Khefest.Windows.Application;
using Khefest.Windows.Timing;

namespace Khefest.Examples.CosmicVanguard3D;

/// <summary>
/// Cosmic Vanguard 3D: High-stress 3D space flight and asteroid destruction benchmark game.
/// Pushes Khefest's 3D rendering pipeline, depth-stencil buffering, Blinn-Phong lighting,
/// frustum culling, dynamic mesh transforms, and hybrid 2D/3D composite rendering to their limits.
///
/// Controls:
///   [W/S]     Pitch Down / Pitch Up
///   [A/D]     Yaw Left / Yaw Right
///   [Q/E]     Roll Left / Roll Right
///   [Shift]   Boost Forward / Throttle Up
///   [Space]   Fire Plasma Cannons
///   [C]       Toggle Frustum Culling (Benchmark comparison!)
///   [F/+]     Spawn +500 Asteroids (Stress Test!)
///   [-]       Remove 500 Asteroids
///   [L]       Toggle Dynamic Rotating Light
///   [V]       Toggle VSync
///   [F3]      Toggle In-Engine Diagnostics Overlay
///   [Esc]     Exit
/// </summary>
public sealed class CosmicVanguard3DGame : Game
{
    private static readonly ILogger Logger = LogManager.GetLogger(KhefestSubsystem.Core, "CosmicVanguard3D");

    // 3D Rendering Systems
    private MeshRenderer? _meshRenderer;
    private PerspectiveCamera? _camera;
    private IGpuTexture? _depthBuffer;
    private Mesh? _cubeMesh;
    private Mesh? _sphereMesh;
    private Material? _rockMaterial;
    private Material? _crystalMaterial;
    private Material? _goldMaterial;
    private DirectionalLight _dirLight = DirectionalLight.Default;
    private AmbientLight _ambientLight = new(new Vector3(0.15f, 0.18f, 0.25f), 0.6f);

    // 2D Composite HUD
    private SpriteBatch? _spriteBatch;
    private Texture2D? _reticleTexture;

    // Ship / Camera Dynamics
    private Vector3 _shipPos = new(0f, 0f, -80f);
    private float _pitch;
    private float _yaw;
    private float _roll;
    private float _throttle = 30f;
    private float _shipShield = 100f;
    private int _score;

    // 3D Celestial Objects
    private readonly List<Asteroid3D> _asteroids = new(3000);
    private readonly List<Laser3D> _lasers = new(200);

    // Diagnostics & Toggles
    private bool _frustumCullingEnabled = true;
    private bool _rotateLight = true;
    private bool _showDiagnostics = true;
    private int _drawnObjectCount;
    private int _culledObjectCount;
    private float _globalTime;
    private readonly FrameStatistics _frameStats = new();

    private struct Asteroid3D
    {
        public Vector3 Position;
        public Vector3 Velocity;
        public Vector3 RotationAxis;
        public float RotationSpeed;
        public float CurrentAngle;
        public float Scale;
        public bool IsSphere;
        public Material Material;
    }

    private struct Laser3D
    {
        public Vector3 Position;
        public Vector3 Direction;
        public float Speed;
        public float Life;
    }

    public override void Initialize()
    {
        Logger.Info("Initializing Cosmic Vanguard 3D Stress Test Game...");

        // 1. Perspective 3D Camera
        _camera = new PerspectiveCamera(
            _shipPos,
            _shipPos + Vector3.UnitZ,
            (float)Window.Width / Window.Height,
            65f,
            0.2f,
            2500f);

        // 2. High-Level 3D Mesh Renderer
        _meshRenderer = new MeshRenderer(GpuDevice, Resources);

        // 3. Create Hardware Depth-Stencil Buffer matching window resolution
        _depthBuffer = GpuDevice.CreateTexture(
            "CosmicDepthBuffer",
            Window.Width,
            Window.Height,
            GpuFormat.D24_UNorm_S8_UInt,
            TextureUsage.DepthStencil);

        // 4. Procedural Geometric Meshes
        _cubeMesh = MeshPrimitives.CreateCube(GpuDevice, 1.0f, Resources);
        _sphereMesh = MeshPrimitives.CreateSphere(GpuDevice, 0.6f, 16, 12, Resources);

        // 5. Materials
        _rockMaterial = new Material("RockMaterial", _meshRenderer.WhiteTexture.GpuTexture, new Vector4(0.65f, 0.62f, 0.58f, 1f), 16f, 0.2f, Resources);
        _crystalMaterial = new Material("CrystalMaterial", _meshRenderer.WhiteTexture.GpuTexture, new Vector4(0.2f, 0.85f, 0.95f, 1f), 64f, 0.8f, Resources);
        _goldMaterial = new Material("GoldMaterial", _meshRenderer.WhiteTexture.GpuTexture, new Vector4(0.95f, 0.75f, 0.2f, 1f), 48f, 0.6f, Resources);

        // 6. 2D SpriteBatch for Cockpit HUD
        _spriteBatch = new SpriteBatch(GpuDevice, Resources);

        // Procedural Reticle Crosshair Texture (32x32)
        var reticlePixels = new byte[32 * 32 * 4];
        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                int idx = (y * 32 + x) * 4;
                float dist = MathF.Sqrt(MathF.Pow(x - 15.5f, 2) + MathF.Pow(y - 15.5f, 2));
                bool isRing = Math.Abs(dist - 12f) < 1.2f;
                bool isCenterDot = dist < 2.2f;
                bool isCross = (Math.Abs(x - 15.5f) < 1.0f && Math.Abs(y - 15.5f) < 15f) ||
                               (Math.Abs(y - 15.5f) < 1.0f && Math.Abs(x - 15.5f) < 15f);

                if (isRing || isCenterDot || isCross)
                {
                    reticlePixels[idx + 0] = 0;
                    reticlePixels[idx + 1] = 255;
                    reticlePixels[idx + 2] = 220;
                    reticlePixels[idx + 3] = 220;
                }
            }
        }
        _reticleTexture = Texture2D.FromRgba(GpuDevice, "CockpitReticle", 32, 32, reticlePixels, Resources);

        // 7. Populate initial 3D asteroid belt (800 objects)
        SpawnAsteroids(800);

        Logger.Info($"Cosmic Vanguard 3D initialized with {_asteroids.Count} 3D objects.");
    }

    private void SpawnAsteroids(int count)
    {
        var rng = new Random(1337);
        for (int i = 0; i < count; i++)
        {
            float radius = 50f + rng.NextSingle() * 450f;
            float angle = rng.NextSingle() * MathF.Tau;
            float height = (rng.NextSingle() - 0.5f) * 120f;

            var pos = new Vector3(
                MathF.Cos(angle) * radius,
                height,
                MathF.Sin(angle) * radius + 150f);

            var vel = Vector3.Normalize(new Vector3(-pos.Z, 0f, pos.X)) * (10f + rng.NextSingle() * 25f);
            var axis = Vector3.Normalize(new Vector3(rng.NextSingle(), rng.NextSingle(), rng.NextSingle()));

            var mat = (i % 5) switch
            {
                0 => _crystalMaterial!,
                1 => _goldMaterial!,
                _ => _rockMaterial!
            };

            _asteroids.Add(new Asteroid3D
            {
                Position = pos,
                Velocity = vel,
                RotationAxis = axis,
                RotationSpeed = rng.NextSingle() * 2.0f - 1.0f,
                CurrentAngle = rng.NextSingle() * MathF.Tau,
                Scale = 1.5f + rng.NextSingle() * 5.0f,
                IsSphere = rng.Next(2) == 0,
                Material = mat
            });
        }
    }

    public override void Update(GameTime time)
    {
        float dt = time.DeltaTimeSeconds;
        _globalTime += dt;
        _frameStats.Update(dt);

        if (Input.IsKeyPressed(Key.Escape)) { Exit(); return; }

        // Stress controls
        if (Input.IsKeyPressed(Key.F) || Input.IsKeyPressed(Key.Add) || Input.IsKeyPressed(Key.Equals))
        {
            SpawnAsteroids(500);
        }
        if (Input.IsKeyPressed(Key.Minus) || Input.IsKeyPressed(Key.Subtract))
        {
            if (_asteroids.Count > 100)
            {
                _asteroids.RemoveRange(_asteroids.Count - 200, 200);
            }
        }
        if (Input.IsKeyPressed(Key.C)) _frustumCullingEnabled = !_frustumCullingEnabled;
        if (Input.IsKeyPressed(Key.L)) _rotateLight = !_rotateLight;
        if (Input.IsKeyPressed(Key.F3)) _showDiagnostics = !_showDiagnostics;

        // Dynamic light rotation
        if (_rotateLight)
        {
            float lx = MathF.Cos(_globalTime * 0.4f);
            float lz = MathF.Sin(_globalTime * 0.4f);
            _dirLight = new DirectionalLight(Vector3.Normalize(new Vector3(lx, -0.6f, lz)), new Vector3(1f, 0.95f, 0.85f), 1.2f);
        }

        // Flight controls
        if (Input.IsKeyDown(Key.W)) _pitch -= 1.8f * dt;
        if (Input.IsKeyDown(Key.S)) _pitch += 1.8f * dt;
        if (Input.IsKeyDown(Key.A)) _yaw -= 2.2f * dt;
        if (Input.IsKeyDown(Key.D)) _yaw += 2.2f * dt;
        if (Input.IsKeyDown(Key.Q)) _roll += 2.5f * dt;
        if (Input.IsKeyDown(Key.E)) _roll -= 2.5f * dt;

        _pitch = Math.Clamp(_pitch, -MathF.PI * 0.45f, MathF.PI * 0.45f);

        // Throttle
        if (Input.IsKeyDown(Key.LeftShift))
        {
            _throttle = Math.Min(_throttle + 60f * dt, 150f);
        }
        else
        {
            _throttle = Math.Max(_throttle - 30f * dt, 30f);
        }

        // Compute forward and right vectors from pitch/yaw/roll
        var rotMatrix = Matrix4x4.CreateFromYawPitchRoll(_yaw, _pitch, _roll);
        var forward = Vector3.TransformNormal(-Vector3.UnitZ, rotMatrix);
        var up = Vector3.TransformNormal(Vector3.UnitY, rotMatrix);

        _shipPos += forward * _throttle * dt;

        // Update Camera
        if (_camera != null)
        {
            _camera.Position = _shipPos;
            _camera.Target = _shipPos + forward * 10f;
            _camera.Up = up;
        }

        // Firing lasers
        if (Input.IsKeyPressed(Key.Space) || Input.IsMouseButtonPressed(MouseButton.Left))
        {
            _lasers.Add(new Laser3D
            {
                Position = _shipPos + forward * 2f,
                Direction = forward,
                Speed = 400f,
                Life = 0f
            });
        }

        // Update lasers & 3D ray collision with asteroids
        for (int i = _lasers.Count - 1; i >= 0; i--)
        {
            var laser = _lasers[i];
            laser.Position += laser.Direction * laser.Speed * dt;
            laser.Life += dt;

            if (laser.Life > 2.5f)
            {
                _lasers.RemoveAt(i);
                continue;
            }

            // Check collision against asteroids (BoundingSphere test)
            bool hit = false;
            for (int a = _asteroids.Count - 1; a >= 0; a--)
            {
                var ast = _asteroids[a];
                float dist = Vector3.Distance(laser.Position, ast.Position);
                if (dist < ast.Scale * 1.2f)
                {
                    hit = true;
                    _score += (int)(ast.Scale * 25f);
                    _asteroids.RemoveAt(a);
                    break;
                }
            }

            if (hit)
            {
                _lasers.RemoveAt(i);
                continue;
            }

            _lasers[i] = laser;
        }

        // Update asteroids
        for (int i = 0; i < _asteroids.Count; i++)
        {
            var ast = _asteroids[i];
            ast.Position += ast.Velocity * dt;
            ast.CurrentAngle += ast.RotationSpeed * dt;
            _asteroids[i] = ast;

            // Damage ship on close proximity
            float distToShip = Vector3.Distance(ast.Position, _shipPos);
            if (distToShip < ast.Scale + 1.5f)
            {
                _shipShield = Math.Max(0f, _shipShield - 15f * dt);
            }
        }
    }

    public override void Render(GameTime time)
    {
        if (_meshRenderer == null || _camera == null || _spriteBatch == null || _depthBuffer == null) return;

        var backBuffer = SwapChain.CurrentBackBuffer;
        RenderStatistics.Current.Reset();

        // 1. Begin 3D Render Session with Depth Buffer
        _meshRenderer.Begin(
            backBuffer,
            _depthBuffer,
            _camera,
            _dirLight,
            _ambientLight,
            clearColor: true,
            clearDepth: true,
            clearColorValue: new Color4(0.015f, 0.02f, 0.04f, 1f));

        var frustum = new BoundingFrustum(_camera.ViewProjectionMatrix);
        _drawnObjectCount = 0;
        _culledObjectCount = 0;

        // Render 3D Asteroids with Frustum Culling
        for (int i = 0; i < _asteroids.Count; i++)
        {
            var ast = _asteroids[i];
            var box = new BoundingBox(
                ast.Position - new Vector3(ast.Scale),
                ast.Position + new Vector3(ast.Scale));

            if (_frustumCullingEnabled && !frustum.Intersects(box))
            {
                _culledObjectCount++;
                continue;
            }

            var world = Matrix4x4.CreateScale(ast.Scale) *
                        Matrix4x4.CreateFromAxisAngle(ast.RotationAxis, ast.CurrentAngle) *
                        Matrix4x4.CreateTranslation(ast.Position);

            var mesh = ast.IsSphere ? _sphereMesh! : _cubeMesh!;
            _meshRenderer.Draw(mesh, ast.Material, world);
            _drawnObjectCount++;
        }

        // Render Lasers as stretched 3D cubes
        for (int i = 0; i < _lasers.Count; i++)
        {
            var laser = _lasers[i];
            var world = Matrix4x4.CreateScale(0.3f, 0.3f, 6.0f) *
                        Matrix4x4.CreateTranslation(laser.Position);
            _meshRenderer.Draw(_cubeMesh!, _crystalMaterial, world);
            _drawnObjectCount++;
        }

        _meshRenderer.End();

        // 2. Render 2D Cockpit HUD & Diagnostics Overlay
        _spriteBatch.Begin(backBuffer, camera: null, SpriteSortMode.Deferred);

        // Center Reticle
        if (_reticleTexture != null)
        {
            var screenCenter = new Vector2(Window.Width * 0.5f - 16f, Window.Height * 0.5f - 16f);
            _spriteBatch.Draw(_reticleTexture, screenCenter, Color4.Cyan);
        }

        // Top Left: Mission HUD
        _spriteBatch.FillRectangle(new Vector2(12, 12), new Vector2(440, 150), new Color4(0f, 0.02f, 0.06f, 0.85f));
        _spriteBatch.DrawRectangle(new Vector2(12, 12), new Vector2(440, 150), Color4.CornflowerBlue, 1.5f);

        _spriteBatch.DrawString("COSMIC VANGUARD 3D - MAXIMUM STRESS TEST", new Vector2(22, 18), Color4.Yellow, 1.15f);
        _spriteBatch.DrawString($"Performance: {_frameStats.FramesPerSecond:F1} FPS | {_frameStats.FrameTimeMilliseconds:F2} ms", new Vector2(22, 38), Color4.White, 1.05f);
        _spriteBatch.DrawString($"Asteroids Total: {_asteroids.Count:N0} | Active Lasers: {_lasers.Count}", new Vector2(22, 58), Color4.Cyan, 1.0f);
        _spriteBatch.DrawString($"Drawn Meshes: {_drawnObjectCount:N0} | Culled: {_culledObjectCount:N0} (Frustum: {(_frustumCullingEnabled ? "ON" : "OFF")})", new Vector2(22, 78), _frustumCullingEnabled ? Color4.Green : Color4.Orange, 1.0f);
        _spriteBatch.DrawString($"Throttle: {_throttle:F0} m/s | Score: {_score:N0} | Shield: {_shipShield:F0}%", new Vector2(22, 98), Color4.White, 1.0f);
        _spriteBatch.DrawString("[W/S/A/D/Q/E] Flight | [Space] Fire | [C] Frustum | [F/+] +500", new Vector2(22, 120), Color4.LightGray, 0.88f);

        // Shield Gauge Bar
        _spriteBatch.FillRectangle(new Vector2(22, 142), new Vector2(220f * (_shipShield / 100f), 8f), _shipShield > 30 ? Color4.Cyan : Color4.Red);
        _spriteBatch.DrawRectangle(new Vector2(22, 142), new Vector2(220f, 8f), Color4.White, 1f);

        // Top Right: Live GPU Diagnostics
        if (_showDiagnostics)
        {
            var stats = RenderStatistics.Current;
            _spriteBatch.FillRectangle(new Vector2(Window.Width - 360, 12), new Vector2(348, 120), new Color4(0f, 0.02f, 0.06f, 0.85f));
            _spriteBatch.DrawRectangle(new Vector2(Window.Width - 360, 12), new Vector2(348, 120), Color4.Yellow, 1.5f);

            _spriteBatch.DrawString("RENDER PIPELINE TELEMETRY", new Vector2(Window.Width - 348, 18), Color4.Yellow, 1.05f);
            _spriteBatch.DrawString($"Draw Calls: {stats.TotalDrawCalls:N0}", new Vector2(Window.Width - 348, 38), Color4.White, 1.0f);
            _spriteBatch.DrawString($"Triangles: {stats.TriangleCount:N0} | Vertices: {stats.VertexCount:N0}", new Vector2(Window.Width - 348, 58), Color4.Cyan, 1.0f);
            _spriteBatch.DrawString($"Texture Binds: {stats.TextureBinds:N0} | Depth: D24_UNorm_S8", new Vector2(Window.Width - 348, 78), Color4.LightGray, 0.95f);
            _spriteBatch.DrawString($"Adapter: {GpuDevice.AdapterName}", new Vector2(Window.Width - 348, 98), Color4.LightGray, 0.85f);
        }

        _spriteBatch.End();
    }

    public override void Shutdown()
    {
        _reticleTexture?.Dispose();
        _spriteBatch?.Dispose();
        _cubeMesh?.Dispose();
        _sphereMesh?.Dispose();
        _rockMaterial?.Dispose();
        _crystalMaterial?.Dispose();
        _goldMaterial?.Dispose();
        _depthBuffer?.Dispose();
        _meshRenderer?.Dispose();
        base.Shutdown();
    }
}

public static class Program
{
    [STAThread]
    public static void Main()
    {
        var config = new KhefestConfigBuilder()
            .ConfigureWindow(w => w with { Title = "Khefest - Cosmic Vanguard 3D Benchmark Game", Width = 1600, Height = 900, VSync = false })
            .Build();

        var game = new CosmicVanguard3DGame();
        KhefestApp.Run(game, config);
    }
}
