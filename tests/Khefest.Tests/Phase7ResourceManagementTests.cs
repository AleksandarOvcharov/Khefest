using System.Numerics;
using System.Runtime.InteropServices;
using Khefest.Core.Assets;
using Khefest.Core.Configuration;
using Khefest.Core.Resources;
using Khefest.Graphics.Assets;
using Khefest.Graphics.LowLevel;
using Khefest.Graphics.LowLevel.Windows;
using Khefest.Graphics.Memory;
using Khefest.Graphics.Texturing;
using Xunit;

namespace Khefest.Tests;

public sealed class Phase7ResourceManagementTests
{
    private sealed class DummyResource : ResourceBase
    {
        public bool DisposeCalled { get; private set; }

        public DummyResource(string name, long size)
            : base(name, ResourceType.Custom, size, false)
        {
        }

        protected override void Dispose(bool disposing)
        {
            DisposeCalled = true;
        }
    }

    [Fact]
    public void ResourceCache_StoresAndRetrieves_WithLRUEviction()
    {
        using var cache = new ResourceCache<string, DummyResource>(maxBudgetBytes: 250);

        var res1 = new DummyResource("Res1", 100);
        var res2 = new DummyResource("Res2", 100);
        var res3 = new DummyResource("Res3", 100);

        cache.Add("item1", res1);
        cache.Add("item2", res2);

        Assert.Equal(2, cache.Count);
        Assert.Equal(200, cache.TotalAllocatedBytes);

        Assert.True(cache.TryGet("item1", out var touched));
        Assert.Same(res1, touched);

        cache.Add("item3", res3);

        Assert.True(res2.DisposeCalled, "Res2 should have been evicted and disposed.");
        Assert.False(cache.TryGet("item2", out _));
        Assert.True(cache.TryGet("item1", out _));
        Assert.True(cache.TryGet("item3", out _));
        Assert.Equal(200, cache.TotalAllocatedBytes);
    }

    [Fact]
    public void SharedResource_ReferenceCounting_DisposesOnlyOnZeroRefs()
    {
        var dummy = new DummyResource("SharedDummy", 64);
        var shared1 = new SharedResource<DummyResource>(dummy);

        Assert.Equal(1, shared1.ReferenceCount);
        Assert.False(dummy.DisposeCalled);

        var shared2 = shared1.Acquire();
        var shared3 = shared2.Acquire();
        Assert.Equal(3, shared1.ReferenceCount);

        shared1.Dispose();
        Assert.Equal(2, shared2.ReferenceCount);
        Assert.False(dummy.DisposeCalled);

        shared2.Dispose();
        Assert.Equal(1, shared3.ReferenceCount);
        Assert.False(dummy.DisposeCalled);

        shared3.Dispose();
        Assert.True(dummy.DisposeCalled, "Underlying resource should be disposed when last ref drops to 0.");
    }

    [Fact]
    public void ResourceScope_DisposesAllTrackedResourcesOnExit()
    {
        DummyResource r1;
        DummyResource r2;

        using (var scope = new ResourceScope())
        {
            r1 = scope.Track(new DummyResource("ScopeRes1", 50));
            r2 = scope.Track(new DummyResource("ScopeRes2", 50));

            Assert.False(r1.DisposeCalled);
            Assert.False(r2.DisposeCalled);
        }

        Assert.True(r1.DisposeCalled);
        Assert.True(r2.DisposeCalled);
    }

    [Fact]
    public void GpuBufferPool_RentsRecyclesAndFlushesBuffers()
    {
        using var device = new WindowsGpuDevice();
        using var pool = new GpuBufferPool(device);

        IGpuBuffer leasedBufferRef;

        using (var lease = pool.Rent(256, BufferUsage.Uniform))
        {
            Assert.NotNull(lease.Buffer);
            Assert.True(lease.Buffer.SizeInBytes >= 256);
            Assert.Equal(1, pool.ActiveLeaseCount);
            leasedBufferRef = lease.Buffer;
        }

        Assert.Equal(0, pool.ActiveLeaseCount);
        Assert.False(leasedBufferRef.IsDisposed);

        using (var lease2 = pool.Rent(256, BufferUsage.Uniform))
        {
            Assert.Same(leasedBufferRef, lease2.Buffer);
            Assert.Equal(1, pool.ActiveLeaseCount);
        }

        pool.Flush();
        Assert.True(leasedBufferRef.IsDisposed, "Flushing pool must dispose idle buffers.");
    }

    [Fact]
    public void RenderTargetPool_RentsRecyclesScratchTextures()
    {
        using var device = new WindowsGpuDevice();
        using var pool = new RenderTargetPool(device);

        IGpuTexture leasedTexRef;

        using (var lease = pool.RentRenderTarget(512, 512, GpuFormat.R8G8B8A8_UNorm))
        {
            Assert.NotNull(lease.Texture);
            Assert.Equal(512, lease.Texture.Width);
            Assert.Equal(512, lease.Texture.Height);
            Assert.Equal(1, pool.ActiveLeaseCount);
            leasedTexRef = lease.Texture;
        }

        Assert.Equal(0, pool.ActiveLeaseCount);
        Assert.False(leasedTexRef.IsDisposed);

        using (var lease2 = pool.RentRenderTarget(512, 512, GpuFormat.R8G8B8A8_UNorm))
        {
            Assert.Same(leasedTexRef, lease2.Texture);
        }

        pool.Flush();
        Assert.True(leasedTexRef.IsDisposed);
    }

    [Fact]
    public async Task AssetManager_LoadsAndCachesSynchronouslyAndAsynchronously()
    {
        using var device = new WindowsGpuDevice();
        using var assets = new AssetManager();
        assets.RegisterLoader(new Texture2DAssetLoader(device));

        // Create a 2x2 24-bit BMP image on disk
        string tempBmp = Path.Combine(Path.GetTempPath(), $"khefest_test_{Guid.NewGuid():N}.bmp");
        try
        {
            byte[] bmpBytes = CreateMinimalBmpBytes(2, 2);
            await File.WriteAllBytesAsync(tempBmp, bmpBytes);

            // 1. Sync Load
            var tex1 = assets.Load<Texture2D>(tempBmp);
            Assert.NotNull(tex1);
            Assert.Equal(2, tex1.Width);
            Assert.Equal(2, tex1.Height);

            // 2. Load again -> returns cached instance!
            var tex2 = assets.Load<Texture2D>(tempBmp);
            Assert.Same(tex1, tex2);

            // 3. Async Load of same asset -> returns cached instance!
            var texAsync = await assets.LoadAsync<Texture2D>(tempBmp);
            Assert.Same(tex1, texAsync);

            // 4. Unload
            bool unloaded = assets.Unload(tempBmp);
            Assert.True(unloaded);
            Assert.True(tex1.IsDisposed);
        }
        finally
        {
            if (File.Exists(tempBmp))
            {
                File.Delete(tempBmp);
            }
        }
    }

    private static byte[] CreateMinimalBmpBytes(int width, int height)
    {
        int rowStride = ((width * 3 + 3) / 4) * 4;
        int pixelDataSize = rowStride * height;
        int fileSize = 54 + pixelDataSize;

        var data = new byte[fileSize];
        // BMP header
        data[0] = (byte)'B';
        data[1] = (byte)'M';
        BitConverter.GetBytes(fileSize).CopyTo(data, 2);
        BitConverter.GetBytes(54).CopyTo(data, 10);

        // DIB header (BITMAPINFOHEADER)
        BitConverter.GetBytes(40).CopyTo(data, 14);
        BitConverter.GetBytes(width).CopyTo(data, 18);
        BitConverter.GetBytes(height).CopyTo(data, 22);
        BitConverter.GetBytes((ushort)1).CopyTo(data, 26);
        BitConverter.GetBytes((ushort)24).CopyTo(data, 28);
        BitConverter.GetBytes(pixelDataSize).CopyTo(data, 34);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int offset = 54 + y * rowStride + x * 3;
                data[offset + 0] = 0x00; // B
                data[offset + 1] = 0xFF; // G
                data[offset + 2] = 0xFF; // R
            }
        }

        return data;
    }
}
