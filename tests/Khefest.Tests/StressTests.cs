using System.Numerics;
using Khefest.Core.Configuration;
using Khefest.Core.Resources;
using Khefest.Graphics.Camera;
using Khefest.Graphics.Diagnostics;
using Khefest.Graphics.LowLevel;
using Khefest.Graphics.LowLevel.Windows;
using Khefest.Graphics.Mathematics;
using Khefest.Graphics.ThreeD;
using Khefest.Graphics.TwoD;

namespace Khefest.Tests;

public sealed class StressTests
{
    [Fact]
    public void SpriteBatch_StressTest_Processes10000QuadsWithoutCrashing()
    {
        var config = new MemoryConfig { EnableLeakTracking = true };
        using var resources = new ResourceManager(config);
        using var device = new WindowsGpuDevice(resources);
        using var target = device.CreateTexture("StressTarget", 1280, 720, GpuFormat.R8G8B8A8_UNorm, TextureUsage.RenderTarget);
        using var spriteBatch = new SpriteBatch(device, resources);

        var camera = new Camera2D(1280, 720);

        // Reset stats before test run
        RenderStatistics.Current.Reset();

        // Begin batch
        spriteBatch.Begin(target, camera);

        // Submit 10,000 quads - this will trigger multiple automated flushes (MaxBatchQuads = 2048)
        var rnd = new Random(42);
        for (int i = 0; i < 10_000; i++)
        {
            var pos = new Vector2(rnd.Next(0, 1280), rnd.Next(0, 720));
            var size = new Vector2(rnd.Next(2, 32), rnd.Next(2, 32));
            var color = new Color4(rnd.NextSingle(), rnd.NextSingle(), rnd.NextSingle(), 1f);
            spriteBatch.Draw(spriteBatch.WhiteTextureRegion, pos, size, color);
        }

        // End should flush any remaining quads and submit cleanly
        spriteBatch.End();

        // Validate stats were recorded
        Assert.True(RenderStatistics.Current.IndexedDrawCallCount >= 5, "Expected at least 5 flushes for 10,000 quads (10000 / 2048 = 4.88)");
        Assert.True(RenderStatistics.Current.VertexCount >= 40_000, "10,000 quads * 4 = 40,000 vertices");
    }

    [Fact]
    public void SpatialMath_FrustumCulling_StressTest_10000Objects()
    {
        var camera = new PerspectiveCamera(
            new Vector3(0, 0, -50),
            Vector3.Zero,
            16f / 9f,
            45f,
            0.1f,
            1000f);

        var frustum = new BoundingFrustum(camera.ViewMatrix * camera.ProjectionMatrix);

        var rnd = new Random(1337);
        int visibleCount = 0;
        int culledCount = 0;

        for (int i = 0; i < 10_000; i++)
        {
            var center = new Vector3(
                rnd.NextSingle() * 400f - 200f,
                rnd.NextSingle() * 400f - 200f,
                rnd.NextSingle() * 600f - 100f
            );
            var box = new BoundingBox(center - new Vector3(1f, 1f, 1f), center + new Vector3(1f, 1f, 1f));

            if (frustum.Intersects(box))
            {
                visibleCount++;
            }
            else
            {
                culledCount++;
            }
        }

        Assert.True(visibleCount > 0, "Some objects should be in frustum");
        Assert.True(culledCount > 0, "Some objects should be culled");
        Assert.Equal(10_000, visibleCount + culledCount);
    }

    [Fact]
    public void ResourceLifetimeTracker_StressTest_MassiveChurnNoLeaks()
    {
        var config = new MemoryConfig { EnableLeakTracking = true };
        using var resources = new ResourceManager(config);
        using var device = new WindowsGpuDevice(resources);

        // Allocate and dispose 300 buffers in rapid churn
        for (int i = 0; i < 300; i++)
        {
            var buffer = device.CreateBuffer($"ChurnBuffer_{i}", 1024, BufferUsage.Vertex);
            buffer.SetData<byte>(new byte[1024]);
            buffer.Dispose();
        }

        // Allocate and dispose 100 textures in rapid churn
        for (int i = 0; i < 100; i++)
        {
            var tex = device.CreateTexture($"ChurnTex_{i}", 64, 64, GpuFormat.R8G8B8A8_UNorm, TextureUsage.Sampled);
            tex.Dispose();
        }

        // Validate 0 leaks remain
        Assert.False(resources.Tracker.HasLeaks);
        Assert.Empty(resources.Tracker.CheckForLeaks());
    }

    [Fact]
    public void ConcurrentResourceCache_StressTest_MultiThreadedAccess()
    {
        using var cache = new ResourceCache<string, MemoryBufferResource>();

        Parallel.For(0, 50, threadId =>
        {
            for (int i = 0; i < 100; i++)
            {
                var key = $"Key_{threadId}_{i % 10}";
                var resource = cache.GetOrCreate(key, k => new MemoryBufferResource(k, 256));
                Assert.NotNull(resource);
            }
        });

        Assert.True(cache.Count > 0);
    }

    [Fact]
    public void MeshRenderer_StressTest_MultipleMeshesWithDepthBuffer()
    {
        var config = new MemoryConfig { EnableLeakTracking = true };
        using var resources = new ResourceManager(config);
        using var device = new WindowsGpuDevice(resources);

        using var colorTarget = device.CreateTexture("ColorTarget", 800, 600, GpuFormat.R8G8B8A8_UNorm, TextureUsage.RenderTarget);
        using var depthTarget = device.CreateTexture("DepthTarget", 800, 600, GpuFormat.D24_UNorm_S8_UInt, TextureUsage.DepthStencil);

        using var cubeMesh = MeshPrimitives.CreateCube(device, 1.0f, resources);
        using var sphereMesh = MeshPrimitives.CreateSphere(device, 0.5f, 24, 16, resources);
        using var renderer = new MeshRenderer(device, resources);

        var camera = new PerspectiveCamera(
            new Vector3(0, 5, -10),
            Vector3.Zero,
            800f / 600f,
            45f,
            0.1f,
            100f);

        RenderStatistics.Current.Reset();

        // Render 50 cubes and 50 spheres at varying depths
        renderer.Begin(colorTarget, depthTarget, camera);

        var rnd = new Random(77);
        for (int i = 0; i < 50; i++)
        {
            var world = Matrix4x4.CreateTranslation(rnd.NextSingle() * 10f - 5f, 0, rnd.NextSingle() * 20f);
            renderer.Draw(cubeMesh, null, world);
        }

        for (int i = 0; i < 50; i++)
        {
            var world = Matrix4x4.CreateTranslation(rnd.NextSingle() * 10f - 5f, 1, rnd.NextSingle() * 20f);
            renderer.Draw(sphereMesh, null, world);
        }

        renderer.End();

        Assert.True(RenderStatistics.Current.IndexedDrawCallCount >= 100);
    }
}
