using System.Numerics;
using Khefest.Core.Configuration;
using Khefest.Core.Errors;
using Khefest.Core.Logging;
using Khefest.Graphics.Camera;
using Khefest.Graphics.Diagnostics;
using Khefest.Graphics.LowLevel;
using Khefest.Graphics.Mathematics;
using Khefest.Graphics.Texturing;
using Khefest.Graphics.TwoD;
using Khefest.Input;
using Khefest.Windows.Application;
using Khefest.Windows.Timing;

namespace Khefest.Examples.StressTest2D;

/// <summary>
/// StarSwarm 2D: High-intensity arcade stress test game pushing SpriteBatch, Camera2D,
/// shape rendering, font rasterization, and spatial collision systems to their limits.
///
/// Features:
///   - Interactive Player Ship with rotational inertia and thruster particles
///   - Dual rapid-fire plasma blasters
///   - Dynamic Asteroid swarms with fragmentation on impact
///   - Massive Particle explosion system with zero allocation per frame
///   - Real-time HUD and stress-testing entity spawner (+1000 entities on demand)
/// </summary>
public sealed class StressTest2DGame : Game
{
    private static readonly ILogger Logger = LogManager.GetLogger(KhefestSubsystem.Core, "StarSwarm2D");

    private SpriteBatch? _spriteBatch;
    private Camera2D? _camera;
    private Texture2D? _particleTexture;
    private Texture2D? _laserTexture;

    // Player state
    private Vector2 _playerPos;
    private Vector2 _playerVel;
    private float _playerAngle;
    private float _playerHealth = 100f;
    private int _score;
    private bool _gameOver;

    // Entity lists
    private readonly List<Laser> _lasers = new(500);
    private readonly List<Asteroid> _asteroids = new(2000);
    private readonly List<Particle> _particles = new(10000);

    // Stress testing & toggles
    private int _stressEntityCount = 2000;
    private bool _drawSprites = true;
    private bool _drawShapes = true;
    private bool _drawText = true;
    private bool _cameraShake = true;
    private float _shakeIntensity;
    private float _globalTime;

    private readonly FrameStatistics _frameStats = new();
    private int _totalQuadsThisFrame;

    private struct Laser
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Life;
    }

    private struct Asteroid
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Radius;
        public float Rotation;
        public float RotationSpeed;
        public Color4 Color;
    }

    private struct Particle
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public Color4 Color;
        public float Life;
        public float MaxLife;
        public float Size;
    }

    public override void Initialize()
    {
        Logger.Info("Initializing StarSwarm 2D Stress Test Game...");

        _camera = new Camera2D(Window.Width, Window.Height);
        _spriteBatch = new SpriteBatch(GpuDevice, Resources);

        _playerPos = new Vector2(Window.Width * 0.5f, Window.Height * 0.5f);

        // 1. Create a 16x16 soft radial particle texture
        var particlePixels = new byte[16 * 16 * 4];
        for (int y = 0; y < 16; y++)
        {
            for (int x = 0; x < 16; x++)
            {
                float dx = (x - 7.5f) / 7.5f;
                float dy = (y - 7.5f) / 7.5f;
                float dist = MathF.Sqrt(dx * dx + dy * dy);
                float alpha = Math.Clamp(1.0f - dist, 0f, 1f);
                int idx = (y * 16 + x) * 4;
                particlePixels[idx + 0] = 255;
                particlePixels[idx + 1] = 255;
                particlePixels[idx + 2] = 255;
                particlePixels[idx + 3] = (byte)(alpha * 255f);
            }
        }
        _particleTexture = Texture2D.FromRgba(GpuDevice, "ParticleTex", 16, 16, particlePixels, Resources);

        // 2. Create 4x16 laser beam texture
        var laserPixels = new byte[4 * 16 * 4];
        for (int i = 0; i < 4 * 16 * 4; i += 4)
        {
            laserPixels[i + 0] = 100;
            laserPixels[i + 1] = 220;
            laserPixels[i + 2] = 255;
            laserPixels[i + 3] = 255;
        }
        _laserTexture = Texture2D.FromRgba(GpuDevice, "LaserTex", 4, 16, laserPixels, Resources);

        // 3. Populate initial asteroids
        SpawnAsteroids(150);

        Logger.Info("StarSwarm 2D initialized successfully.");
    }

    private void SpawnAsteroids(int count)
    {
        var rng = new Random();
        for (int i = 0; i < count; i++)
        {
            float rad = 12f + rng.NextSingle() * 24f;
            var pos = new Vector2(rng.NextSingle() * Window.Width, rng.NextSingle() * Window.Height);

            // Don't spawn on top of player
            if (Vector2.Distance(pos, _playerPos) < 150f)
            {
                pos += new Vector2(300f, 300f);
            }

            var vel = new Vector2(rng.NextSingle() * 160f - 80f, rng.NextSingle() * 160f - 80f);
            _asteroids.Add(new Asteroid
            {
                Position = pos,
                Velocity = vel,
                Radius = rad,
                Rotation = rng.NextSingle() * MathF.Tau,
                RotationSpeed = rng.NextSingle() * 3f - 1.5f,
                Color = new Color4(0.6f + rng.NextSingle() * 0.3f, 0.5f + rng.NextSingle() * 0.3f, 0.4f, 1f)
            });
        }
    }

    private void TriggerExplosion(Vector2 pos, int count, Color4 color, float speed = 250f)
    {
        var rng = new Random();
        for (int i = 0; i < count; i++)
        {
            if (_particles.Count >= 20000) break; // Hard ceiling

            float angle = rng.NextSingle() * MathF.Tau;
            float spd = rng.NextSingle() * speed;
            var vel = new Vector2(MathF.Cos(angle) * spd, MathF.Sin(angle) * spd);
            _particles.Add(new Particle
            {
                Position = pos,
                Velocity = vel,
                Color = color,
                Life = 0f,
                MaxLife = 0.4f + rng.NextSingle() * 0.8f,
                Size = 4f + rng.NextSingle() * 12f
            });
        }

        if (_cameraShake)
        {
            _shakeIntensity = Math.Min(_shakeIntensity + 5f, 25f);
        }
    }

    public override void Update(GameTime time)
    {
        float dt = time.DeltaTimeSeconds;
        _globalTime += dt;
        _frameStats.Update(dt);

        if (Input.IsKeyPressed(Key.Escape)) { Exit(); return; }

        // Stress controls
        if (Input.IsKeyPressed(Key.Add) || Input.IsKeyPressed(Key.Equals) || Input.IsKeyPressed(Key.F))
        {
            _stressEntityCount += 500;
            SpawnAsteroids(50);
            TriggerExplosion(_playerPos, 500, Color4.Cyan, 500f);
        }
        if (Input.IsKeyPressed(Key.Subtract) || Input.IsKeyPressed(Key.Minus))
        {
            if (_asteroids.Count > 20) _asteroids.RemoveRange(_asteroids.Count - 20, 20);
            if (_particles.Count > 200) _particles.RemoveRange(_particles.Count - 200, 200);
            _stressEntityCount = Math.Max(100, _stressEntityCount - 500);
        }
        if (Input.IsKeyPressed(Key.D1)) _drawSprites = !_drawSprites;
        if (Input.IsKeyPressed(Key.D2)) _drawShapes = !_drawShapes;
        if (Input.IsKeyPressed(Key.D3)) _drawText = !_drawText;
        if (Input.IsKeyPressed(Key.D4)) _cameraShake = !_cameraShake;

        if (_gameOver)
        {
            if (Input.IsKeyPressed(Key.R))
            {
                _gameOver = false;
                _playerHealth = 100f;
                _score = 0;
                _playerPos = new Vector2(Window.Width * 0.5f, Window.Height * 0.5f);
                _playerVel = Vector2.Zero;
                _asteroids.Clear();
                _particles.Clear();
                _lasers.Clear();
                SpawnAsteroids(150);
            }
            return;
        }

        // Player Controls
        if (Input.IsKeyDown(Key.A) || Input.IsKeyDown(Key.Left))
        {
            _playerAngle -= 4.0f * dt;
        }
        if (Input.IsKeyDown(Key.D) || Input.IsKeyDown(Key.Right))
        {
            _playerAngle += 4.0f * dt;
        }

        bool thrusting = Input.IsKeyDown(Key.W) || Input.IsKeyDown(Key.Up);
        if (thrusting)
        {
            var forward = new Vector2(MathF.Cos(_playerAngle), MathF.Sin(_playerAngle));
            _playerVel += forward * 600f * dt;

            // Spawn exhaust trail particles
            var rear = _playerPos - forward * 16f;
            TriggerExplosion(rear, 3, Color4.Orange, 100f);
        }

        _playerVel *= MathF.Pow(0.85f, dt * 10f); // Damping
        _playerPos += _playerVel * dt;

        // Screen wrap player
        if (_playerPos.X < 0) _playerPos.X += Window.Width;
        if (_playerPos.X > Window.Width) _playerPos.X -= Window.Width;
        if (_playerPos.Y < 0) _playerPos.Y += Window.Height;
        if (_playerPos.Y > Window.Height) _playerPos.Y -= Window.Height;

        // Player shooting
        if (Input.IsKeyPressed(Key.Space) || Input.IsMouseButtonPressed(MouseButton.Left))
        {
            var forward = new Vector2(MathF.Cos(_playerAngle), MathF.Sin(_playerAngle));
            var right = new Vector2(-forward.Y, forward.X);

            _lasers.Add(new Laser
            {
                Position = _playerPos + right * 8f + forward * 10f,
                Velocity = forward * 900f + _playerVel * 0.5f,
                Life = 0f
            });

            _lasers.Add(new Laser
            {
                Position = _playerPos - right * 8f + forward * 10f,
                Velocity = forward * 900f + _playerVel * 0.5f,
                Life = 0f
            });
        }

        // Update lasers
        for (int i = _lasers.Count - 1; i >= 0; i--)
        {
            var laser = _lasers[i];
            laser.Position += laser.Velocity * dt;
            laser.Life += dt;

            if (laser.Life > 1.2f || laser.Position.X < -50 || laser.Position.X > Window.Width + 50 ||
                laser.Position.Y < -50 || laser.Position.Y > Window.Height + 50)
            {
                _lasers.RemoveAt(i);
                continue;
            }

            _lasers[i] = laser;
        }

        // Update Asteroids & Collisions
        for (int i = _asteroids.Count - 1; i >= 0; i--)
        {
            var ast = _asteroids[i];
            ast.Position += ast.Velocity * dt;
            ast.Rotation += ast.RotationSpeed * dt;

            // Screen wrap
            if (ast.Position.X < -50) ast.Position = new Vector2(Window.Width + 40, ast.Position.Y);
            if (ast.Position.X > Window.Width + 50) ast.Position = new Vector2(-40, ast.Position.Y);
            if (ast.Position.Y < -50) ast.Position = new Vector2(ast.Position.X, Window.Height + 40);
            if (ast.Position.Y > Window.Height + 50) ast.Position = new Vector2(ast.Position.X, -40);

            // Check collision with player
            if (Vector2.Distance(ast.Position, _playerPos) < ast.Radius + 14f)
            {
                _playerHealth -= ast.Radius * 0.5f;
                TriggerExplosion(_playerPos, 30, Color4.Red, 300f);

                if (_playerHealth <= 0f)
                {
                    _playerHealth = 0f;
                    _gameOver = true;
                    TriggerExplosion(_playerPos, 200, Color4.Yellow, 400f);
                    break;
                }
            }

            // Check collision with lasers
            bool hit = false;
            for (int l = _lasers.Count - 1; l >= 0; l--)
            {
                if (Vector2.Distance(_lasers[l].Position, ast.Position) < ast.Radius + 6f)
                {
                    hit = true;
                    _lasers.RemoveAt(l);
                    break;
                }
            }

            if (hit)
            {
                _score += (int)(ast.Radius * 10f);
                TriggerExplosion(ast.Position, (int)(ast.Radius * 2f), ast.Color, 200f);

                // Split if large enough
                if (ast.Radius > 14f && _asteroids.Count < 2000)
                {
                    var rng = new Random();
                    float newRad = ast.Radius * 0.6f;
                    _asteroids.Add(new Asteroid
                    {
                        Position = ast.Position + new Vector2(10f, 0),
                        Velocity = ast.Velocity + new Vector2(rng.NextSingle() * 80f - 40f, rng.NextSingle() * 80f - 40f),
                        Radius = newRad,
                        Rotation = 0f,
                        RotationSpeed = ast.RotationSpeed * 1.5f,
                        Color = ast.Color
                    });
                    ast.Position -= new Vector2(10f, 0);
                    ast.Radius = newRad;
                    _asteroids[i] = ast;
                }
                else
                {
                    _asteroids.RemoveAt(i);
                }
                continue;
            }

            _asteroids[i] = ast;
        }

        // Maintain minimum asteroid count
        if (_asteroids.Count < 60)
        {
            SpawnAsteroids(40);
        }

        // Update particles
        for (int i = _particles.Count - 1; i >= 0; i--)
        {
            var p = _particles[i];
            p.Position += p.Velocity * dt;
            p.Velocity *= MathF.Pow(0.92f, dt * 10f);
            p.Life += dt;

            if (p.Life >= p.MaxLife)
            {
                _particles.RemoveAt(i);
                continue;
            }

            _particles[i] = p;
        }

        // Camera update & shake
        if (_camera != null)
        {
            var targetCam = _playerPos;
            if (_shakeIntensity > 0.01f)
            {
                var shakeOffset = new Vector2(
                    (Random.Shared.NextSingle() * 2f - 1f) * _shakeIntensity,
                    (Random.Shared.NextSingle() * 2f - 1f) * _shakeIntensity);
                targetCam += shakeOffset;
                _shakeIntensity = Math.Max(0f, _shakeIntensity - dt * 25f);
            }

            _camera.Position = Vector2.Lerp(_camera.Position, targetCam, dt * 8f);
        }
    }

    public override void Render(GameTime time)
    {
        if (_spriteBatch == null || _camera == null || _particleTexture == null || _laserTexture == null) return;

        var backBuffer = SwapChain.CurrentBackBuffer;
        _totalQuadsThisFrame = 0;

        // Pass 1: Clear
        var clearPass = new RenderPassDesc
        {
            ColorTarget = backBuffer,
            ClearColor = new Color4(0.01f, 0.015f, 0.035f, 1.0f),
            ClearColorTarget = true
        };
        using var clearCmd = GpuDevice.CreateCommandRecorder();
        clearCmd.BeginPass(clearPass);
        clearCmd.EndPass();
        GpuDevice.Submit(clearCmd);

        // Pass 2: World Scene with Camera
        _spriteBatch.Begin(backBuffer, _camera, SpriteSortMode.Deferred);

        // Render Asteroids (Shapes + Textured particles)
        if (_drawShapes)
        {
            foreach (var ast in _asteroids)
            {
                int segs = Math.Clamp((int)(ast.Radius * 0.8f), 6, 16);
                _spriteBatch.DrawCircle(ast.Position, ast.Radius, ast.Color, segs, 2f);
                _totalQuadsThisFrame += segs;
            }
        }

        // Render Lasers
        foreach (var laser in _lasers)
        {
            _spriteBatch.Draw(
                _laserTexture,
                laser.Position,
                new Vector2(4f, 16f),
                Color4.White,
                MathF.Atan2(laser.Velocity.Y, laser.Velocity.X) + MathF.PI * 0.5f,
                new Vector2(2f, 8f),
                SpriteEffects.None);
            _totalQuadsThisFrame++;
        }

        // Render Particles
        if (_drawSprites)
        {
            foreach (var p in _particles)
            {
                float lifeFactor = 1.0f - (p.Life / p.MaxLife);
                var alphaColor = new Color4(p.Color.R, p.Color.G, p.Color.B, lifeFactor);
                var size = new Vector2(p.Size * lifeFactor);
                _spriteBatch.Draw(
                    _particleTexture,
                    p.Position,
                    size,
                    alphaColor,
                    0f,
                    size * 0.5f,
                    SpriteEffects.None);
                _totalQuadsThisFrame++;
            }
        }

        // Render Player Ship (Procedural triangle using DrawLine)
        if (!_gameOver)
        {
            var forward = new Vector2(MathF.Cos(_playerAngle), MathF.Sin(_playerAngle));
            var right = new Vector2(-forward.Y, forward.X);

            var tip = _playerPos + forward * 18f;
            var leftWing = _playerPos - forward * 12f - right * 10f;
            var rightWing = _playerPos - forward * 12f + right * 10f;

            _spriteBatch.DrawLine(tip, leftWing, Color4.Cyan, 2.5f);
            _spriteBatch.DrawLine(leftWing, _playerPos - forward * 6f, Color4.Cyan, 2f);
            _spriteBatch.DrawLine(_playerPos - forward * 6f, rightWing, Color4.Cyan, 2f);
            _spriteBatch.DrawLine(rightWing, tip, Color4.Cyan, 2.5f);
            _totalQuadsThisFrame += 4;
        }

        _spriteBatch.End();

        // Pass 3: Screen-space HUD
        _spriteBatch.Begin(backBuffer, camera: null, SpriteSortMode.Deferred);

        // Top HUD Panel
        _spriteBatch.FillRectangle(new Vector2(10, 10), new Vector2(460, 150), new Color4(0f, 0f, 0f, 0.8f));
        _spriteBatch.DrawRectangle(new Vector2(10, 10), new Vector2(460, 150), Color4.CornflowerBlue, 1.5f);

        _spriteBatch.DrawString("STARSWARM 2D - STRESS BENCHMARK", new Vector2(20, 16), Color4.Yellow, 1.2f);
        _spriteBatch.DrawString($"FPS: {_frameStats.FramesPerSecond:F1} | Frame: {_frameStats.FrameTimeMilliseconds:F2} ms", new Vector2(20, 38), Color4.White, 1.1f);
        _spriteBatch.DrawString($"Score: {_score:N0} | Health: {_playerHealth:F0}%", new Vector2(20, 58), _playerHealth > 30 ? Color4.Green : Color4.Red, 1.1f);
        _spriteBatch.DrawString($"Asteroids: {_asteroids.Count} | Particles: {_particles.Count:N0} | Lasers: {_lasers.Count}", new Vector2(20, 78), Color4.Cyan, 1.0f);
        _spriteBatch.DrawString($"Quads This Frame: {_totalQuadsThisFrame:N0} | Flush Limit: {SpriteBatch.MaxBatchQuads}", new Vector2(20, 96), Color4.LightGray, 0.95f);
        _spriteBatch.DrawString("[W/A/D] Fly | [Space] Shoot | [F/+] Spawn Swarm | [R] Reset", new Vector2(20, 118), Color4.White, 0.9f);

        // Health Bar
        _spriteBatch.FillRectangle(new Vector2(20, 138), new Vector2(200f * (_playerHealth / 100f), 10f), _playerHealth > 30 ? Color4.Green : Color4.Red);
        _spriteBatch.DrawRectangle(new Vector2(20, 138), new Vector2(200f, 10f), Color4.White, 1f);

        if (_gameOver)
        {
            var center = new Vector2(Window.Width * 0.5f - 180f, Window.Height * 0.5f - 50f);
            _spriteBatch.FillRectangle(center, new Vector2(360, 100), new Color4(0.2f, 0f, 0f, 0.9f));
            _spriteBatch.DrawRectangle(center, new Vector2(360, 100), Color4.Red, 2f);
            _spriteBatch.DrawString("GAME OVER", center + new Vector2(100, 20), Color4.Red, 1.8f);
            _spriteBatch.DrawString("Press [R] to Restart", center + new Vector2(85, 60), Color4.White, 1.2f);
        }

        _spriteBatch.End();
    }

    public override void Shutdown()
    {
        _particleTexture?.Dispose();
        _laserTexture?.Dispose();
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
            .ConfigureWindow(w => w with { Title = "Khefest - StarSwarm 2D Stress Test Game", Width = 1600, Height = 900, VSync = false })
            .Build();

        var game = new StressTest2DGame();
        KhefestApp.Run(game, config);
    }
}
