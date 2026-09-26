using System.Numerics;
using System.Runtime.InteropServices;
using Khefest.Graphics.LowLevel;

namespace Khefest.Graphics.ThreeD;

/// <summary>
/// Standard 3D vertex containing position, normal, and texture coordinates (32 bytes).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct VertexPositionNormalTexture
{
    public Vector3 Position;
    public Vector3 Normal;
    public Vector2 TexCoord;

    public const int SizeInBytes = 32;

    public static readonly VertexLayout Layout = new(
        SizeInBytes,
        new VertexElement("POSITION", 0, GpuFormat.R32G32B32_Float, 0),
        new VertexElement("NORMAL", 0, GpuFormat.R32G32B32_Float, 12),
        new VertexElement("TEXCOORD", 0, GpuFormat.R32G32_Float, 24));

    public VertexPositionNormalTexture(Vector3 position, Vector3 normal, Vector2 texCoord)
    {
        Position = position;
        Normal = normal;
        TexCoord = texCoord;
    }
}
