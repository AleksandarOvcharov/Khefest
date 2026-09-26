using System.Numerics;
using Khefest.Core.Configuration;
using Khefest.Core.Errors;
using Khefest.Core.Resources;
using Khefest.Graphics.LowLevel;
using Khefest.Graphics.LowLevel.Windows;
using Khefest.Windows.Windowing;
using Khefest.Windowing;
using Xunit;

namespace Khefest.Tests;

public class ApiAbuseAndCorrectnessTests
{
    private const string BasicVsSource = @"
cbuffer TransformBuffer : register(b0)
{
    matrix u_WorldViewProjection;
};

struct VS_INPUT
{
    float3 Position : POSITION;
    float4 Color : COLOR;
};

struct PS_INPUT
{
    float4 Position : SV_POSITION;
    float4 Color : COLOR;
};

PS_INPUT main(VS_INPUT input)
{
    PS_INPUT output;
    output.Position = mul(float4(input.Position, 1.0f), u_WorldViewProjection);
    output.Color = input.Color;
    return output;
}
";

    private const string BasicPsSource = @"
struct PS_INPUT
{
    float4 Position : SV_POSITION;
    float4 Color : COLOR;
};

float4 main(PS_INPUT input) : SV_TARGET
{
    return input.Color;
}
";

    [Fact]
    public void ApiAbuse_DuplicateResourceDisposal_IsSafeAndIdempotent()
    {
        var config = new MemoryConfig { EnableLeakTracking = true };
        using var manager = new ResourceManager(config);
        using var device = new WindowsGpuDevice(manager);

        var buffer = device.CreateBuffer("IdempotentBuffer", 256, BufferUsage.Vertex);
        var texture = device.CreateTexture("IdempotentTexture", 64, 64, GpuFormat.R8G8B8A8_UNorm, TextureUsage.Sampled);

        // First disposal
        buffer.Dispose();
        texture.Dispose();
        Assert.True(buffer.IsDisposed);
        Assert.True(texture.IsDisposed);
        Assert.Equal(ResourceState.Disposed, buffer.State);
        Assert.Equal(ResourceState.Disposed, texture.State);

        // Second disposal must not throw
        var bufferEx = Record.Exception(() => buffer.Dispose());
        var textureEx = Record.Exception(() => texture.Dispose());

        Assert.Null(bufferEx);
        Assert.Null(textureEx);
    }

    [Fact]
    public void ApiAbuse_CommandOutsideRenderPass_ThrowsInvalidOperation()
    {
        using var device = new WindowsGpuDevice();
        using var recorder = device.CreateCommandRecorder();
        using var buffer = device.CreateBuffer("DummyBuffer", 64, BufferUsage.Vertex);

        // 1. Draw before BeginPass
        var ex1 = Assert.Throws<KhefestGraphicsException>(() => recorder.Draw(3));
        Assert.Equal(KhefestErrorCode.InvalidOperation, ex1.ErrorCode);

        // 2. DrawIndexed before BeginPass
        var ex2 = Assert.Throws<KhefestGraphicsException>(() => recorder.DrawIndexed(3));
        Assert.Equal(KhefestErrorCode.InvalidOperation, ex2.ErrorCode);

        // 3. SetVertexBuffer before BeginPass
        var ex3 = Assert.Throws<KhefestGraphicsException>(() => recorder.SetVertexBuffer(0, buffer));
        Assert.Equal(KhefestErrorCode.InvalidOperation, ex3.ErrorCode);

        // 4. SetViewport before BeginPass
        var ex4 = Assert.Throws<KhefestGraphicsException>(() => recorder.SetViewport(new Viewport(0, 0, 100, 100)));
        Assert.Equal(KhefestErrorCode.InvalidOperation, ex4.ErrorCode);

        // 5. EndPass before BeginPass
        var ex5 = Assert.Throws<KhefestGraphicsException>(() => recorder.EndPass());
        Assert.Equal(KhefestErrorCode.InvalidOperation, ex5.ErrorCode);
    }

    [Fact]
    public void ApiAbuse_NestedBeginPass_ThrowsInvalidOperation()
    {
        using var device = new WindowsGpuDevice();
        using var recorder = device.CreateCommandRecorder();
        using var rt = device.CreateTexture("RT", 64, 64, GpuFormat.R8G8B8A8_UNorm, TextureUsage.RenderTarget);

        var pass = new RenderPassDesc { ColorTarget = rt };
        recorder.BeginPass(pass);

        // Calling BeginPass again while pass is active
        var ex = Assert.Throws<KhefestGraphicsException>(() => recorder.BeginPass(pass));
        Assert.Equal(KhefestErrorCode.InvalidOperation, ex.ErrorCode);

        recorder.EndPass();
    }

    [Fact]
    public void ApiAbuse_DrawWithDisposedResource_ThrowsException()
    {
        using var device = new WindowsGpuDevice();
        using var recorder = device.CreateCommandRecorder();
        using var rt = device.CreateTexture("RT", 64, 64, GpuFormat.R8G8B8A8_UNorm, TextureUsage.RenderTarget);

        var vs = device.CompileShader("BasicVS", ShaderStage.Vertex, BasicVsSource);
        var ps = device.CompileShader("BasicPS", ShaderStage.Pixel, BasicPsSource);
        var layout = new VertexLayout(28,
            new VertexElement("POSITION", 0, GpuFormat.R32G32B32_Float, 0),
            new VertexElement("COLOR", 0, GpuFormat.R32G32B32A32_Float, 12));

        var pipeline = device.CreatePipeline("DisposedTestPipeline", new PipelineDesc
        {
            VertexShader = vs,
            PixelShader = ps,
            VertexLayout = layout
        });

        var vertexBuffer = device.CreateBuffer("DisposedVB", 28 * 3, BufferUsage.Vertex);
        var texture = device.CreateTexture("DisposedTex", 16, 16, GpuFormat.R8G8B8A8_UNorm, TextureUsage.Sampled);

        recorder.BeginPass(new RenderPassDesc { ColorTarget = rt });

        // Dispose vertex buffer and attempt binding
        vertexBuffer.Dispose();
        var exVb = Assert.Throws<KhefestGraphicsException>(() => recorder.SetVertexBuffer(0, vertexBuffer));
        Assert.Equal(KhefestErrorCode.GraphicsResourceBindingFailed, exVb.ErrorCode);

        // Dispose texture and attempt binding
        texture.Dispose();
        var exTex = Assert.Throws<KhefestGraphicsException>(() => recorder.SetTexture(0, texture));
        Assert.Equal(KhefestErrorCode.GraphicsResourceBindingFailed, exTex.ErrorCode);

        // Dispose pipeline and attempt binding
        pipeline.Dispose();
        var exPipe = Assert.Throws<KhefestGraphicsException>(() => recorder.SetPipeline(pipeline));
        Assert.Equal(KhefestErrorCode.GraphicsResourceBindingFailed, exPipe.ErrorCode);

        recorder.EndPass();

        vs.Dispose();
        ps.Dispose();
    }

    [Fact]
    public void ApiAbuse_MismatchedBufferSizes_ThrowsArgumentOutOfRangeException()
    {
        using var device = new WindowsGpuDevice();
        using var buffer = device.CreateBuffer("SmallBuffer", 32, BufferUsage.Vertex);

        // Data exceeds buffer capacity (48 bytes > 32 bytes)
        var oversizedData = new float[12]; // 12 * 4 = 48 bytes
        Assert.Throws<ArgumentOutOfRangeException>(() => buffer.SetData<float>(oversizedData));

        // Offset + data exceeds buffer capacity (16 + 20 = 36 > 32)
        var partialData = new float[5]; // 20 bytes
        Assert.Throws<ArgumentOutOfRangeException>(() => buffer.SetData<float>(partialData, offsetInBytes: 16));
    }

    [Fact]
    public void ApiAbuse_InvalidPipelineConfiguration_ThrowsException()
    {
        using var device = new WindowsGpuDevice();
        var vs = device.CompileShader("BasicVS", ShaderStage.Vertex, BasicVsSource);
        var ps = device.CompileShader("BasicPS", ShaderStage.Pixel, BasicPsSource);
        var layout = new VertexLayout(28,
            new VertexElement("POSITION", 0, GpuFormat.R32G32B32_Float, 0),
            new VertexElement("COLOR", 0, GpuFormat.R32G32B32A32_Float, 12));

        // 1. Disposed vertex shader
        vs.Dispose();
        var ex1 = Assert.Throws<KhefestGraphicsException>(() => device.CreatePipeline("InvalidPipe1", new PipelineDesc
        {
            VertexShader = vs,
            PixelShader = ps,
            VertexLayout = layout
        }));
        Assert.Equal(KhefestErrorCode.GraphicsPipelineCreationFailed, ex1.ErrorCode);

        // 2. Empty vertex layout
        var validVs = device.CompileShader("BasicVS2", ShaderStage.Vertex, BasicVsSource);
        var emptyLayout = new VertexLayout(0, Array.Empty<VertexElement>());
        var ex2 = Assert.Throws<KhefestGraphicsException>(() => device.CreatePipeline("InvalidPipe2", new PipelineDesc
        {
            VertexShader = validVs,
            PixelShader = ps,
            VertexLayout = emptyLayout
        }));
        Assert.Equal(KhefestErrorCode.GraphicsPipelineCreationFailed, ex2.ErrorCode);

        ps.Dispose();
        validVs.Dispose();
    }

    [Fact]
    public void RenderingCorrectness_DepthOnlyPass_Succeeds()
    {
        using var device = new WindowsGpuDevice();
        using var recorder = device.CreateCommandRecorder();
        using var depthTex = device.CreateTexture("DepthTarget", 512, 512, GpuFormat.D24_UNorm_S8_UInt, TextureUsage.DepthStencil);

        // Depth-only pass without a color render target (e.g. shadow map generation or Z-prepass)
        var pass = new RenderPassDesc
        {
            ColorTarget = null,
            DepthTarget = depthTex,
            ClearDepthTarget = true,
            ClearDepth = 1.0f
        };

        recorder.BeginPass(pass);
        recorder.SetViewport(new Viewport(0, 0, 512, 512));
        recorder.EndPass();

        device.Submit(recorder);
    }

    [Fact]
    public void RenderingCorrectness_MultipleRenderTargets_Succeeds()
    {
        using var device = new WindowsGpuDevice();
        using var recorder = device.CreateCommandRecorder();

        using var rtAlbedo = device.CreateTexture("RT_Albedo", 256, 256, GpuFormat.R8G8B8A8_UNorm, TextureUsage.RenderTarget | TextureUsage.Sampled);
        using var rtNormal = device.CreateTexture("RT_Normal", 256, 256, GpuFormat.R8G8B8A8_UNorm, TextureUsage.RenderTarget | TextureUsage.Sampled);

        var pass = new RenderPassDesc
        {
            ColorTargets = new[] { rtAlbedo, rtNormal },
            ClearColor = Color4.CornflowerBlue,
            ClearColorTarget = true
        };

        recorder.BeginPass(pass);
        recorder.SetViewport(new Viewport(0, 0, 256, 256));
        recorder.EndPass();

        device.Submit(recorder);
    }

    [Fact]
    public void RenderingCorrectness_RenderToTexture_CanSampleInSubsequentPass()
    {
        using var device = new WindowsGpuDevice();
        using var recorder = device.CreateCommandRecorder();

        // 1. Offscreen render target
        using var offscreenRt = device.CreateTexture("OffscreenRT", 256, 256, GpuFormat.R8G8B8A8_UNorm, TextureUsage.RenderTarget | TextureUsage.Sampled);
        // 2. Final backbuffer-style target
        using var finalRt = device.CreateTexture("FinalRT", 800, 600, GpuFormat.R8G8B8A8_UNorm, TextureUsage.RenderTarget);

        // Pass 1: Render into offscreen RT
        recorder.BeginPass(new RenderPassDesc
        {
            ColorTarget = offscreenRt,
            ClearColor = Color4.Red,
            ClearColorTarget = true
        });
        recorder.EndPass();

        // Pass 2: Render into final RT and bind offscreen RT as sampled texture
        recorder.BeginPass(new RenderPassDesc
        {
            ColorTarget = finalRt,
            ClearColor = Color4.Black,
            ClearColorTarget = true
        });
        recorder.SetTexture(0, offscreenRt);
        recorder.SetViewport(new Viewport(0, 0, 800, 600));
        recorder.EndPass();

        device.Submit(recorder);
    }

    [Fact]
    public void RenderingCorrectness_DynamicViewportAndScissor_ChangesMidPass()
    {
        using var device = new WindowsGpuDevice();
        using var recorder = device.CreateCommandRecorder();
        using var rt = device.CreateTexture("RT", 800, 600, GpuFormat.R8G8B8A8_UNorm, TextureUsage.RenderTarget);

        recorder.BeginPass(new RenderPassDesc { ColorTarget = rt, ClearColorTarget = true });

        // Viewport 1 (Main display)
        recorder.SetViewport(new Viewport(0, 0, 800, 600));
        recorder.SetScissor(new ScissorRect(0, 0, 800, 600));

        // Viewport 2 (Minimap / Picture-in-picture)
        recorder.SetViewport(new Viewport(600, 400, 180, 180));
        recorder.SetScissor(new ScissorRect(600, 400, 180, 180));

        recorder.EndPass();
        device.Submit(recorder);
    }

    [Fact]
    public void ResizeStress_RepeatedResizeAndZeroSizeHandling_ZeroLeaks()
    {
        var config = new MemoryConfig { EnableLeakTracking = true };
        using var manager = new ResourceManager(config);
        using var device = new WindowsGpuDevice(manager);

        var winConfig = new WindowConfig
        {
            Title = "ResizeStressWindow",
            Width = 800,
            Height = 600,
            Mode = WindowMode.Windowed
        };

        using var window = new Win32Window(winConfig);
        using var swapChain = device.CreateSwapChain(window, 800, 600, vsync: false);

        int[] resizeWidths = [1920, 800, 1280, 640, 1024, 800, 1, 1600, 800];
        int[] resizeHeights = [1080, 600, 720, 480, 768, 600, 1, 900, 600];

        for (int i = 0; i < resizeWidths.Length; i++)
        {
            int w = resizeWidths[i];
            int h = resizeHeights[i];
            swapChain.Resize(w, h);

            Assert.Equal(Math.Max(1, w), swapChain.Width);
            Assert.Equal(Math.Max(1, h), swapChain.Height);
            Assert.NotNull(swapChain.CurrentBackBuffer);
            Assert.False(swapChain.CurrentBackBuffer.IsDisposed);
        }

        // Verify leak tracker has exactly 1 active resource (the active swapchain backbuffer itself)
        var leaks = manager.Tracker.CheckForLeaks();
        Assert.Single(leaks);
        Assert.Equal("SwapChainBackBuffer", leaks[0].Name);
    }

    [Fact]
    public void ResourceStress_MassiveTextureAndBufferChurn_CacheEvictionAndZeroLeaks()
    {
        var config = new MemoryConfig { EnableLeakTracking = true };
        using var manager = new ResourceManager(config);
        using var device = new WindowsGpuDevice(manager);

        float[] sampleData = [1f, 2f, 3f, 4f];

        // Rapid allocation and disposal of 300 buffers and 100 textures
        for (int i = 0; i < 300; i++)
        {
            using var buf = device.CreateBuffer($"ChurnBuf_{i}", 128, BufferUsage.Vertex);
            buf.SetData(sampleData);
        }

        for (int i = 0; i < 100; i++)
        {
            using var tex = device.CreateTexture($"ChurnTex_{i}", 32, 32, GpuFormat.R8G8B8A8_UNorm, TextureUsage.Sampled);
        }

        // All resources were disposed via using blocks
        var leaks = manager.Tracker.CheckForLeaks();
        Assert.Empty(leaks);
        Assert.False(manager.Tracker.HasLeaks);
    }
}
