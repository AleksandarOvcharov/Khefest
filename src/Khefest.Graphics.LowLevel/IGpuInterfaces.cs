using Khefest.Core.Resources;
using Khefest.Windowing;

namespace Khefest.Graphics.LowLevel;

/// <summary>
/// Root interface for all low-level GPU objects (buffers, textures, shaders, pipelines).
/// </summary>
public interface IGpuResource : IResource
{
}

/// <summary>
/// GPU memory buffer for vertices, indices, uniform/constant data, and compute buffers.
/// </summary>
public interface IGpuBuffer : IGpuResource
{
    BufferUsage Usage { get; }
    void SetData<T>(ReadOnlySpan<T> data, int offsetInBytes = 0) where T : unmanaged;
    void SetData<T>(in T value, int offsetInBytes = 0) where T : unmanaged;
}

/// <summary>
/// GPU texture resource for 2D/3D textures and render target backbuffers.
/// </summary>
public interface IGpuTexture : IGpuResource
{
    int Width { get; }
    int Height { get; }
    GpuFormat Format { get; }
    TextureUsage Usage { get; }

    /// <summary>
    /// Gets the canonical GPU texture represented by this resource.
    /// Returns itself by default for primary hardware textures.
    /// </summary>
    IGpuTexture CanonicalTexture => this;

    void SetData<T>(ReadOnlySpan<T> data, int subresource = 0, int rowPitchInBytes = 0) where T : unmanaged;
}

/// <summary>
/// Compiled GPU shader module.
/// </summary>
public interface IGpuShader : IGpuResource
{
    ShaderStage Stage { get; }
    string EntryPoint { get; }
    ReadOnlySpan<byte> Bytecode { get; }
}

/// <summary>
/// Unified pipeline state object combining shaders, vertex layout, rasterizer, blend, and depth states.
/// </summary>
public interface IPipeline : IGpuResource
{
    PipelineDesc Description { get; }
}

/// <summary>
/// State-machine command list builder for encoding GPU rendering operations.
/// </summary>
public interface ICommandRecorder : IDisposable
{
    bool IsInPass { get; }
    void BeginPass(in RenderPassDesc pass);
    ScopedRenderPass BeginScopedPass(in RenderPassDesc pass);
    void SetPipeline(IPipeline pipeline);
    void SetVertexBuffer(int slot, IGpuBuffer buffer);
    void SetIndexBuffer(IGpuBuffer buffer, IndexFormat format);
    void SetUniformBuffer(int slot, IGpuBuffer buffer);
    void SetTexture(int slot, IGpuTexture texture);
    void SetViewport(in Viewport viewport);
    void SetScissor(in ScissorRect scissor);
    void Draw(int vertexCount, int firstVertex = 0);
    void DrawIndexed(int indexCount, int firstIndex = 0, int vertexOffset = 0);
    void EndPass();
}

/// <summary>
/// Presentation coordinator binding rendered surfaces to the window display.
/// </summary>
public interface ISwapChain : IDisposable
{
    IGpuTexture CurrentBackBuffer { get; }
    int Width { get; }
    int Height { get; }
    void Resize(int width, int height);
    void Present(bool vsync);
}

/// <summary>
/// Primary hardware abstraction representing the physical GPU and execution context.
/// </summary>
public interface IGpuDevice : IDisposable
{
    string AdapterName { get; }
    long DedicatedVideoMemory { get; }

    IGpuBuffer CreateBuffer(string name, long sizeInBytes, BufferUsage usage);
    IGpuTexture CreateTexture(string name, int width, int height, GpuFormat format, TextureUsage usage);
    IGpuShader CreateShader(string name, ShaderStage stage, ReadOnlySpan<byte> bytecode, string entryPoint = "main");
    IGpuShader CompileShader(string name, ShaderStage stage, string sourceCode, string entryPoint = "main");
    IPipeline CreatePipeline(string name, in PipelineDesc desc);
    ISwapChain CreateSwapChain(IWindow window, int width, int height, bool vsync);
    ICommandRecorder CreateCommandRecorder();

    void Submit(ICommandRecorder recorder);
    void WaitForGpu();
}
