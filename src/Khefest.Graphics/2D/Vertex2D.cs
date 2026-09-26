using System.Numerics;
using System.Runtime.InteropServices;
using Khefest.Graphics.LowLevel;

namespace Khefest.Graphics.TwoD;

/// <summary>
/// Vertex format used by the 2D batch renderer for sprites, shapes, and text.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct Vertex2D
{
    public Vector2 Position;
    public Vector2 TexCoord;
    public Color4 Color;

    public Vertex2D(Vector2 position, Vector2 texCoord, Color4 color)
    {
        Position = position;
        TexCoord = texCoord;
        Color = color;
    }

    public static readonly VertexLayout Layout = new(
        sizeof(float) * 8, // 2 pos + 2 uv + 4 color = 8 floats = 32 bytes
        new VertexElement("POSITION", 0, GpuFormat.R32G32_Float, 0),
        new VertexElement("TEXCOORD", 0, GpuFormat.R32G32_Float, sizeof(float) * 2),
        new VertexElement("COLOR", 0, GpuFormat.R32G32B32A32_Float, sizeof(float) * 4));
}
