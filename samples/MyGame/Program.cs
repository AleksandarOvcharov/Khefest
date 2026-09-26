using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Khefest.Core.Configuration;
using Khefest.Graphics.Camera;
using Khefest.Graphics.LowLevel;
using Khefest.Graphics.Mathematics;
using Khefest.Graphics.PostProcessing;
using Khefest.Graphics.Texturing;
using Khefest.Graphics.TwoD;
using Khefest.Input;
using Khefest.Windows.Application;
using Khefest.Windows.Timing;

namespace MyGame;

/// <summary>
/// Custom vertex struct for low-level GPU rendering test.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct CustomVertex(Vector3 position, Color4 color)
{
    public Vector3 Position = position;
    public Color4 Color = color;
}

/// <summary>
/// Uniform constant buffer struct for post-process effect.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct PostUniforms
{
    public float Time;
    public float VignetteIntensity;
    public float ScanlineAmount;
    public float Padding;
}

/// <summary>
/// Standalone Khefest Game demonstrating:
/// 1. Windowing and event lifecycle.
/// 2. 2D SpriteBatch rendering with Camera2D.
/// 3. Canonical Vector2 input polling and tuple deconstruction.
/// 4. Dynamic resize-aware RenderTarget2D.
/// 5. Custom HLSL post-processing via PostProcessEffect.
/// 6. Custom low-level GPU pipeline, vertex buffer, and scoped render pass.
/// 7. Spatial math with Rect2D and MathHelper.LerpAngle.
/// 8. Deterministic zero-leak resource disposal on shutdown.
/// </summary>
public sealed class StandaloneGame : Game
{
    // High-Level Subsystems
    private SpriteBatch? _spriteBatch;
    private Camera2D? _camera;
    private RenderTarget2D? _sceneTarget;
    private PostProcessEffect? _postEffect;

    // Low-Level GPU Subsystems
    private IGpuShader? _customVS;
    private IGpuShader? _customPS;
    private IPipeline? _customPipeline;
    private IGpuBuffer? _customVB;

    // Game Simulation State
    private Vector2 _playerPos = new(640, 360);
    private float _playerAngle = 0f;
    private float _totalTime = 0f;
    private int _score = 0;
    private readonly Rect2D _arenaBounds = new(100f, 100f, 1080f, 520f);

    // Low-Level HLSL Vertex & Pixel Shaders
    private const string LowLevelHlsl = @"
struct VSInput
{
    float3 Position : POSITION;
    float4 Color : COLOR0;
};

struct PSInput
{
    float4 Position : SV_POSITION;
    float4 Color : COLOR0;
};

PSInput VSMain(VSInput input)
{
    PSInput output;
    output.Position = float4(input.Position, 1.0f);
    output.Color = input.Color;
    return output;
}

float4 PSMain(PSInput input) : SV_TARGET
{
    return input.Color;
}
";

    // Post-Process HLSL Shader
    private const string PostHlsl = @"
Texture2D g_Texture : register(t0);
SamplerState g_Sampler : register(s0);

cbuffer Uniforms : register(b0)
{
    float Time;
    float VignetteIntensity;
    float ScanlineAmount;
    float Padding;
};

struct PSInput
{
    float4 Position : SV_POSITION;
    float2 TexCoord : TEXCOORD0;
};

float4 PSMain(PSInput input) : SV_TARGET
{
    float2 uv = input.TexCoord;
    float4 color = g_Texture.Sample(g_Sampler, uv);

    // Vignette
    float dist = distance(uv, float2(0.5f, 0.5f));
    color.rgb *= (1.0f - dist * VignetteIntensity);

    // Subtle scanlines
    float scanline = sin(uv.y * 600.0f + Time * 8.0f) * ScanlineAmount;
    color.rgb -= scanline;

    return color;
}
";

    public override void Initialize()
    {
        // 1. Initialize High-Level 2D Renderer and Camera
        _spriteBatch = new SpriteBatch(GpuDevice, Resources);
        _camera = new Camera2D(Window.Width, Window.Height);

        // 2. Create Dynamic Offscreen RenderTarget2D
        _sceneTarget = new RenderTarget2D(
            GpuDevice,
            "SceneRT",
            Window.Width,
            Window.Height,
            hasDepth: false,
            manager: Resources);

        // 3. Create Custom HLSL Post-Processing Effect
        _postEffect = new PostProcessEffect(
            GpuDevice,
            "VignettePostFx",
            PostHlsl,
            entryPoint: "PSMain",
            manager: Resources);

        // 4. Create Low-Level GPU Pipeline and Geometry (Demonstrating low-level dropdown)
        _customVS = GpuDevice.CompileShader("BgVS", ShaderStage.Vertex, LowLevelHlsl, "VSMain");
        _customPS = GpuDevice.CompileShader("BgPS", ShaderStage.Pixel, LowLevelHlsl, "PSMain");

        var vertexLayout = new VertexLayout([
            new VertexElement("POSITION", GpuFormat.R32G32B32_Float, 0),
            new VertexElement("COLOR", GpuFormat.R32G32B32A32_Float, 12)
        ]);

        var pipelineDesc = new PipelineDesc
        {
            VertexShader = _customVS,
            PixelShader = _customPS,
            VertexLayout = vertexLayout,
            Topology = PrimitiveTopology.TriangleList,
            CullMode = CullMode.None,
            FillMode = FillMode.Solid,
            BlendMode = BlendMode.Opaque,
            DepthTestEnabled = false,
            DepthWriteEnabled = false
        };
        _customPipeline = GpuDevice.CreatePipeline("CustomBgPipeline", pipelineDesc);

        // Low-level backdrop quad geometry (-1 to 1 NDC)
        CustomVertex[] vertices = [
            new(new Vector3(-1f,  1f, 0f), new Color4(0.06f, 0.08f, 0.12f, 1f)),
            new(new Vector3( 1f,  1f, 0f), new Color4(0.08f, 0.10f, 0.16f, 1f)),
            new(new Vector3(-1f, -1f, 0f), new Color4(0.03f, 0.04f, 0.07f, 1f)),

            new(new Vector3(-1f, -1f, 0f), new Color4(0.03f, 0.04f, 0.07f, 1f)),
            new(new Vector3( 1f,  1f, 0f), new Color4(0.08f, 0.10f, 0.16f, 1f)),
            new(new Vector3( 1f, -1f, 0f), new Color4(0.05f, 0.06f, 0.09f, 1f))
        ];

        _customVB = GpuDevice.CreateBuffer("CustomBgVB", Marshal.SizeOf<CustomVertex>() * vertices.Length, BufferUsage.Vertex);
        _customVB.SetData<CustomVertex>(vertices);
    }

    public override void OnResize(int width, int height)
    {
        // Keep camera and render targets synchronized on window resize
        _camera?.Resize(width, height);
        _sceneTarget?.Resize(width, height);
    }

    public override void Update(GameTime gameTime)
    {
        if (Input.IsKeyPressed(Key.Escape))
        {
            Exit();
            return;
        }

        float dt = gameTime.DeltaTime;
        _totalTime += dt;

        // Player speed and input handling
        float speed = 350f;
        Vector2 movement = Vector2.Zero;

        if (Input.IsKeyDown(Key.W) || Input.IsKeyDown(Key.Up))    movement.Y -= 1f;
        if (Input.IsKeyDown(Key.S) || Input.IsKeyDown(Key.Down))  movement.Y += 1f;
        if (Input.IsKeyDown(Key.A) || Input.IsKeyDown(Key.Left))  movement.X -= 1f;
        if (Input.IsKeyDown(Key.D) || Input.IsKeyDown(Key.Right)) movement.X += 1f;

        if (movement.LengthSquared() > 0.001f)
        {
            movement = Vector2.Normalize(movement);
            _playerPos += movement * speed * dt;
        }

        // Clamp player within arena bounds
        _playerPos.X = MathHelper.Clamp(_playerPos.X, _arenaBounds.Left + 20f, _arenaBounds.Right - 20f);
        _playerPos.Y = MathHelper.Clamp(_playerPos.Y, _arenaBounds.Top + 20f, _arenaBounds.Bottom - 20f);

        // Smoothly rotate player towards mouse cursor using LerpAngle
        Vector2 mousePos = Input.MousePosition;
        Vector2 toMouse = mousePos - _playerPos;
        if (toMouse.LengthSquared() > 10f)
        {
            float targetAngle = MathF.Atan2(toMouse.Y, toMouse.X);
            _playerAngle = MathHelper.LerpAngle(_playerAngle, targetAngle, 12f * dt);
        }

        // Mouse click awards points
        if (Input.IsMouseButtonPressed(MouseButton.Left))
        {
            _score += 10;
        }

        // Smooth camera follow
        _camera?.Move((_playerPos - _camera.Position) * (6f * dt));
    }

    public override void Render(GameTime gameTime)
    {
        if (_sceneTarget == null || _spriteBatch == null || _postEffect == null) return;

        // -------------------------------------------------------------
        // PASS 1: Low-Level GPU Draw & High-Level Sprite World to SceneRT
        // -------------------------------------------------------------
        var scenePass = _sceneTarget.CreatePass(Color4.Black);

        using (var recorder = GpuDevice.CreateCommandRecorder())
        {
            using (recorder.BeginScopedPass(scenePass))
            {
                // Low-level backdrop quad draw
                if (_customPipeline != null && _customVB != null)
                {
                    recorder.SetPipeline(_customPipeline);
                    recorder.SetVertexBuffer(0, _customVB);
                    recorder.Draw(6, 0);
                }
            }
            GpuDevice.Submit(recorder);
        }

        // High-level SpriteBatch rendering into SceneRT
        _spriteBatch.Begin(_sceneTarget, _camera);

        // Draw arena boundary using Rect2D
        _spriteBatch.DrawRectangle(_arenaBounds.Position, _arenaBounds.Size, Color4.SlateGray, thickness: 3f);

        // Draw animated grid markings
        for (float x = _arenaBounds.Left + 60f; x < _arenaBounds.Right; x += 120f)
        {
            _spriteBatch.DrawLine(new Vector2(x, _arenaBounds.Top), new Vector2(x, _arenaBounds.Bottom), new Color4(0.15f, 0.18f, 0.24f, 0.5f), 1f);
        }
        for (float y = _arenaBounds.Top + 60f; y < _arenaBounds.Bottom; y += 120f)
        {
            _spriteBatch.DrawLine(new Vector2(_arenaBounds.Left, y), new Vector2(_arenaBounds.Right, y), new Color4(0.15f, 0.18f, 0.24f, 0.5f), 1f);
        }

        // Draw player hull
        _spriteBatch.FillCircle(_playerPos, 22f, Color4.RoyalBlue);
        _spriteBatch.DrawCircle(_playerPos, 22f, Color4.White, thickness: 2f);

        // Draw player gun turret pointing along _playerAngle
        Vector2 barrelEnd = _playerPos + new Vector2(MathF.Cos(_playerAngle), MathF.Sin(_playerAngle)) * 34f;
        _spriteBatch.DrawLine(_playerPos, barrelEnd, Color4.Gold, thickness: 5f);

        _spriteBatch.End();

        // -------------------------------------------------------------
        // PASS 2: Screen-Space Post-Processing (SceneRT -> BackBuffer)
        // -------------------------------------------------------------
        var uniforms = new PostUniforms
        {
            Time = _totalTime,
            VignetteIntensity = 0.55f,
            ScanlineAmount = 0.05f
        };
        _postEffect.SetUniforms(in uniforms);
        _postEffect.Apply(_sceneTarget, SwapChain.CurrentBackBuffer);

        // -------------------------------------------------------------
        // PASS 3: High-Level UI / HUD Overlay directly onto BackBuffer
        // -------------------------------------------------------------
        _spriteBatch.Begin(SwapChain.CurrentBackBuffer, null);

        var (mx, my) = Input.MousePosition;
        _spriteBatch.DrawString("KHEFEST 1.4 EXTERNAL DEVELOPER VALIDATION", new Vector2(24, 24), Color4.White, scale: 1.3f);
        _spriteBatch.DrawString($"FPS: {(int)gameTime.Fps} | Score: {_score}", new Vector2(24, 58), Color4.LimeGreen);
        _spriteBatch.DrawString($"Player: ({_playerPos.X:F0}, {_playerPos.Y:F0}) | Mouse: ({mx:F0}, {my:F0})", new Vector2(24, 86), Color4.LightSkyBlue);
        _spriteBatch.DrawString("Controls: WASD/Arrows to Move | Mouse to Aim | Left Click for Score | ESC to Exit", new Vector2(24, 114), Color4.Gray);

        _spriteBatch.End();
    }

    public override void Shutdown()
    {
        // Deterministic disposal of all application-created resources
        _customVB?.Dispose();
        _customPipeline?.Dispose();
        _customPS?.Dispose();
        _customVS?.Dispose();

        _postEffect?.Dispose();
        _sceneTarget?.Dispose();
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
                Title = "Khefest 1.4 - External Developer Validation Game",
                Width = 1280,
                Height = 720,
                VSync = true,
                Resizable = true
            })
            .ConfigureMemory(m => m with
            {
                EnableLeakTracking = true
            })
            .Build();

        var game = new StandaloneGame();
        KhefestApp.Run(game, config);
    }
}
