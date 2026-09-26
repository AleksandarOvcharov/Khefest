using Khefest.Core.Resources;
using Khefest.Graphics.LowLevel;

namespace Khefest.Graphics.ThreeD;

/// <summary>
/// GPU-backed 3D polygonal mesh consisting of vertex and index buffers.
/// </summary>
public sealed class Mesh : ResourceBase
{
    private IGpuBuffer _vertexBuffer;
    private IGpuBuffer _indexBuffer;
    private readonly int _vertexCount;
    private readonly int _indexCount;
    private readonly IndexFormat _indexFormat;
    private readonly BoundingBox3D _bounds;
    private readonly bool _ownsBuffers;

    public IGpuBuffer VertexBuffer => _vertexBuffer;
    public IGpuBuffer IndexBuffer => _indexBuffer;
    public int VertexCount => _vertexCount;
    public int IndexCount => _indexCount;
    public IndexFormat IndexFormat => _indexFormat;
    public BoundingBox3D Bounds => _bounds;

    public Mesh(
        string name,
        IGpuBuffer vertexBuffer,
        IGpuBuffer indexBuffer,
        int vertexCount,
        int indexCount,
        BoundingBox3D bounds,
        IndexFormat indexFormat = IndexFormat.UInt16,
        ResourceManager? manager = null,
        bool ownsBuffers = true)
        : base(name, ResourceType.Mesh, vertexBuffer.SizeInBytes + indexBuffer.SizeInBytes, manager?.Tracker.EnableLeakTracking ?? false)
    {
        _vertexBuffer = vertexBuffer;
        _indexBuffer = indexBuffer;
        _vertexCount = vertexCount;
        _indexCount = indexCount;
        _bounds = bounds;
        _indexFormat = indexFormat;
        _ownsBuffers = ownsBuffers;

        manager?.Register(this);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && _ownsBuffers)
        {
            _vertexBuffer?.Dispose();
            _indexBuffer?.Dispose();
        }
    }
}
