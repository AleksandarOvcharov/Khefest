using System.Numerics;
using Khefest.Core.Configuration;
using Khefest.Core.Errors;
using Khefest.Core.Profiling;
using Khefest.Core.Resources;
using Khefest.Graphics.Diagnostics;
using Khefest.Graphics.Diagnostics.Validation;
using Khefest.Graphics.LowLevel;
using Khefest.Graphics.LowLevel.Windows;
using Xunit;

namespace Khefest.Tests;

public sealed class Phase8DebuggingTests
{
    private sealed class MockResource : ResourceBase
    {
        public MockResource(string name, long size)
            : base(name, ResourceType.Buffer, size, captureStackTrace: true)
        {
        }

        protected override void Dispose(bool disposing) { }
    }

    [Fact]
    public void Profiler_HierarchicalScopes_RecordsTimingsAndCallCounts()
    {
        Profiler.Reset();
        Profiler.IsEnabled = true;

        Profiler.BeginFrame();

        using (Profiler.Scope("RootUpdate"))
        {
            Thread.Sleep(5);
            using (Profiler.Scope("Physics"))
            {
                Thread.Sleep(5);
            }
        }

        using (Profiler.Scope("Render"))
        {
            Thread.Sleep(2);
        }

        var frame = Profiler.EndFrame();
        Assert.NotNull(frame);
        Assert.True(frame.TotalFrameTimeMilliseconds >= 10.0);
        Assert.Equal(2, frame.RootSamples.Count);

        var updateSample = frame.RootSamples.First(s => s.Name == "RootUpdate");
        Assert.Equal(1, updateSample.CallCount);
        Assert.Single(updateSample.Children);

        var physicsSample = updateSample.Children[0];
        Assert.Equal("Physics", physicsSample.Name);
        Assert.Equal(1, physicsSample.CallCount);

        var renderSample = frame.RootSamples.First(s => s.Name == "Render");
        Assert.Equal(1, renderSample.CallCount);

        var (avg, min, max) = Profiler.GetFrameStatistics();
        Assert.True(avg > 0);
        Assert.True(min > 0);
        Assert.True(max >= min);
    }

    [Fact]
    public void RenderStatistics_AccumulatesMetricsAndResets()
    {
        var stats = new RenderStatistics();

        stats.RecordDraw(vertexCount: 300, triangleCount: 100);
        stats.RecordDrawIndexed(indexCount: 36, vertexCount: 24);
        stats.RecordPipelineSwitch();
        stats.RecordPipelineSwitch();
        stats.RecordShaderSwitch();
        stats.RecordTextureBind();
        stats.RecordConstantBufferUpload();
        stats.RecordRenderPass();

        Assert.Equal(1, stats.DrawCallCount);
        Assert.Equal(1, stats.IndexedDrawCallCount);
        Assert.Equal(2, stats.TotalDrawCalls);
        Assert.Equal(324, stats.VertexCount);
        Assert.Equal(36, stats.IndexCount);
        Assert.Equal(112, stats.TriangleCount); // 100 + 36/3
        Assert.Equal(2, stats.PipelineSwitches);
        Assert.Equal(1, stats.ShaderSwitches);
        Assert.Equal(1, stats.TextureBinds);
        Assert.Equal(1, stats.ConstantBufferUploads);
        Assert.Equal(1, stats.RenderPassCount);

        var cloned = stats.Clone();
        Assert.Equal(stats.TotalDrawCalls, cloned.TotalDrawCalls);

        stats.Reset();
        Assert.Equal(0, stats.TotalDrawCalls);
        Assert.Equal(0, stats.VertexCount);
        Assert.Equal(0, stats.TriangleCount);
    }

    [Fact]
    public void FrameStatistics_CalculatesFpsAndTracksMemory()
    {
        var stats = new FrameStatistics();

        // Simulate 60 FPS (16.66 ms per frame) over 35 frames (exceeds 0.5s accumulator)
        for (int i = 0; i < 35; i++)
        {
            stats.Update(0.016666);
        }

        Assert.Equal(35, stats.FrameNumber);
        Assert.InRange(stats.FramesPerSecond, 55.0, 65.0);
        Assert.True(stats.FrameTimeMilliseconds > 15.0);
        var mem = stats.ManagedMemoryBytes;
        Assert.True(stats.ManagedMemoryBytes > 0, $"Expected ManagedMemoryBytes > 0, but was {mem}. FrameTimeMs was {stats.FrameTimeMilliseconds}");
    }

    [Fact]
    public void GpuValidator_DetectsContractViolationsInStrictMode()
    {
        var validator = new GpuValidator(ValidationMode.Strict);

        // 1. Invalid vertex count
        var ex1 = Assert.Throws<KhefestGraphicsException>(() =>
            validator.ValidateDraw(0, 0, null, null));
        Assert.Equal(KhefestErrorCode.InvalidOperation, ex1.ErrorCode);

        // 2. Negative start location
        var ex2 = Assert.Throws<KhefestGraphicsException>(() =>
            validator.ValidateDraw(10, -1, null, null));
        Assert.Equal(KhefestErrorCode.InvalidOperation, ex2.ErrorCode);

        // 3. Null buffer upload
        var ex3 = Assert.Throws<KhefestGraphicsException>(() =>
            validator.ValidateBufferUpload(null!, 0, 100));
        Assert.Equal(KhefestErrorCode.InvalidOperation, ex3.ErrorCode);

        // 4. In Disabled mode, no exception is thrown
        validator.Mode = ValidationMode.Disabled;
        validator.ValidateDraw(0, -5, null, null); // Does not throw
    }

    [Fact]
    public void GpuValidator_ValidatesBufferCapacityAndAttachments()
    {
        using var device = new WindowsGpuDevice();
        using var buffer = device.CreateBuffer("TestBuffer", 256, BufferUsage.Vertex);

        var validator = new GpuValidator(ValidationMode.Strict);

        // Valid upload range
        validator.ValidateBufferUpload(buffer, 0, 256);
        validator.ValidateBufferUpload(buffer, 100, 156);

        // Exceeding capacity
        Assert.Throws<KhefestGraphicsException>(() =>
            validator.ValidateBufferUpload(buffer, 10, 250));

        // Mismatched render target dimensions
        using var colorTarget = device.CreateTexture("Color512", 512, 512, GpuFormat.R8G8B8A8_UNorm, TextureUsage.RenderTarget);
        using var depthTarget = device.CreateTexture("Depth256", 256, 256, GpuFormat.D24_UNorm_S8_UInt, TextureUsage.DepthStencil);

        Assert.Throws<KhefestGraphicsException>(() =>
            validator.ValidateRenderTargetAttachment(colorTarget, depthTarget));
    }

    [Fact]
    public void ResourceLifetimeTracker_DetectsLeaksAndGeneratesReport()
    {
        var tracker = new ResourceLifetimeTracker(enableLeakTracking: true);
        var res = new MockResource("LeakedVertexBuffer", 2048);

        tracker.Track(res);
        var leaks = tracker.CheckForLeaks();
        Assert.Single(leaks);
        Assert.Equal("LeakedVertexBuffer", leaks[0].Name);

        string report = tracker.GenerateLeakReport();
        Assert.Contains("KHEFEST RESOURCE LEAK REPORT (1 Leaks Detected)", report);
        Assert.Contains("LeakedVertexBuffer", report);
        Assert.Contains("2048 bytes", report);

        // EnforceNoLeaks throws
        Assert.Throws<KhefestResourceException>(() => tracker.EnforceNoLeaks());

        // Disposing cleans up
        res.Dispose();
        Assert.Empty(tracker.CheckForLeaks());
        Assert.Contains("No resource leaks detected", tracker.GenerateLeakReport());
    }

    [Fact]
    public void DebugOverlay_FormatsComprehensiveDiagnosticReport()
    {
        var frameStats = new FrameStatistics();
        frameStats.Update(0.016);

        var renderStats = new RenderStatistics();
        renderStats.RecordDraw(600, 200);
        renderStats.RecordDrawIndexed(72, 48);

        using var resManager = new ResourceManager(new MemoryConfig { AutoMemManagement = true });

        var overlay = new DebugOverlay(frameStats, renderStats, resManager);
        string report = overlay.GetFormattedReport();

        Assert.Contains("KHEFEST ENGINE DIAGNOSTICS", report);
        Assert.Contains("Performance:", report);
        Assert.Contains("Render Stats: 2 Draw Calls", report);
        Assert.Contains("Geometry:", report);
        Assert.Contains("Resources: 0 Active", report);
        Assert.Contains("GC Memory:", report);
    }
}

