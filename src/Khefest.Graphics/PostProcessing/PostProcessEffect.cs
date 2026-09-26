using System.Runtime.InteropServices;
using Khefest.Core.Resources;
using Khefest.Graphics.LowLevel;
using Khefest.Graphics.Texturing;

namespace Khefest.Graphics.PostProcessing;

/// <summary>
/// High-level post-processing effect that transforms an input texture to a destination target
/// using a custom HLSL pixel shader and a zero-allocation procedural fullscreen triangle.
/// Eliminates the need for manual vertex buffers, input layouts, and pipeline plumbing.
/// </summary>
public sealed class PostProcessEffect : ResourceBase
{
    private readonly IGpuDevice _device;
    private readonly IGpuShader _vertexShader;
    private readonly IGpuShader _pixelShader;
    private readonly IPipeline _pipeline;
    private IGpuBuffer? _uniformBuffer;
    private int _uniformBufferSize;

    public IGpuDevice Device => _device;
    public IPipeline Pipeline => _pipeline;
    public IGpuBuffer? UniformBuffer => _uniformBuffer;

    private const string ProceduralVsSource = @"
struct VSOutput
{
    float4 Position : SV_POSITION;
    float2 TexCoord : TEXCOORD0;
};

VSOutput VSMain(uint vertexId : SV_VertexID)
{
    VSOutput output;
    float2 tex = float2((vertexId << 1) & 2, vertexId & 2);
    output.Position = float4(tex * float2(2.0f, -2.0f) + float2(-1.0f, 1.0f), 0.0f, 1.0f);
    output.TexCoord = tex;
    return output;
}
";

    public PostProcessEffect(
        IGpuDevice device,
        string name,
        string pixelShaderSource,
        string entryPoint = "PSMain",
        BlendMode blendMode = BlendMode.Opaque,
        ResourceManager? manager = null)
        : base(name, ResourceType.Shader, 0, manager?.Tracker.EnableLeakTracking ?? false)
    {
        _device = device ?? throw new ArgumentNullException(nameof(device));
        ArgumentException.ThrowIfNullOrWhiteSpace(pixelShaderSource);

        _vertexShader = _device.CompileShader($"{name}_VS", ShaderStage.Vertex, ProceduralVsSource, "VSMain");
        _pixelShader = _device.CompileShader($"{name}_PS", ShaderStage.Pixel, pixelShaderSource, entryPoint);

        var pipelineDesc = new PipelineDesc
        {
            VertexShader = _vertexShader,
            PixelShader = _pixelShader,
            VertexLayout = null, // Zero-VB procedural fullscreen triangle
            Topology = PrimitiveTopology.TriangleList,
            CullMode = CullMode.None,
            FillMode = FillMode.Solid,
            BlendMode = blendMode,
            DepthTestEnabled = false,
            DepthWriteEnabled = false
        };

        _pipeline = _device.CreatePipeline($"{name}_Pipeline", pipelineDesc);
        manager?.Register(this);
    }

    /// <summary>
    /// Updates the uniform constant buffer with a single unmanaged struct value with zero heap allocations.
    /// Reallocates the internal buffer if the struct size changes.
    /// </summary>
    public void SetUniforms<T>(in T value) where T : unmanaged
    {
        ThrowIfDisposed();
        int size = Marshal.SizeOf<T>();

        if (_uniformBuffer == null || _uniformBufferSize != size)
        {
            _uniformBuffer?.Dispose();
            _uniformBufferSize = size;
            _uniformBuffer = _device.CreateBuffer($"{Name}_Uniforms", size, BufferUsage.Uniform);
        }

        _uniformBuffer.SetData(in value);
    }

    /// <summary>
    /// Applies the post-processing effect from a source texture into a destination target in a dedicated render pass.
    /// Automatically manages the command recorder and pass execution.
    /// </summary>
    public void Apply(IGpuTexture source, IGpuTexture destination, Color4? clearColor = null)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);

        var actualSource = source is RenderTarget2D rtSrc ? rtSrc.ColorTexture : source;
        var actualDest = destination is RenderTarget2D rtDst ? rtDst.ColorTexture : destination;

        using var recorder = _device.CreateCommandRecorder();
        var pass = new RenderPassDesc
        {
            ColorTarget = actualDest,
            ClearColorTarget = clearColor.HasValue,
            ClearColor = clearColor ?? Color4.Black
        };

        using (recorder.BeginScopedPass(pass))
        {
            Draw(recorder, actualSource);
        }

        _device.Submit(recorder);
    }

    /// <summary>
    /// Draws the procedural fullscreen triangle within an already active render pass on the given command recorder.
    /// </summary>
    public void Draw(ICommandRecorder recorder, IGpuTexture source)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(recorder);
        ArgumentNullException.ThrowIfNull(source);

        var actualSource = source is RenderTarget2D rtSrc ? rtSrc.ColorTexture : source;

        recorder.SetPipeline(_pipeline);
        recorder.SetTexture(0, actualSource);

        if (_uniformBuffer != null)
        {
            recorder.SetUniformBuffer(0, _uniformBuffer);
        }

        recorder.Draw(3, 0);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _uniformBuffer?.Dispose();
            _pipeline.Dispose();
            _pixelShader.Dispose();
            _vertexShader.Dispose();
        }
    }
}
