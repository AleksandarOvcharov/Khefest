namespace Khefest.Graphics.LowLevel;

public readonly record struct Color4(float R, float G, float B, float A = 1.0f)
{
    public static readonly Color4 Transparent = new(0f, 0f, 0f, 0f);
    public static readonly Color4 Black = new(0f, 0f, 0f, 1f);
    public static readonly Color4 White = new(1f, 1f, 1f, 1f);
    public static readonly Color4 CornflowerBlue = new(0.392f, 0.584f, 0.929f, 1.0f);
    public static readonly Color4 Red = new(1f, 0f, 0f, 1f);
    public static readonly Color4 Green = new(0f, 1f, 0f, 1f);
    public static readonly Color4 Blue = new(0f, 0f, 1f, 1f);
    public static readonly Color4 Yellow = new(1f, 1f, 0f, 1f);
    public static readonly Color4 Cyan = new(0f, 1f, 1f, 1f);
    public static readonly Color4 Magenta = new(1f, 0f, 1f, 1f);
    public static readonly Color4 Orange = new(1f, 0.647f, 0f, 1f);
    public static readonly Color4 Purple = new(0.5f, 0f, 0.5f, 1f);
    public static readonly Color4 Gray = new(0.5f, 0.5f, 0.5f, 1f);
    public static readonly Color4 DarkGray = new(0.2f, 0.2f, 0.2f, 1f);
    public static readonly Color4 LightGray = new(0.8f, 0.8f, 0.8f, 1f);

    // Extended common game palette
    public static readonly Color4 LimeGreen = new(0.196f, 0.804f, 0.196f, 1.0f);
    public static readonly Color4 Gold = new(1.0f, 0.843f, 0.0f, 1.0f);
    public static readonly Color4 SlateGray = new(0.439f, 0.502f, 0.565f, 1.0f);
    public static readonly Color4 LightSkyBlue = new(0.529f, 0.808f, 0.980f, 1.0f);
    public static readonly Color4 RoyalBlue = new(0.255f, 0.412f, 0.882f, 1.0f);
    public static readonly Color4 Crimson = new(0.863f, 0.078f, 0.235f, 1.0f);
    public static readonly Color4 DeepSkyBlue = new(0.0f, 0.749f, 1.0f, 1.0f);
    public static readonly Color4 Teal = new(0.0f, 0.502f, 0.502f, 1.0f);
}

public readonly record struct Viewport(
    float X,
    float Y,
    float Width,
    float Height,
    float MinDepth = 0.0f,
    float MaxDepth = 1.0f);

public readonly record struct ScissorRect(int X, int Y, int Width, int Height);

public sealed record VertexElement(
    string SemanticName,
    int SemanticIndex,
    GpuFormat Format,
    int Offset)
{
    public VertexElement(string semanticName, GpuFormat format, int offset, int semanticIndex = 0)
        : this(semanticName, semanticIndex, format, offset)
    {
    }
}

public sealed record VertexLayout(
    IReadOnlyList<VertexElement> Elements,
    int Stride)
{
    public VertexLayout(int stride, params VertexElement[] elements) : this(elements, stride)
    {
    }

    public VertexLayout(params VertexElement[] elements) : this(elements, CalculateStride(elements))
    {
    }

    private static int CalculateStride(IReadOnlyList<VertexElement> elements)
    {
        if (elements == null || elements.Count == 0) return 0;
        int max = 0;
        for (int i = 0; i < elements.Count; i++)
        {
            var el = elements[i];
            int size = GetFormatSize(el.Format);
            int end = el.Offset + size;
            if (end > max) max = end;
        }
        return max;
    }

    private static int GetFormatSize(GpuFormat format) => format switch
    {
        GpuFormat.R32G32B32A32_Float => 16,
        GpuFormat.R32G32B32_Float => 12,
        GpuFormat.R32G32_Float => 8,
        GpuFormat.R32_Float => 4,
        GpuFormat.R16G16B16A16_Float => 8,
        GpuFormat.R8G8B8A8_UNorm => 4,
        GpuFormat.B8G8R8A8_UNorm => 4,
        GpuFormat.R16_UInt => 2,
        GpuFormat.R32_UInt => 4,
        GpuFormat.D32_Float => 4,
        GpuFormat.D24_UNorm_S8_UInt => 4,
        _ => 4
    };
}

public sealed record RenderPassDesc
{
    /// <summary>
    /// Single primary color render target. Null for depth-only passes.
    /// </summary>
    public IGpuTexture? ColorTarget { get; init; }

    /// <summary>
    /// Optional multiple render targets (MRT). If provided, overrides ColorTarget.
    /// </summary>
    public IReadOnlyList<IGpuTexture>? ColorTargets { get; init; }

    public Color4 ClearColor { get; init; } = Color4.CornflowerBlue;
    public bool ClearColorTarget { get; init; } = true;
    public IGpuTexture? DepthTarget { get; init; }
    public float ClearDepth { get; init; } = 1.0f;
    public bool ClearDepthTarget { get; init; } = false;
}

public sealed record PipelineDesc
{
    public required IGpuShader VertexShader { get; init; }
    public required IGpuShader PixelShader { get; init; }
    public VertexLayout? VertexLayout { get; init; }
    public PrimitiveTopology Topology { get; init; } = PrimitiveTopology.TriangleList;
    public CullMode CullMode { get; init; } = CullMode.Back;
    public FillMode FillMode { get; init; } = FillMode.Solid;
    public BlendMode BlendMode { get; init; } = BlendMode.Opaque;
    public bool DepthTestEnabled { get; init; } = true;
    public bool DepthWriteEnabled { get; init; } = true;
    public CompareFunction DepthFunction { get; init; } = CompareFunction.Less;
}
