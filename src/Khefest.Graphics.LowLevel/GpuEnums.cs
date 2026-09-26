namespace Khefest.Graphics.LowLevel;

public enum GpuFormat
{
    R8G8B8A8_UNorm = 28,
    B8G8R8A8_UNorm = 87,
    R16G16B16A16_Float = 10,
    R32G32B32A32_Float = 2,
    R32G32B32_Float = 6,
    R32G32_Float = 16,
    R32_Float = 41,
    R16_UInt = 57,
    R32_UInt = 42,
    D32_Float = 40,
    D24_UNorm_S8_UInt = 45
}

[Flags]
public enum BufferUsage
{
    Vertex = 1 << 0,
    Index = 1 << 1,
    Uniform = 1 << 2,
    Storage = 1 << 3,
    Staging = 1 << 4
}

[Flags]
public enum TextureUsage
{
    Sampled = 1 << 0,
    RenderTarget = 1 << 1,
    DepthStencil = 1 << 2,
    Storage = 1 << 3
}

public enum IndexFormat
{
    UInt16,
    UInt32
}

public enum PrimitiveTopology
{
    TriangleList,
    TriangleStrip,
    LineList,
    LineStrip,
    PointList
}

public enum CullMode
{
    None,
    Back,
    Front
}

public enum FillMode
{
    Solid,
    Wireframe
}

public enum BlendMode
{
    Opaque,
    AlphaBlend,
    Additive
}

public enum CompareFunction
{
    Never,
    Less,
    Equal,
    LessEqual,
    Greater,
    NotEqual,
    GreaterEqual,
    Always
}

public enum ShaderStage
{
    Vertex,
    Pixel,
    Compute
}
