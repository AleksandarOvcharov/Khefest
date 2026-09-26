using System.Numerics;
using System.Runtime.InteropServices;
using Khefest.Core.Configuration;
using Khefest.Core.Errors;
using Khefest.Core.Logging;
using Khefest.Graphics.LowLevel;
using Khefest.Input;
using Khefest.Windows.Application;
using Khefest.Windows.Timing;

namespace HelloTriangle;

[StructLayout(LayoutKind.Sequential)]
public struct VertexPositionColor
{
    public Vector3 Position;
    public Color4 Color;

    public VertexPositionColor(float x, float y, float z, Color4 color)
    {
        Position = new Vector3(x, y, z);
        Color = color;
    }
}

public sealed class HelloTriangleGame : Game
{
    private static readonly ILogger Logger = LogManager.GetLogger(KhefestSubsystem.Core, "HelloTriangle");

    private const string TriangleShaderSource = @"
cbuffer FrameConstants : register(b0)
{
    float RotationAngle;
    float Scale;
    float2 Padding;
};

struct VSInput
{
    float3 Position : POSITION;
    float4 Color : COLOR;
};

struct PSInput
{
    float4 Position : SV_POSITION;
    float4 Color : COLOR;
};

PSInput VSMain(VSInput input)
{
    PSInput output;
    
    // Apply 2D rotation in the XY plane
    float cosA = cos(RotationAngle);
    float sinA = sin(RotationAngle);
    
    float2 rotatedPos;
    rotatedPos.x = (input.Position.x * cosA - input.Position.y * sinA) * Scale;
    rotatedPos.y = (input.Position.x * sinA + input.Position.y * cosA) * Scale;
    
    output.Position = float4(rotatedPos, input.Position.z, 1.0f);
    output.Color = input.Color;
    return output;
}

float4 PSMain(PSInput input) : SV_TARGET
{
    return input.Color;
}
";

    [StructLayout(LayoutKind.Sequential, Size = 16)]
    private struct FrameConstants
    {
        public float RotationAngle;
        public float Scale;
        public float Pad1;
        public float Pad2;
    }

    private IGpuBuffer? _vertexBuffer;
    private IGpuBuffer? _constantBuffer;
    private IPipeline? _solidPipeline;
    private IPipeline? _wireframePipeline;
    private bool _isWireframe;
    private float _rotation;

    public override void Initialize()
    {
        Logger.Info($"GPU Adapter: {GpuDevice.AdapterName} ({GpuDevice.DedicatedVideoMemory / (1024 * 1024)} MB VRAM)");

        // 1. Compile Shaders
        var vs = GpuDevice.CompileShader("TriangleVS", ShaderStage.Vertex, TriangleShaderSource, "VSMain");
        var ps = GpuDevice.CompileShader("TrianglePS", ShaderStage.Pixel, TriangleShaderSource, "PSMain");

        // 2. Vertex Layout (Position: Float3, Color: Float4)
        var layout = new VertexLayout(
            sizeof(float) * 7,
            new VertexElement("POSITION", 0, GpuFormat.R32G32B32_Float, 0),
            new VertexElement("COLOR", 0, GpuFormat.R32G32B32A32_Float, sizeof(float) * 3));

        // 3. Create Solid and Wireframe Pipelines
        var solidDesc = new PipelineDesc
        {
            VertexShader = vs,
            PixelShader = ps,
            VertexLayout = layout,
            Topology = PrimitiveTopology.TriangleList,
            CullMode = CullMode.None,
            FillMode = FillMode.Solid,
            BlendMode = BlendMode.Opaque,
            DepthTestEnabled = false,
            DepthWriteEnabled = false
        };
        _solidPipeline = GpuDevice.CreatePipeline("SolidTrianglePipeline", solidDesc);

        var wireDesc = solidDesc with { FillMode = FillMode.Wireframe };
        _wireframePipeline = GpuDevice.CreatePipeline("WireframeTrianglePipeline", wireDesc);

        // 4. Create Vertex Buffer (Top: Red, Right: Green, Left: Blue)
        var vertices = new[]
        {
            new VertexPositionColor(0.0f, 0.55f, 0.0f, new Color4(1.0f, 0.2f, 0.2f, 1.0f)),   // Top: Vibrant Red
            new VertexPositionColor(0.5f, -0.45f, 0.0f, new Color4(0.2f, 1.0f, 0.2f, 1.0f)),  // Right: Vibrant Green
            new VertexPositionColor(-0.5f, -0.45f, 0.0f, new Color4(0.2f, 0.5f, 1.0f, 1.0f))  // Left: Vibrant Blue
        };
        _vertexBuffer = GpuDevice.CreateBuffer("TriangleVBO", vertices.Length * sizeof(float) * 7, BufferUsage.Vertex);
        _vertexBuffer.SetData<VertexPositionColor>(vertices);

        // 5. Create Constant Buffer for dynamic rotation & scale
        _constantBuffer = GpuDevice.CreateBuffer("FrameConstants", Marshal.SizeOf<FrameConstants>(), BufferUsage.Uniform);

        Logger.Info("HelloTriangle initialized successfully. Press [TAB] to toggle wireframe, [F11] for fullscreen, [ESC] to exit.");
    }

    public override void Update(GameTime time)
    {
        // Toggle Wireframe with Tab
        if (Input.IsKeyPressed(Key.Tab))
        {
            _isWireframe = !_isWireframe;
            Logger.Info($"Wireframe mode: {(_isWireframe ? "ON" : "OFF")}");
        }

        // Toggle Fullscreen with F11
        if (Input.IsKeyPressed(Key.F11))
        {
            var nextMode = Window.Mode == WindowMode.Windowed
                ? WindowMode.BorderlessFullscreen
                : WindowMode.Windowed;
            Window.SetMode(nextMode);
        }

        // Exit on Escape
        if (Input.IsKeyPressed(Key.Escape))
        {
            Exit();
            return;
        }

        // Rotate triangle smoothly over time
        _rotation += (float)time.ElapsedTime.TotalSeconds * 1.5f;

        // Update constant buffer
        var constants = new FrameConstants
        {
            RotationAngle = _rotation,
            Scale = 0.85f + 0.15f * MathF.Sin((float)time.TotalTime.TotalSeconds * 2.0f)
        };
        _constantBuffer?.SetData(MemoryMarshal.CreateReadOnlySpan(ref constants, 1));
    }

    public override void Render(GameTime time)
    {
        if (_vertexBuffer == null || _constantBuffer == null) return;

        using var recorder = GpuDevice.CreateCommandRecorder();

        var pass = new RenderPassDesc
        {
            ColorTarget = SwapChain.CurrentBackBuffer,
            ClearColor = new Color4(0.1f, 0.12f, 0.16f, 1.0f), // Deep slate gray clear color
            ClearColorTarget = true
        };

        recorder.BeginPass(pass);

        var pipeline = _isWireframe ? _wireframePipeline! : _solidPipeline!;
        recorder.SetPipeline(pipeline);
        recorder.SetVertexBuffer(0, _vertexBuffer);
        recorder.SetUniformBuffer(0, _constantBuffer);
        recorder.Draw(3, 0);

        recorder.EndPass();

        GpuDevice.Submit(recorder);
    }

    public override void Shutdown()
    {
        Logger.Info("Cleaning up HelloTriangle resources...");
        _solidPipeline?.Dispose();
        _wireframePipeline?.Dispose();
        _vertexBuffer?.Dispose();
        _constantBuffer?.Dispose();
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
                Title = "Khefest - First Triangle (Phase 4)",
                Width = 1024,
                Height = 640,
                VSync = true
            })
            .ConfigureLogging(l => l with
            {
                MinimumLevel = LogLevel.Info
            })
            .Build();

        KhefestApp.Run(new HelloTriangleGame(), config);
    }
}
