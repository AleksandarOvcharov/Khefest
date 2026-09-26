using System.Numerics;
using System.Runtime.InteropServices;
using Khefest.Core.Configuration;
using Khefest.Core.Errors;
using Khefest.Core.Resources;
using Khefest.Graphics.LowLevel;
using Khefest.Graphics.LowLevel.Windows;
using Khefest.Windows.Windowing;
using Khefest.Windowing;
using Xunit;

namespace Khefest.Tests;

public class MinimalRendererTests
{
    [StructLayout(LayoutKind.Sequential)]
    private struct VertexPositionColor
    {
        public Vector3 Position;
        public Color4 Color;

        public VertexPositionColor(float x, float y, float z, Color4 color)
        {
            Position = new Vector3(x, y, z);
            Color = color;
        }
    }

    private const string SimpleTriangleHlsl = @"
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
    output.Position = float4(input.Position, 1.0f);
    output.Color = input.Color;
    return output;
}

float4 PSMain(PSInput input) : SV_TARGET
{
    return input.Color;
}
";

    [Fact]
    public void GpuDevice_InitializesAndDetectsHardwareOrSoftwareAdapter()
    {
        using var device = new WindowsGpuDevice();
        Assert.NotNull(device.AdapterName);
        Assert.NotEmpty(device.AdapterName);
        Assert.True(device.DedicatedVideoMemory >= 0);
    }

    [Fact]
    public void GpuDevice_AllocatesAndUploadsBufferData()
    {
        using var device = new WindowsGpuDevice();
        using var buffer = device.CreateBuffer("TestVertexBuffer", 1024, BufferUsage.Vertex);

        Assert.Equal("TestVertexBuffer", buffer.Name);
        Assert.Equal(BufferUsage.Vertex, buffer.Usage);
        Assert.True(buffer.SizeInBytes >= 1024);

        var vertices = new[]
        {
            new VertexPositionColor(0.0f, 0.5f, 0.0f, new Color4(1f, 0f, 0f, 1f)),
            new VertexPositionColor(0.5f, -0.5f, 0.0f, new Color4(0f, 1f, 0f, 1f)),
            new VertexPositionColor(-0.5f, -0.5f, 0.0f, new Color4(0f, 0f, 1f, 1f))
        };

        // Should upload without throwing
        buffer.SetData<VertexPositionColor>(vertices);
    }

    [Fact]
    public void GpuDevice_AllocatesRenderTargetAndSampledTexture()
    {
        using var device = new WindowsGpuDevice();
        using var texture = device.CreateTexture(
            "OffscreenRT",
            256,
            256,
            GpuFormat.R8G8B8A8_UNorm,
            TextureUsage.RenderTarget | TextureUsage.Sampled);

        Assert.Equal(256, texture.Width);
        Assert.Equal(256, texture.Height);
        Assert.Equal(GpuFormat.R8G8B8A8_UNorm, texture.Format);
        Assert.True(texture.Usage.HasFlag(TextureUsage.RenderTarget));
        Assert.True(texture.Usage.HasFlag(TextureUsage.Sampled));
    }

    [Fact]
    public void GpuDevice_CompilesHlslShadersSuccessfully()
    {
        using var device = new WindowsGpuDevice();

        using var vs = device.CompileShader("TriangleVS", ShaderStage.Vertex, SimpleTriangleHlsl, "VSMain");
        Assert.NotNull(vs);
        Assert.Equal(ShaderStage.Vertex, vs.Stage);
        Assert.Equal("VSMain", vs.EntryPoint);
        Assert.False(vs.Bytecode.IsEmpty);

        using var ps = device.CompileShader("TrianglePS", ShaderStage.Pixel, SimpleTriangleHlsl, "PSMain");
        Assert.NotNull(ps);
        Assert.Equal(ShaderStage.Pixel, ps.Stage);
        Assert.Equal("PSMain", ps.EntryPoint);
        Assert.False(ps.Bytecode.IsEmpty);
    }

    [Fact]
    public void GpuDevice_ThrowsOnInvalidHlslSyntax()
    {
        using var device = new WindowsGpuDevice();
        const string invalidHlsl = "This is completely invalid HLSL syntax!";

        var ex = Assert.Throws<KhefestGraphicsException>(() =>
            device.CompileShader("BrokenShader", ShaderStage.Vertex, invalidHlsl, "main"));

        Assert.Equal(KhefestErrorCode.GraphicsShaderCompilationFailed, ex.ErrorCode);
    }

    [Fact]
    public void GpuDevice_RendersFirstColoredTriangleThroughCleanKhefestApi()
    {
        var config = new WindowConfig
        {
            Title = "Phase 4 - First Triangle Test",
            Width = 640,
            Height = 480,
            Mode = WindowMode.Windowed
        };

        using var window = new Win32Window(config);
        using var device = new WindowsGpuDevice();
        using var swapChain = device.CreateSwapChain(window, window.ClientWidth, window.ClientHeight, true);

        // 1. Compile Shaders
        using var vs = device.CompileShader("TriangleVS", ShaderStage.Vertex, SimpleTriangleHlsl, "VSMain");
        using var ps = device.CompileShader("TrianglePS", ShaderStage.Pixel, SimpleTriangleHlsl, "PSMain");

        // 2. Define Vertex Layout (Position: Float3, Color: Float4)
        var layout = new VertexLayout(
            sizeof(float) * 7,
            new VertexElement("POSITION", 0, GpuFormat.R32G32B32_Float, 0),
            new VertexElement("COLOR", 0, GpuFormat.R32G32B32A32_Float, sizeof(float) * 3));

        // 3. Create Pipeline
        var pipelineDesc = new PipelineDesc
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
        using var pipeline = device.CreatePipeline("TrianglePipeline", pipelineDesc);

        // 4. Create Vertex Buffer with 3 vertices
        var vertices = new[]
        {
            new VertexPositionColor(0.0f, 0.5f, 0.0f, new Color4(1.0f, 0.0f, 0.0f, 1.0f)),   // Top: Red
            new VertexPositionColor(0.5f, -0.5f, 0.0f, new Color4(0.0f, 1.0f, 0.0f, 1.0f)),  // Right: Green
            new VertexPositionColor(-0.5f, -0.5f, 0.0f, new Color4(0.0f, 0.0f, 1.0f, 1.0f))  // Left: Blue
        };
        using var vbo = device.CreateBuffer("TriangleVBO", vertices.Length * sizeof(float) * 7, BufferUsage.Vertex);
        vbo.SetData<VertexPositionColor>(vertices);

        // 5. Render Pass & Command Recording
        using var recorder = device.CreateCommandRecorder();
        var pass = new RenderPassDesc
        {
            ColorTarget = swapChain.CurrentBackBuffer,
            ClearColor = Color4.CornflowerBlue,
            ClearColorTarget = true
        };

        recorder.BeginPass(pass);
        recorder.SetPipeline(pipeline);
        recorder.SetVertexBuffer(0, vbo);
        recorder.Draw(3, 0);
        recorder.EndPass();

        device.Submit(recorder);
        swapChain.Present(true);
        device.WaitForGpu();
    }

    [Fact]
    public void DualModeResourceManager_TracksGpuResourcesAndReportsCleanShutdown()
    {
        var memConfig = new MemoryConfig
        {
            AutoMemManagement = false, // Strict manual mode
            EnableLeakTracking = true
        };

        var resourceManager = new ResourceManager(memConfig);
        using (var device = new WindowsGpuDevice(resourceManager))
        {
            var vbo = device.CreateBuffer("ManualVBO", 256, BufferUsage.Vertex);
            var tex = device.CreateTexture("ManualTex", 64, 64, GpuFormat.R8G8B8A8_UNorm, TextureUsage.Sampled);

            Assert.Equal(2, resourceManager.Tracker.GetActiveResources().Count);

            // Dispose manually
            vbo.Dispose();
            tex.Dispose();
        }

        // Verify zero leaks detected in manual mode
        var leaks = resourceManager.Tracker.CheckForLeaks();
        Assert.Empty(leaks);
        resourceManager.Dispose();
    }
}
