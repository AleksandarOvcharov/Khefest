namespace Khefest.Graphics.Diagnostics;

/// <summary>
/// Collects and aggregates low-level and high-level graphics rendering statistics per frame.
/// </summary>
public sealed class RenderStatistics
{
    /// <summary>
    /// Global singleton instance collecting metrics for the current active frame.
    /// </summary>
    public static RenderStatistics Current { get; } = new();

    public long DrawCallCount { get; private set; }
    public long IndexedDrawCallCount { get; private set; }
    public long TotalDrawCalls => DrawCallCount + IndexedDrawCallCount;
    public long VertexCount { get; private set; }
    public long IndexCount { get; private set; }
    public long TriangleCount { get; private set; }
    public long PipelineSwitches { get; private set; }
    public long ShaderSwitches { get; private set; }
    public long TextureBinds { get; private set; }
    public long ConstantBufferUploads { get; private set; }
    public long RenderPassCount { get; private set; }

    public void RecordDraw(int vertexCount, int triangleCount = 0)
    {
        DrawCallCount++;
        VertexCount += vertexCount;
        TriangleCount += triangleCount > 0 ? triangleCount : (vertexCount / 3);
    }

    public void RecordDrawIndexed(int indexCount, int vertexCount = 0)
    {
        IndexedDrawCallCount++;
        IndexCount += indexCount;
        VertexCount += vertexCount;
        TriangleCount += (indexCount / 3);
    }

    public void RecordPipelineSwitch()
    {
        PipelineSwitches++;
    }

    public void RecordShaderSwitch()
    {
        ShaderSwitches++;
    }

    public void RecordTextureBind()
    {
        TextureBinds++;
    }

    public void RecordConstantBufferUpload()
    {
        ConstantBufferUploads++;
    }

    public void RecordRenderPass()
    {
        RenderPassCount++;
    }

    public void Reset()
    {
        DrawCallCount = 0;
        IndexedDrawCallCount = 0;
        VertexCount = 0;
        IndexCount = 0;
        TriangleCount = 0;
        PipelineSwitches = 0;
        ShaderSwitches = 0;
        TextureBinds = 0;
        ConstantBufferUploads = 0;
        RenderPassCount = 0;
    }

    /// <summary>
    /// Creates a cloned snapshot of the current frame statistics.
    /// </summary>
    public RenderStatistics Clone()
    {
        return new RenderStatistics
        {
            DrawCallCount = this.DrawCallCount,
            IndexedDrawCallCount = this.IndexedDrawCallCount,
            VertexCount = this.VertexCount,
            IndexCount = this.IndexCount,
            TriangleCount = this.TriangleCount,
            PipelineSwitches = this.PipelineSwitches,
            ShaderSwitches = this.ShaderSwitches,
            TextureBinds = this.TextureBinds,
            ConstantBufferUploads = this.ConstantBufferUploads,
            RenderPassCount = this.RenderPassCount
        };
    }
}
