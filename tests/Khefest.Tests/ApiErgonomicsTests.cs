using System.Numerics;
using System.Runtime.InteropServices;
using Khefest.Core.Configuration;
using Khefest.Core.Logging;
using Khefest.Core.Resources;
using Khefest.Graphics.LowLevel;
using Khefest.Graphics.LowLevel.Windows;
using Khefest.Graphics.Mathematics;
using Khefest.Graphics.PostProcessing;
using Khefest.Graphics.Texturing;
using Khefest.Input;
using Khefest.Windows.Timing;
using Xunit;

namespace Khefest.Tests;

public class ApiErgonomicsTests
{
    [StructLayout(LayoutKind.Sequential)]
    private struct TestUniformData
    {
        public float Time;
        public float Intensity;
        public float Scale;
        public float Padding;
    }

    [Fact]
    public void Rect2D_GeometryOperations_BehaveAccurately()
    {
        var r1 = new Rect2D(10f, 20f, 100f, 50f);

        // Core dimensions and bounds
        Assert.Equal(10f, r1.X);
        Assert.Equal(20f, r1.Y);
        Assert.Equal(100f, r1.Width);
        Assert.Equal(50f, r1.Height);
        Assert.Equal(10f, r1.Left);
        Assert.Equal(20f, r1.Top);
        Assert.Equal(110f, r1.Right);
        Assert.Equal(70f, r1.Bottom);
        Assert.Equal(new Vector2(60f, 45f), r1.Center);
        Assert.Equal(new Vector2(10f, 20f), r1.TopLeft);
        Assert.Equal(new Vector2(110f, 70f), r1.BottomRight);

        // Contains point
        Assert.True(r1.Contains(new Vector2(15f, 25f)));
        Assert.True(r1.Contains(new Vector2(10f, 20f))); // inclusive boundary
        Assert.True(r1.Contains(new Vector2(110f, 70f)));
        Assert.False(r1.Contains(new Vector2(9f, 20f)));
        Assert.False(r1.Contains(new Vector2(111f, 70f)));

        // Contains rect
        var inner = new Rect2D(20f, 30f, 40f, 20f);
        Assert.True(r1.Contains(inner));
        Assert.False(inner.Contains(r1));

        // Intersects
        var overlapping = new Rect2D(50f, 40f, 100f, 50f);
        Assert.True(r1.Intersects(overlapping));
        Assert.True(overlapping.Intersects(r1));

        var intersection = r1.Intersect(overlapping);
        Assert.Equal(50f, intersection.X);
        Assert.Equal(40f, intersection.Y);
        Assert.Equal(60f, intersection.Width);
        Assert.Equal(30f, intersection.Height);

        var disjoint = new Rect2D(200f, 200f, 10f, 10f);
        Assert.False(r1.Intersects(disjoint));
        Assert.Equal(Rect2D.Empty, r1.Intersect(disjoint));

        // Union
        var union = r1.Union(overlapping);
        Assert.Equal(10f, union.X);
        Assert.Equal(20f, union.Y);
        Assert.Equal(140f, union.Width);
        Assert.Equal(70f, union.Height);

        // Inflate
        var inflated = r1.Inflate(5f, 10f);
        Assert.Equal(5f, inflated.X);
        Assert.Equal(10f, inflated.Y);
        Assert.Equal(110f, inflated.Width);
        Assert.Equal(70f, inflated.Height);

        // Offset
        var offset = r1.Offset(new Vector2(20f, -10f));
        Assert.Equal(30f, offset.X);
        Assert.Equal(10f, offset.Y);
        Assert.Equal(100f, offset.Width);
        Assert.Equal(50f, offset.Height);
    }

    [Fact]
    public void MathHelper_LerpAngle_InterpolatesShortestArcAcrossBoundary()
    {
        // 1. Basic interpolation
        float r1 = MathHelper.LerpAngle(0f, MathF.PI, 0.5f);
        Assert.Equal(MathF.PI * 0.5f, r1, 0.001f);

        // 2. Shortest arc crossing +PI / -PI boundary
        // from +170 degrees (2.967 rad) to -170 degrees (-2.967 rad) -> should cross +/-PI (delta is +20 deg, not -340 deg)
        float from = MathHelper.ToRadians(170f);
        float to = MathHelper.ToRadians(-170f);
        float halfway = MathHelper.LerpAngle(from, to, 0.5f);
        float halfwayDeg = MathHelper.ToDegrees(halfway);

        // Halfway should be 180 degrees (or -180 degrees)
        Assert.True(Math.Abs(Math.Abs(halfwayDeg) - 180f) < 0.1f);

        // 3. Clamped endpoints at 0 and 1
        Assert.Equal(from, MathHelper.LerpAngle(from, to, 0f), 0.001f);
        Assert.Equal(to, MathHelper.LerpAngle(from, to, 1f), 0.001f);
    }

    [Fact]
    public void InputErgonomics_Vector2AndPixelQueries_AndVectorDeconstruction()
    {
        var input = new InputManager();

        input.OnMouseMove(320, 240);

        // Vector2 return
        Vector2 pos = input.MousePosition;
        Assert.Equal(320f, pos.X);
        Assert.Equal(240f, pos.Y);

        // Pixel tuple return
        var (px, py) = input.MousePositionPixels;
        Assert.Equal(320, px);
        Assert.Equal(240, py);

        // Vector2 deconstruction extension
        var (vx, vy) = input.MousePosition;
        Assert.Equal(320f, vx);
        Assert.Equal(240f, vy);

        // Delta
        input.Update();
        input.OnMouseMove(350, 260);

        var (dx, dy) = input.MouseDelta;
        Assert.Equal(30f, dx);
        Assert.Equal(20f, dy);
    }

    [Fact]
    public void GameTime_DeltaTime_ReturnsAccurateSeconds()
    {
        var total = TimeSpan.FromSeconds(10.5);
        var elapsed = TimeSpan.FromMilliseconds(16.6667);
        var gameTime = new GameTime(total, elapsed, 100, 60f);

        Assert.Equal(0.0166667f, gameTime.DeltaTime, 0.0001f);
        Assert.Equal((float)total.TotalSeconds, (float)gameTime.TotalSeconds, 0.0001f);
    }

    [Fact]
    public void ScopedRenderPass_GuaranteesEndPass_EvenOnException()
    {
        using var device = new WindowsGpuDevice();
        using var recorder = device.CreateCommandRecorder();
        using var target = device.CreateTexture("ScopedTarget", 64, 64, GpuFormat.R8G8B8A8_UNorm, TextureUsage.RenderTarget);

        Assert.False(recorder.IsInPass);

        var passDesc = new RenderPassDesc
        {
            ColorTarget = target,
            ClearColor = Color4.CornflowerBlue,
            ClearColorTarget = true
        };

        // Normal execution path
        using (var pass = recorder.BeginScopedPass(passDesc))
        {
            Assert.True(recorder.IsInPass);
        }
        Assert.False(recorder.IsInPass);

        // Exception exit path
        Assert.Throws<InvalidOperationException>((Action)(() =>
        {
            using var pass = recorder.BeginScopedPass(passDesc);
            Assert.True(recorder.IsInPass);
            throw new InvalidOperationException("Simulation error inside pass");
        }));

        // Must still be safely ended
        Assert.False(recorder.IsInPass);
    }

    [Fact]
    public void GpuBuffer_SetDataSingleStruct_ZeroAllocationUpload()
    {
        using var device = new WindowsGpuDevice();
        using var buffer = device.CreateBuffer("UniformStructBuf", 64, BufferUsage.Uniform);

        var data = new TestUniformData
        {
            Time = 12.34f,
            Intensity = 0.85f,
            Scale = 2.0f,
            Padding = 0f
        };

        // Single struct upload without allocating array
        buffer.SetData(in data);

        // Offset upload
        buffer.SetData(in data, offsetInBytes: 32);

        // Boundary violation throws
        Assert.Throws<ArgumentOutOfRangeException>((Action)(() =>
        {
            buffer.SetData(in data, offsetInBytes: 60);
        }));
    }

    [Fact]
    public void RenderTarget2D_LifecyclePassGenerationAndResizing_ZeroLeaks()
    {
        var config = new MemoryConfig { EnableLeakTracking = true };
        using var manager = new ResourceManager(config);
        using var device = new WindowsGpuDevice(manager);

        var rt = new RenderTarget2D(
            device,
            "GameCanvasRT",
            width: 800,
            height: 600,
            hasDepth: true,
            format: GpuFormat.R8G8B8A8_UNorm,
            manager: manager);

        Assert.Equal(800, rt.Width);
        Assert.Equal(600, rt.Height);
        Assert.Equal(GpuFormat.R8G8B8A8_UNorm, rt.Format);
        Assert.True(rt.HasDepth);
        Assert.NotNull(rt.DepthTexture);

        // Create pass desc
        var pass = rt.CreatePass(Color4.CornflowerBlue, clearDepth: true);
        Assert.Same(rt.ColorTexture, pass.ColorTarget);
        Assert.Same(rt.DepthTexture, pass.DepthTarget);
        Assert.Equal(Color4.CornflowerBlue, pass.ClearColor);
        Assert.True(pass.ClearDepthTarget);

        // Test resizing
        rt.Resize(1280, 720);
        Assert.Equal(1280, rt.Width);
        Assert.Equal(720, rt.Height);
        Assert.NotNull(rt.ColorTexture);
        Assert.NotNull(rt.DepthTexture);
        Assert.False(rt.ColorTexture.IsDisposed);
        Assert.False(rt.DepthTexture.IsDisposed);

        // Repeated resize stress
        int[] widths = [1920, 640, 1024, 800];
        int[] heights = [1080, 480, 768, 600];
        for (int i = 0; i < widths.Length; i++)
        {
            rt.Resize(widths[i], heights[i]);
            Assert.Equal(widths[i], rt.Width);
            Assert.Equal(heights[i], rt.Height);
        }

        // Dispose and check leaks
        rt.Dispose();
        Assert.True(rt.IsDisposed);
        Assert.True(rt.ColorTexture.IsDisposed);
        Assert.True(rt.DepthTexture!.IsDisposed);

        var leaks = manager.Tracker.CheckForLeaks();
        Assert.Empty(leaks);
    }

    [Fact]
    public void PostProcessEffect_ProceduralTrianglePipelineAndApply()
    {
        using var device = new WindowsGpuDevice();

        const string InvertHlsl = @"
Texture2D g_Texture : register(t0);
SamplerState g_Sampler : register(s0);

struct PSInput
{
    float4 Position : SV_POSITION;
    float2 TexCoord : TEXCOORD0;
};

float4 PSMain(PSInput input) : SV_TARGET
{
    float4 color = g_Texture.Sample(g_Sampler, input.TexCoord);
    return float4(1.0f - color.rgb, color.a);
}
";

        using var effect = new PostProcessEffect(
            device,
            "InvertEffect",
            InvertHlsl,
            entryPoint: "PSMain",
            blendMode: BlendMode.Opaque);

        Assert.NotNull(effect.Pipeline);
        Assert.Equal("InvertEffect", effect.Name);

        using var srcTarget = new RenderTarget2D(device, "SrcTarget", 256, 256);
        using var dstTarget = new RenderTarget2D(device, "DstTarget", 256, 256);

        // Apply effect from src to dst without manual vertex buffers or vertex layouts
        var ex = Record.Exception(() => effect.Apply(srcTarget, dstTarget, Color4.Black));
        Assert.Null(ex);
    }
}
