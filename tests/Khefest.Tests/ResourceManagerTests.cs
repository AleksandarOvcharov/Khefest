using Khefest.Core.Configuration;
using Khefest.Core.Errors;
using Khefest.Core.Resources;
using Xunit;

namespace Khefest.Tests;

public class ResourceManagerTests
{
    [Fact]
    public void ResourceRegistration_ResolvesTypedHandleAndTracksStatistics()
    {
        using var manager = new ResourceManager(new MemoryConfig { AutoMemManagement = true });

        var buffer = new MemoryBufferResource("VertexBuffer", 1024);
        var handle = manager.Register(buffer);

        Assert.True(handle.IsValid);
        var resolved = manager.Get(handle);
        Assert.Same(buffer, resolved);

        var stats = manager.GetStatistics();
        Assert.Equal(1, stats.TotalResourceCount);
        Assert.Equal(1024, stats.TotalAllocatedBytes);
        Assert.Equal(1, stats.CountsByType[ResourceType.Buffer]);
        Assert.Equal(1024, stats.BytesByType[ResourceType.Buffer]);

        // Release resource
        var released = manager.Release(handle);
        Assert.True(released);
        Assert.True(buffer.IsDisposed);

        var updatedStats = manager.GetStatistics();
        Assert.Equal(0, updatedStats.TotalResourceCount);
        Assert.Equal(0, updatedStats.TotalAllocatedBytes);
    }

    [Fact]
    public void AccessingDisposedResource_ThrowsAlreadyDisposedException()
    {
        using var manager = new ResourceManager(new MemoryConfig { AutoMemManagement = true });

        var buffer = new MemoryBufferResource("IndexBuffer", 512);
        var handle = manager.Register(buffer);

        manager.Release(handle);

        var ex = Assert.Throws<KhefestResourceException>(() => manager.Get(handle));
        Assert.Equal(KhefestErrorCode.ResourceNotFound, ex.ErrorCode);

        // Accessing buffer directly
        var directEx = Assert.Throws<KhefestResourceException>(() => _ = buffer.NativePointer);
        Assert.Equal(KhefestErrorCode.ResourceAlreadyDisposed, directEx.ErrorCode);
    }

    [Fact]
    public void AutoMemManagement_DisposesAllResourcesOnManagerShutdown()
    {
        var manager = new ResourceManager(new MemoryConfig { AutoMemManagement = true });
        var buffer1 = new MemoryBufferResource("Buffer1", 256);
        var buffer2 = new MemoryBufferResource("Buffer2", 512);

        manager.Register(buffer1);
        manager.Register(buffer2);

        Assert.False(buffer1.IsDisposed);
        Assert.False(buffer2.IsDisposed);

        manager.Dispose();

        Assert.True(buffer1.IsDisposed);
        Assert.True(buffer2.IsDisposed);
    }

    [Fact]
    public void ManualMode_ThrowsLeakExceptionWhenResourcesAreAbandoned()
    {
        var manager = new ResourceManager(new MemoryConfig
        {
            AutoMemManagement = false,
            EnableLeakTracking = true
        });

        var leakedBuffer = new MemoryBufferResource("LeakedResource", 1024, captureStackTrace: true);
        manager.Register(leakedBuffer);

        // Attempting to dispose manager while resource remains unreleased
        var ex = Assert.Throws<KhefestResourceException>(() => manager.Dispose());
        Assert.Equal(KhefestErrorCode.ResourceLeakDetected, ex.ErrorCode);
        Assert.Contains("LeakedResource", ex.Message);

        // Clean up to prevent native leak
        leakedBuffer.Dispose();
    }
}
