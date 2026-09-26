using System.Numerics;
using System.Runtime.InteropServices;
using Khefest.Core.Configuration;
using Khefest.Core.Errors;
using Khefest.Core.Logging;
using Khefest.Graphics.Camera;
using Khefest.Graphics.Diagnostics;
using Khefest.Graphics.LowLevel;
using Khefest.Graphics.Mathematics;
using Khefest.Graphics.PostProcessing;
using Khefest.Graphics.Texturing;
using Khefest.Graphics.TwoD;
using Khefest.Input;
using Khefest.Windows.Application;
using Khefest.Windows.Timing;

namespace Khefest.Examples.RetroTankArena;

#region Data Structures & PostFX

[StructLayout(LayoutKind.Sequential)]
public struct PostFxUniforms
{
    public float Time;
    public float Vignette;
    public float Aberration;
    public float HitFlash;
}

public enum WallType { Concrete, Brick }

public sealed class Obstacle
{
    public Vector2 Position;
    public Vector2 Size;
    public WallType Type;
    public int Health;
    public int MaxHealth;

    public Rect2D Bounds => new(Position.X - Size.X * 0.5f, Position.Y - Size.Y * 0.5f, Size.X, Size.Y);
}

public sealed class Projectile
{
    public Vector2 Position;
    public Vector2 Velocity;
    public bool IsPlayer;
    public float Damage;
    public int BouncesLeft;
    public float Life;
    public float MaxLife = 3.5f;
}

public sealed class Landmine
{
    public Vector2 Position;
    public float Timer;
    public bool Armed;
    public bool IsPlayer;
}

public enum PickupType { Health, Ammo, Mine, Shield }

public sealed class Pickup
{
    public Vector2 Position;
    public PickupType Type;
    public float PulseTimer;
}

public sealed class TreadMark
{
    public Vector2 Position;
    public float Rotation;
    public float Alpha;
}

public sealed class ArenaParticle
{
    public Vector2 Position;
    public Vector2 Velocity;
    public Color4 Color;
    public float Size;
    public float Life;
    public float MaxLife;
}

public enum EnemyType { Scout, BattleTank, Heavy }

public sealed class EnemyTank
{
    public EnemyType Type;
    public Vector2 Position;
    public float HullAngle;
    public float TurretAngle;
    public float Health;
    public float MaxHealth;
    public float Speed;
    public float FireCooldown;
    public float FireInterval;
    public Vector2 PatrolTarget;
    public float RetargetTimer;
}

#endregion

/// <summary>
/// Retro Tank Arena: Complete top-down combat game showcasing how Khefest naturally blends
/// high-level rendering (SpriteBatch, Camera2D, 2D Primitives) with direct low-level GPU control
/// (RenderTarget2D, PostProcessEffect, Zero-Allocation Uniform Buffers).
/// </summary>
public sealed class RetroTankArenaGame : Game
{
    private static readonly ILogger Logger = LogManager.GetLogger(KhefestSubsystem.Core, "TankArena");

    // High-level rendering
    private SpriteBatch? _spriteBatch;
    private Camera2D? _camera;
    private Texture2D? _particleTex;

    // High-level GPU post-processing pipeline
    private RenderTarget2D? _offscreenTarget;
    private PostProcessEffect? _postFx;
    private PostFxUniforms _postFxData;

    // Arena geometry & entities
    private const float ArenaWidth = 2000f;
    private const float ArenaHeight = 1600f;

    private readonly List<Obstacle> _obstacles = new();
    private readonly List<Projectile> _projectiles = new();
    private readonly List<Landmine> _landmines = new();
    private readonly List<Pickup> _pickups = new();
    private readonly List<TreadMark> _treads = new();
    private readonly List<ArenaParticle> _particles = new();
    private readonly List<EnemyTank> _enemies = new();

    // Player state
    private Vector2 _playerPos = new(ArenaWidth * 0.5f, ArenaHeight * 0.5f);
    private Vector2 _playerVel = Vector2.Zero;
    private float _playerHullAngle;
    private float _playerTurretAngle;
    private float _playerHealth = 100f;
    private float _playerMaxHealth = 100f;
    private int _playerAmmo = 40;
    private int _playerMines = 3;
    private float _fireCooldown;
    private float _shieldTime;
    private int _score;
    private int _wave = 1;
    private bool _gameOver;

    // Visual feedback
    private float _screenShake;
    private float _globalTime;
    private float _treadTimer;
    private readonly FrameStatistics _frameStats = new();

    private const string PostFxShaderHlsl = @"
cbuffer PostFxConstants : register(b0)
{
    float u_Time;
    float u_Vignette;
    float u_Aberration;
    float u_HitFlash;
};

struct PS_INPUT
{
    float4 Position : SV_POSITION;
    float2 TexCoord : TEXCOORD0;
};

Texture2D SceneTex : register(t0);
SamplerState LinearSampler : register(s0);

float4 PSMain(PS_INPUT input) : SV_TARGET
{
    float2 uv = input.TexCoord;
    float2 centerOffset = uv - 0.5f;
    float dist = length(centerOffset);

    // Chromatic aberration
    float2 caOffset = centerOffset * (u_Aberration * 0.02f * dist);
    float r = SceneTex.Sample(LinearSampler, uv + caOffset).r;
    float g = SceneTex.Sample(LinearSampler, uv).g;
    float b = SceneTex.Sample(LinearSampler, uv - caOffset).b;
    float3 color = float3(r, g, b);

    // Retro scanline effect
    float scanline = sin(uv.y * 600.0f + u_Time * 5.0f) * 0.035f;
    color -= scanline;

    // Radial vignette
    float vig = 1.0f - smoothstep(0.35f, 0.85f, dist) * u_Vignette;
    color *= vig;

    // Red damage flash
    color = lerp(color, float3(1.0f, 0.15f, 0.15f), u_HitFlash * 0.7f);

    return float4(color, 1.0f);
}
";

    public override void Initialize()
    {
        Logger.Info("Initializing Retro Tank Arena...");

        _camera = new Camera2D(Window.Width, Window.Height);
        _spriteBatch = new SpriteBatch(GpuDevice, Resources);

        // 1. Create a circular particle texture
        var particleBytes = new byte[16 * 16 * 4];
        for (int y = 0; y < 16; y++)
        {
            for (int x = 0; x < 16; x++)
            {
                float dx = (x - 7.5f) / 7.5f;
                float dy = (y - 7.5f) / 7.5f;
                float dist = MathF.Sqrt(dx * dx + dy * dy);
                float a = Math.Clamp(1.0f - dist, 0f, 1f);
                int idx = (y * 16 + x) * 4;
                particleBytes[idx + 0] = 255;
                particleBytes[idx + 1] = 255;
                particleBytes[idx + 2] = 255;
                particleBytes[idx + 3] = (byte)(a * 255f);
            }
        }
        _particleTex = Texture2D.FromRgba(GpuDevice, "TankParticle", 16, 16, particleBytes, Resources);

        // 2. Setup Offscreen Render Target and Custom Post-FX Pipeline
        InitLowLevelPostFx();

        // 3. Setup arena map and initial wave
        BuildArenaLayout();
        SpawnWave(_wave);

        Logger.Info("Retro Tank Arena initialized successfully.");
    }

    private void InitLowLevelPostFx()
    {
        _offscreenTarget = new RenderTarget2D(
            GpuDevice,
            "ArenaOffscreenRT",
            Window.Width,
            Window.Height,
            hasDepth: false,
            format: GpuFormat.R8G8B8A8_UNorm,
            manager: Resources);

        _postFx = new PostProcessEffect(
            GpuDevice,
            "TankArenaPostFx",
            PostFxShaderHlsl,
            entryPoint: "PSMain",
            blendMode: BlendMode.Opaque,
            manager: Resources);
    }

    public override void OnResize(int width, int height)
    {
        _camera?.Resize(width, height);
        _offscreenTarget?.Resize(width, height);
    }

    private void BuildArenaLayout()
    {
        _obstacles.Clear();

        // Outer indestructible boundary
        float borderThickness = 40f;
        _obstacles.Add(new Obstacle { Position = new Vector2(ArenaWidth * 0.5f, borderThickness * 0.5f), Size = new Vector2(ArenaWidth, borderThickness), Type = WallType.Concrete });
        _obstacles.Add(new Obstacle { Position = new Vector2(ArenaWidth * 0.5f, ArenaHeight - borderThickness * 0.5f), Size = new Vector2(ArenaWidth, borderThickness), Type = WallType.Concrete });
        _obstacles.Add(new Obstacle { Position = new Vector2(borderThickness * 0.5f, ArenaHeight * 0.5f), Size = new Vector2(borderThickness, ArenaHeight), Type = WallType.Concrete });
        _obstacles.Add(new Obstacle { Position = new Vector2(ArenaWidth - borderThickness * 0.5f, ArenaHeight * 0.5f), Size = new Vector2(borderThickness, ArenaHeight), Type = WallType.Concrete });

        // Interior concrete pillars
        Vector2[] pillars =
        [
            new(500, 400), new(1500, 400),
            new(500, 1200), new(1500, 1200),
            new(1000, 800)
        ];
        foreach (var p in pillars)
        {
            _obstacles.Add(new Obstacle { Position = p, Size = new Vector2(80, 80), Type = WallType.Concrete });
        }

        // Destructible brick walls
        var rnd = new Random(1337);
        for (int i = 0; i < 28; i++)
        {
            float x = 250f + rnd.Next(0, 15) * 100f;
            float y = 200f + rnd.Next(0, 12) * 100f;

            // Keep center clear for player start
            if (Vector2.Distance(new Vector2(x, y), _playerPos) < 250f) continue;

            _obstacles.Add(new Obstacle
            {
                Position = new Vector2(x, y),
                Size = new Vector2(60, 60),
                Type = WallType.Brick,
                Health = 70,
                MaxHealth = 70
            });
        }
    }

    private void SpawnWave(int waveNumber)
    {
        _enemies.Clear();
        int count = 4 + waveNumber * 2;
        var rnd = Random.Shared;

        for (int i = 0; i < count; i++)
        {
            var type = (i % 5 == 0) ? EnemyType.Heavy : (i % 2 == 0 ? EnemyType.BattleTank : EnemyType.Scout);
            float hp = type switch { EnemyType.Heavy => 120f, EnemyType.BattleTank => 60f, _ => 35f };
            float spd = type switch { EnemyType.Heavy => 90f, EnemyType.BattleTank => 130f, _ => 180f };
            float interval = type switch { EnemyType.Heavy => 2.2f, EnemyType.BattleTank => 1.4f, _ => 0.9f };

            Vector2 pos;
            do
            {
                pos = new Vector2(150f + rnd.NextSingle() * (ArenaWidth - 300f), 150f + rnd.NextSingle() * (ArenaHeight - 300f));
            } while (Vector2.Distance(pos, _playerPos) < 400f);

            _enemies.Add(new EnemyTank
            {
                Type = type,
                Position = pos,
                HullAngle = rnd.NextSingle() * MathF.Tau,
                TurretAngle = 0f,
                Health = hp,
                MaxHealth = hp,
                Speed = spd,
                FireInterval = interval,
                FireCooldown = 1.0f + rnd.NextSingle() * 1.5f,
                PatrolTarget = pos
            });
        }

        // Spawn pickups
        _pickups.Clear();
        _pickups.Add(new Pickup { Position = new Vector2(300, 300), Type = PickupType.Health });
        _pickups.Add(new Pickup { Position = new Vector2(1700, 1300), Type = PickupType.Ammo });
        _pickups.Add(new Pickup { Position = new Vector2(1000, 400), Type = PickupType.Mine });
        _pickups.Add(new Pickup { Position = new Vector2(1000, 1200), Type = PickupType.Shield });
    }

    public override void Update(GameTime time)
    {
        float dt = time.DeltaTime;
        _globalTime += dt;
        _frameStats.Update(time.DeltaTime);

        // Screen shake decay
        _screenShake = Math.Max(0f, _screenShake - dt * 20f);
        _postFxData.HitFlash = Math.Max(0f, _postFxData.HitFlash - dt * 3.5f);
        _postFxData.Aberration = Math.Max(0f, _postFxData.Aberration - dt * 4.0f);
        _postFxData.Vignette = 0.85f + MathF.Sin(_globalTime * 2f) * 0.05f;
        _postFxData.Time = _globalTime;

        if (_shieldTime > 0f) _shieldTime -= dt;

        if (Input.IsKeyPressed(Key.R))
        {
            _playerPos = new Vector2(ArenaWidth * 0.5f, ArenaHeight * 0.5f);
            _playerVel = Vector2.Zero;
            _playerHealth = 100f;
            _playerAmmo = 40;
            _playerMines = 3;
            _score = 0;
            _wave = 1;
            _gameOver = false;
            BuildArenaLayout();
            SpawnWave(_wave);
        }

        if (!_gameOver)
        {
            UpdatePlayer(dt);
        }

        UpdateProjectiles(dt);
        UpdateLandmines(dt);
        UpdateEnemies(dt);
        UpdatePickups(dt);
        UpdateParticlesAndTreads(dt);

        // Check wave completion
        if (_enemies.Count == 0 && !_gameOver)
        {
            _wave++;
            _score += 1000;
            _playerAmmo = Math.Min(60, _playerAmmo + 25);
            _playerMines = Math.Min(5, _playerMines + 2);
            _playerHealth = Math.Min(_playerMaxHealth, _playerHealth + 30f);
            SpawnWave(_wave);
            TriggerExplosion(_playerPos, 50, Color4.Cyan, 250f);
        }

        // Camera smoothly follows player with screen shake
        if (_camera != null)
        {
            var targetCam = _playerPos;
            if (_screenShake > 0.01f)
            {
                var shakeOffset = new Vector2(
                    (Random.Shared.NextSingle() * 2f - 1f) * _screenShake,
                    (Random.Shared.NextSingle() * 2f - 1f) * _screenShake);
                targetCam += shakeOffset;
            }
            _camera.Position = Vector2.Lerp(_camera.Position, targetCam, dt * 7.5f);
        }
    }

    private void UpdatePlayer(float dt)
    {
        // 1. Tank Hull Steering & Driving
        float turnSpeed = 3.2f;
        float driveSpeed = 220f;

        if (Input.IsKeyDown(Key.A) || Input.IsKeyDown(Key.Left)) _playerHullAngle -= turnSpeed * dt;
        if (Input.IsKeyDown(Key.D) || Input.IsKeyDown(Key.Right)) _playerHullAngle += turnSpeed * dt;

        var forward = new Vector2(MathF.Cos(_playerHullAngle), MathF.Sin(_playerHullAngle));
        Vector2 moveDir = Vector2.Zero;

        if (Input.IsKeyDown(Key.W) || Input.IsKeyDown(Key.Up)) moveDir += forward;
        if (Input.IsKeyDown(Key.S) || Input.IsKeyDown(Key.Down)) moveDir -= forward * 0.65f;

        _playerVel = Vector2.Lerp(_playerVel, moveDir * driveSpeed, dt * 8f);
        var newPos = _playerPos + _playerVel * dt;

        // Collision against arena obstacles
        if (!CheckObstacleCollision(newPos, 22f))
        {
            _playerPos = newPos;
        }
        else
        {
            _playerVel = Vector2.Zero;
        }

        // 2. Tread marks deposition
        if (_playerVel.LengthSquared() > 200f)
        {
            _treadTimer += dt;
            if (_treadTimer >= 0.08f)
            {
                _treadTimer = 0f;
                _treads.Add(new TreadMark { Position = _playerPos, Rotation = _playerHullAngle, Alpha = 0.6f });
                if (_treads.Count > 400) _treads.RemoveAt(0);
            }
        }

        // 3. Mouse Aiming Turret
        if (_camera != null)
        {
            var mouseWorld = _camera.ScreenToWorld(Input.MousePosition);
            _playerTurretAngle = MathF.Atan2(mouseWorld.Y - _playerPos.Y, mouseWorld.X - _playerPos.X);
        }

        // 4. Firing Cannon Shells
        _fireCooldown = Math.Max(0f, _fireCooldown - dt);
        if ((Input.IsMouseButtonDown(MouseButton.Left) || Input.IsKeyDown(Key.Space)) && _fireCooldown <= 0f && _playerAmmo > 0)
        {
            _fireCooldown = 0.22f;
            _playerAmmo--;

            var turretForward = new Vector2(MathF.Cos(_playerTurretAngle), MathF.Sin(_playerTurretAngle));
            var shellPos = _playerPos + turretForward * 32f;

            _projectiles.Add(new Projectile
            {
                Position = shellPos,
                Velocity = turretForward * 750f,
                IsPlayer = true,
                Damage = 35f,
                BouncesLeft = 1
            });

            // Recoil & Juice
            _playerVel -= turretForward * 60f;
            _screenShake = 6f;
            TriggerExplosion(shellPos, 8, Color4.Orange, 120f);
        }

        // 5. Deploy Landmine
        if (Input.IsMouseButtonPressed(MouseButton.Right) && _playerMines > 0)
        {
            _playerMines--;
            _landmines.Add(new Landmine { Position = _playerPos, Timer = 0.6f, Armed = false, IsPlayer = true });
            TriggerExplosion(_playerPos, 5, Color4.Yellow, 60f);
        }
    }

    private void UpdateProjectiles(float dt)
    {
        for (int i = _projectiles.Count - 1; i >= 0; i--)
        {
            var p = _projectiles[i];
            p.Life += dt;
            if (p.Life >= p.MaxLife)
            {
                _projectiles.RemoveAt(i);
                continue;
            }

            var nextPos = p.Position + p.Velocity * dt;

            // Check obstacle collisions
            bool hitObstacle = false;
            foreach (var obs in _obstacles)
            {
                if (obs.Bounds.Contains(nextPos))
                {
                    hitObstacle = true;
                    if (obs.Type == WallType.Brick)
                    {
                        obs.Health -= (int)p.Damage;
                        TriggerExplosion(nextPos, 12, Color4.Orange, 160f);
                        if (obs.Health <= 0)
                        {
                            _obstacles.Remove(obs);
                            TriggerExplosion(obs.Position, 30, new Color4(0.8f, 0.4f, 0.2f, 1f), 240f);
                            _screenShake = 8f;
                        }
                        _projectiles.RemoveAt(i);
                        break;
                    }
                    else // Concrete wall bounce or stop
                    {
                        if (p.BouncesLeft > 0)
                        {
                            p.BouncesLeft--;
                            // Determine bounce normal
                            var b = obs.Bounds;
                            if (p.Position.X < b.Left || p.Position.X > b.Right) p.Velocity.X = -p.Velocity.X;
                            else p.Velocity.Y = -p.Velocity.Y;

                            TriggerExplosion(nextPos, 6, Color4.LightGray, 80f);
                        }
                        else
                        {
                            TriggerExplosion(nextPos, 10, Color4.Yellow, 180f);
                            _projectiles.RemoveAt(i);
                        }
                        break;
                    }
                }
            }
            if (hitObstacle) continue;

            // Check entity collisions
            if (p.IsPlayer)
            {
                for (int e = _enemies.Count - 1; e >= 0; e--)
                {
                    var enemy = _enemies[e];
                    if (Vector2.Distance(nextPos, enemy.Position) < 26f)
                    {
                        enemy.Health -= p.Damage;
                        _projectiles.RemoveAt(i);
                        TriggerExplosion(nextPos, 15, Color4.Yellow, 200f);

                        if (enemy.Health <= 0f)
                        {
                            _enemies.RemoveAt(e);
                            _score += enemy.Type switch { EnemyType.Heavy => 300, EnemyType.BattleTank => 150, _ => 75 };
                            TriggerExplosion(enemy.Position, 45, Color4.Orange, 350f);
                            _screenShake = 12f;
                            _postFxData.Aberration = 1.0f;
                        }
                        hitObstacle = true;
                        break;
                    }
                }
                if (hitObstacle) continue;
            }
            else // Enemy projectile against player
            {
                if (Vector2.Distance(nextPos, _playerPos) < 22f && !_gameOver)
                {
                    _projectiles.RemoveAt(i);
                    TriggerExplosion(nextPos, 20, Color4.Red, 250f);

                    if (_shieldTime <= 0f)
                    {
                        _playerHealth -= p.Damage;
                        _screenShake = 14f;
                        _postFxData.HitFlash = 1.0f;
                        _postFxData.Aberration = 0.8f;

                        if (_playerHealth <= 0f)
                        {
                            _playerHealth = 0f;
                            _gameOver = true;
                            TriggerExplosion(_playerPos, 80, Color4.Red, 400f);
                        }
                    }
                    continue;
                }
            }

            p.Position = nextPos;
        }
    }

    private void UpdateLandmines(float dt)
    {
        for (int i = _landmines.Count - 1; i >= 0; i--)
        {
            var m = _landmines[i];
            if (!m.Armed)
            {
                m.Timer -= dt;
                if (m.Timer <= 0f) m.Armed = true;
                continue;
            }

            bool exploded = false;
            // Check enemies near mine
            if (m.IsPlayer)
            {
                foreach (var enemy in _enemies)
                {
                    if (Vector2.Distance(m.Position, enemy.Position) < 45f)
                    {
                        exploded = true;
                        break;
                    }
                }
            }

            if (exploded)
            {
                _landmines.RemoveAt(i);
                TriggerExplosion(m.Position, 60, Color4.Orange, 400f);
                _screenShake = 16f;
                _postFxData.Aberration = 1.2f;

                // Damage all nearby entities
                for (int e = _enemies.Count - 1; e >= 0; e--)
                {
                    var en = _enemies[e];
                    float d = Vector2.Distance(m.Position, en.Position);
                    if (d < 120f)
                    {
                        en.Health -= 90f * (1f - d / 120f);
                        if (en.Health <= 0f)
                        {
                            _enemies.RemoveAt(e);
                            _score += 200;
                        }
                    }
                }
            }
        }
    }

    private void UpdateEnemies(float dt)
    {
        foreach (var enemy in _enemies)
        {
            enemy.RetargetTimer -= dt;
            if (enemy.RetargetTimer <= 0f)
            {
                enemy.RetargetTimer = 2.0f + Random.Shared.NextSingle() * 1.5f;
                enemy.PatrolTarget = _playerPos + new Vector2(
                    (Random.Shared.NextSingle() * 2f - 1f) * 200f,
                    (Random.Shared.NextSingle() * 2f - 1f) * 200f);
            }

            // Move toward target
            var toTarget = enemy.PatrolTarget - enemy.Position;
            if (toTarget.LengthSquared() > 100f)
            {
                float targetAngle = MathF.Atan2(toTarget.Y, toTarget.X);
                enemy.HullAngle = MathHelper.LerpAngle(enemy.HullAngle, targetAngle, dt * 4f);
                var fwd = new Vector2(MathF.Cos(enemy.HullAngle), MathF.Sin(enemy.HullAngle));
                var nextPos = enemy.Position + fwd * enemy.Speed * dt;
                if (!CheckObstacleCollision(nextPos, 24f))
                {
                    enemy.Position = nextPos;
                }
            }

            // Aim turret at player
            var toPlayer = _playerPos - enemy.Position;
            float aimAngle = MathF.Atan2(toPlayer.Y, toPlayer.X);
            enemy.TurretAngle = MathHelper.LerpAngle(enemy.TurretAngle, aimAngle, dt * 5f);

            // Fire at player if in range and line of sight roughly aligned
            enemy.FireCooldown -= dt;
            if (enemy.FireCooldown <= 0f && toPlayer.Length() < 700f && !_gameOver)
            {
                enemy.FireCooldown = enemy.FireInterval;
                var turretDir = new Vector2(MathF.Cos(enemy.TurretAngle), MathF.Sin(enemy.TurretAngle));
                _projectiles.Add(new Projectile
                {
                    Position = enemy.Position + turretDir * 28f,
                    Velocity = turretDir * 550f,
                    IsPlayer = false,
                    Damage = enemy.Type switch { EnemyType.Heavy => 30f, EnemyType.BattleTank => 18f, _ => 10f },
                    BouncesLeft = 0
                });
                TriggerExplosion(enemy.Position + turretDir * 28f, 6, Color4.Red, 90f);
            }
        }
    }

    private void UpdatePickups(float dt)
    {
        for (int i = _pickups.Count - 1; i >= 0; i--)
        {
            var p = _pickups[i];
            p.PulseTimer += dt * 5f;
            if (Vector2.Distance(_playerPos, p.Position) < 32f)
            {
                switch (p.Type)
                {
                    case PickupType.Health:
                        _playerHealth = Math.Min(_playerMaxHealth, _playerHealth + 40f);
                        break;
                    case PickupType.Ammo:
                        _playerAmmo = Math.Min(60, _playerAmmo + 20);
                        break;
                    case PickupType.Mine:
                        _playerMines += 2;
                        break;
                    case PickupType.Shield:
                        _shieldTime = 8.0f;
                        break;
                }

                _score += 50;
                TriggerExplosion(p.Position, 20, Color4.Green, 180f);
                _pickups.RemoveAt(i);
            }
        }
    }

    private void UpdateParticlesAndTreads(float dt)
    {
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

        for (int i = _treads.Count - 1; i >= 0; i--)
        {
            var t = _treads[i];
            t.Alpha -= dt * 0.035f;
            if (t.Alpha <= 0f)
            {
                _treads.RemoveAt(i);
            }
        }
    }

    private bool CheckObstacleCollision(Vector2 pos, float radius)
    {
        foreach (var obs in _obstacles)
        {
            var b = obs.Bounds;
            float nearestX = Math.Clamp(pos.X, b.Left, b.Right);
            float nearestY = Math.Clamp(pos.Y, b.Top, b.Bottom);
            float dx = pos.X - nearestX;
            float dy = pos.Y - nearestY;
            if ((dx * dx + dy * dy) < (radius * radius))
            {
                return true;
            }
        }
        return false;
    }

    private void TriggerExplosion(Vector2 pos, int count, Color4 color, float speed)
    {
        for (int i = 0; i < count; i++)
        {
            if (_particles.Count > 1500) break;
            float a = Random.Shared.NextSingle() * MathF.Tau;
            float s = Random.Shared.NextSingle() * speed;
            _particles.Add(new ArenaParticle
            {
                Position = pos,
                Velocity = new Vector2(MathF.Cos(a) * s, MathF.Sin(a) * s),
                Color = color,
                Size = 3f + Random.Shared.NextSingle() * 10f,
                Life = 0f,
                MaxLife = 0.3f + Random.Shared.NextSingle() * 0.6f
            });
        }
    }

    public override void Render(GameTime time)
    {
        if (_spriteBatch == null || _camera == null || _offscreenTarget == null || _postFx == null)
        {
            return;
        }

        var backBuffer = SwapChain.CurrentBackBuffer;

        // =========================================================================
        // PASS 1: Render Arena Scene to Offscreen Render Target (High-Level SpriteBatch)
        // =========================================================================
        _spriteBatch.Begin(_offscreenTarget, _camera, SpriteSortMode.Deferred);

        // Ground arena grid lines
        for (float x = 0; x <= ArenaWidth; x += 100f)
        {
            _spriteBatch.DrawLine(new Vector2(x, 0), new Vector2(x, ArenaHeight), new Color4(0.08f, 0.1f, 0.14f, 1f), 1f);
        }
        for (float y = 0; y <= ArenaHeight; y += 100f)
        {
            _spriteBatch.DrawLine(new Vector2(0, y), new Vector2(ArenaWidth, y), new Color4(0.08f, 0.1f, 0.14f, 1f), 1f);
        }

        // Tread marks on floor
        foreach (var t in _treads)
        {
            _spriteBatch.DrawRectangle(t.Position - new Vector2(10, 10), new Vector2(20, 20), new Color4(0.04f, 0.05f, 0.07f, t.Alpha), 2f);
        }

        // Obstacles (Concrete vs Brick)
        foreach (var obs in _obstacles)
        {
            var b = obs.Bounds;
            var color = obs.Type == WallType.Concrete
                ? new Color4(0.35f, 0.4f, 0.45f, 1f)
                : new Color4(0.65f, 0.3f, 0.2f, 1f);

            _spriteBatch.FillRectangle(new Vector2(b.Left, b.Top), new Vector2(b.Width, b.Height), color);
            _spriteBatch.DrawRectangle(new Vector2(b.Left, b.Top), new Vector2(b.Width, b.Height), Color4.Black, 2f);

            // Brick damage cracking indicator
            if (obs.Type == WallType.Brick && obs.Health < obs.MaxHealth)
            {
                _spriteBatch.DrawLine(new Vector2(b.Left + 5, b.Top + 5), new Vector2(b.Right - 5, b.Bottom - 5), Color4.Black, 1.5f);
            }
        }

        // Pickups
        foreach (var p in _pickups)
        {
            float scale = 1.0f + MathF.Sin(p.PulseTimer) * 0.15f;
            var pColor = p.Type switch
            {
                PickupType.Health => Color4.Green,
                PickupType.Ammo => Color4.Yellow,
                PickupType.Mine => Color4.Red,
                _ => Color4.Cyan
            };
            _spriteBatch.DrawCircle(p.Position, 16f * scale, pColor, 12, 3f);
        }

        // Landmines
        foreach (var m in _landmines)
        {
            var mColor = m.Armed ? Color4.Red : Color4.Yellow;
            _spriteBatch.DrawCircle(m.Position, 8f, mColor, 8, 2f);
        }

        // Enemy Tanks
        foreach (var enemy in _enemies)
        {
            DrawTank(enemy.Position, enemy.HullAngle, enemy.TurretAngle,
                enemy.Type == EnemyType.Heavy ? Color4.Purple : (enemy.Type == EnemyType.BattleTank ? Color4.Red : Color4.Yellow),
                enemy.Health / enemy.MaxHealth);
        }

        // Player Tank
        if (!_gameOver)
        {
            DrawTank(_playerPos, _playerHullAngle, _playerTurretAngle, Color4.Cyan, _playerHealth / _playerMaxHealth);

            // Shield bubble
            if (_shieldTime > 0f)
            {
                _spriteBatch.DrawCircle(_playerPos, 34f, Color4.Cyan, 20, 2.5f);
            }
        }

        // Projectiles
        foreach (var p in _projectiles)
        {
            var pColor = p.IsPlayer ? Color4.Yellow : Color4.Red;
            _spriteBatch.DrawCircle(p.Position, 4f, pColor, 8, 2f);
        }

        // Particles
        if (_particleTex != null)
        {
            foreach (var part in _particles)
            {
                float lifeFactor = 1f - (part.Life / part.MaxLife);
                var c = new Color4(part.Color.R, part.Color.G, part.Color.B, lifeFactor);
                _spriteBatch.Draw(_particleTex, part.Position, new Vector2(part.Size), c, 0f, new Vector2(part.Size * 0.5f), SpriteEffects.None);
            }
        }

        _spriteBatch.End();

        // =========================================================================
        // PASS 2: Custom HLSL Post-FX Pipeline via Procedural Fullscreen Triangle
        // =========================================================================
        _postFx.SetUniforms(in _postFxData);
        _postFx.Apply(_offscreenTarget, backBuffer);

        // =========================================================================
        // PASS 3: High-Level Screen Space UI / HUD (Directly on BackBuffer)
        // =========================================================================
        _spriteBatch.Begin(backBuffer, camera: null, SpriteSortMode.Deferred);

        // HUD Top Panel
        _spriteBatch.FillRectangle(new Vector2(16, 16), new Vector2(460, 110), new Color4(0.05f, 0.05f, 0.08f, 0.85f));
        _spriteBatch.DrawRectangle(new Vector2(16, 16), new Vector2(460, 110), Color4.Cyan, 1.5f);

        _spriteBatch.DrawString($"RETRO TANK ARENA  -  WAVE {_wave}", new Vector2(28, 22), Color4.Yellow, 1.2f);
        _spriteBatch.DrawString($"Score: {_score:N0}   Enemies Left: {_enemies.Count}", new Vector2(28, 44), Color4.White, 1.0f);
        _spriteBatch.DrawString($"Ammo: {_playerAmmo}   Mines: {_playerMines}   [LMB/Space] Fire   [RMB] Mine", new Vector2(28, 64), Color4.LightGray, 0.95f);
        _spriteBatch.DrawString($"FPS: {_frameStats.FramesPerSecond:F0}   PostFX: RenderTarget2D + Procedural PostProcessEffect", new Vector2(28, 84), Color4.Green, 0.9f);

        // Health bar
        _spriteBatch.FillRectangle(new Vector2(28, 106), new Vector2(220f * (_playerHealth / _playerMaxHealth), 12f), _playerHealth > 30 ? Color4.Green : Color4.Red);
        _spriteBatch.DrawRectangle(new Vector2(28, 106), new Vector2(220f, 12f), Color4.White, 1.2f);

        // Minimap Radar in top right
        float mapSize = 140f;
        var mapPos = new Vector2(Window.Width - mapSize - 20f, 20f);
        _spriteBatch.FillRectangle(mapPos, new Vector2(mapSize, mapSize), new Color4(0.02f, 0.04f, 0.06f, 0.85f));
        _spriteBatch.DrawRectangle(mapPos, new Vector2(mapSize, mapSize), Color4.CornflowerBlue, 1.5f);

        // Player dot on radar
        var pDot = mapPos + new Vector2((_playerPos.X / ArenaWidth) * mapSize, (_playerPos.Y / ArenaHeight) * mapSize);
        _spriteBatch.DrawCircle(pDot, 3f, Color4.Cyan, 6, 2f);

        // Enemy dots on radar
        foreach (var en in _enemies)
        {
            var eDot = mapPos + new Vector2((en.Position.X / ArenaWidth) * mapSize, (en.Position.Y / ArenaHeight) * mapSize);
            _spriteBatch.DrawCircle(eDot, 2f, Color4.Red, 4, 1.5f);
        }

        // Game Over Overlay
        if (_gameOver)
        {
            var overlayPos = new Vector2(Window.Width * 0.5f - 200f, Window.Height * 0.5f - 60f);
            _spriteBatch.FillRectangle(overlayPos, new Vector2(400, 120), new Color4(0f, 0f, 0f, 0.85f));
            _spriteBatch.DrawRectangle(overlayPos, new Vector2(400, 120), Color4.Red, 2f);
            _spriteBatch.DrawString("GAME OVER", overlayPos + new Vector2(130, 25), Color4.Red, 1.5f);
            _spriteBatch.DrawString("Press [R] to Restart", overlayPos + new Vector2(120, 65), Color4.White, 1.1f);
        }

        _spriteBatch.End();
    }

    private void DrawTank(Vector2 pos, float hullAngle, float turretAngle, Color4 color, float healthPercent)
    {
        if (_spriteBatch == null) return;

        // Tank hull
        _spriteBatch.DrawRectangle(pos - new Vector2(18, 14), new Vector2(36, 28), color, 3f);
        _spriteBatch.FillRectangle(pos - new Vector2(16, 12), new Vector2(32, 24), new Color4(color.R * 0.35f, color.G * 0.35f, color.B * 0.35f, 1f));

        // Tank treads left & right
        _spriteBatch.FillRectangle(pos - new Vector2(20, 18), new Vector2(40, 5), Color4.DarkGray);
        _spriteBatch.FillRectangle(pos - new Vector2(20, -13), new Vector2(40, 5), Color4.DarkGray);

        // Turret cannon
        var turretFwd = new Vector2(MathF.Cos(turretAngle), MathF.Sin(turretAngle));
        _spriteBatch.DrawLine(pos, pos + turretFwd * 26f, color, 4f);

        // Turret cupola
        _spriteBatch.FillCircle(pos, 8f, color, 12);
        _spriteBatch.DrawCircle(pos, 8f, Color4.Black, 12, 1.5f);

        // Mini health pip above tank
        if (healthPercent < 0.99f && healthPercent > 0f)
        {
            var barPos = pos - new Vector2(16, 28);
            _spriteBatch.FillRectangle(barPos, new Vector2(32f * healthPercent, 3f), healthPercent > 0.3f ? Color4.Green : Color4.Red);
            _spriteBatch.DrawRectangle(barPos, new Vector2(32f, 3f), Color4.Black, 1f);
        }
    }

    public override void Shutdown()
    {
        _postFx?.Dispose();
        _offscreenTarget?.Dispose();
        _particleTex?.Dispose();
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
                Title = "Khefest Retro Tank Arena [Milestone 1.3.0]",
                Width = 1280,
                Height = 720,
                Resizable = true,
                VSync = true,
                TargetFps = 120
            })
            .Build();

        var game = new RetroTankArenaGame();
        KhefestApp.Run(game, config);
    }
}
