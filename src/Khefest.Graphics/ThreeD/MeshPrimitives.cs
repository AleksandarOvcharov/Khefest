using System.Numerics;
using System.Runtime.InteropServices;
using Khefest.Core.Resources;
using Khefest.Graphics.LowLevel;

namespace Khefest.Graphics.ThreeD;

/// <summary>
/// Procedural generator for standard 3D geometric primitive meshes.
/// </summary>
public static class MeshPrimitives
{
    /// <summary>
    /// Generates a solid 3D cube with distinct face normals and UV coordinates.
    /// </summary>
    public static Mesh CreateCube(IGpuDevice device, float size = 1.0f, ResourceManager? manager = null)
    {
        ArgumentNullException.ThrowIfNull(device);
        float h = size * 0.5f;

        // 6 faces * 4 vertices = 24 vertices
        var vertices = new VertexPositionNormalTexture[24]
        {
            // Front face (+Z)
            new(new Vector3(-h, -h,  h), Vector3.UnitZ, new Vector2(0f, 1f)),
            new(new Vector3( h, -h,  h), Vector3.UnitZ, new Vector2(1f, 1f)),
            new(new Vector3( h,  h,  h), Vector3.UnitZ, new Vector2(1f, 0f)),
            new(new Vector3(-h,  h,  h), Vector3.UnitZ, new Vector2(0f, 0f)),

            // Back face (-Z)
            new(new Vector3( h, -h, -h), -Vector3.UnitZ, new Vector2(0f, 1f)),
            new(new Vector3(-h, -h, -h), -Vector3.UnitZ, new Vector2(1f, 1f)),
            new(new Vector3(-h,  h, -h), -Vector3.UnitZ, new Vector2(1f, 0f)),
            new(new Vector3( h,  h, -h), -Vector3.UnitZ, new Vector2(0f, 0f)),

            // Top face (+Y)
            new(new Vector3(-h,  h,  h), Vector3.UnitY, new Vector2(0f, 1f)),
            new(new Vector3( h,  h,  h), Vector3.UnitY, new Vector2(1f, 1f)),
            new(new Vector3( h,  h, -h), Vector3.UnitY, new Vector2(1f, 0f)),
            new(new Vector3(-h,  h, -h), Vector3.UnitY, new Vector2(0f, 0f)),

            // Bottom face (-Y)
            new(new Vector3(-h, -h, -h), -Vector3.UnitY, new Vector2(0f, 1f)),
            new(new Vector3( h, -h, -h), -Vector3.UnitY, new Vector2(1f, 1f)),
            new(new Vector3( h, -h,  h), -Vector3.UnitY, new Vector2(1f, 0f)),
            new(new Vector3(-h, -h,  h), -Vector3.UnitY, new Vector2(0f, 0f)),

            // Left face (-X)
            new(new Vector3(-h, -h, -h), -Vector3.UnitX, new Vector2(0f, 1f)),
            new(new Vector3(-h, -h,  h), -Vector3.UnitX, new Vector2(1f, 1f)),
            new(new Vector3(-h,  h,  h), -Vector3.UnitX, new Vector2(1f, 0f)),
            new(new Vector3(-h,  h, -h), -Vector3.UnitX, new Vector2(0f, 0f)),

            // Right face (+X)
            new(new Vector3( h, -h,  h), Vector3.UnitX, new Vector2(0f, 1f)),
            new(new Vector3( h, -h, -h), Vector3.UnitX, new Vector2(1f, 1f)),
            new(new Vector3( h,  h, -h), Vector3.UnitX, new Vector2(1f, 0f)),
            new(new Vector3( h,  h,  h), Vector3.UnitX, new Vector2(0f, 0f)),
        };

        // 6 faces * 2 triangles * 3 indices = 36 indices (clockwise winding for Left-Handed Direct3D)
        var indices = new ushort[36];
        for (int f = 0; f < 6; f++)
        {
            ushort baseIdx = (ushort)(f * 4);
            int i = f * 6;
            indices[i + 0] = (ushort)(baseIdx + 0);
            indices[i + 1] = (ushort)(baseIdx + 3);
            indices[i + 2] = (ushort)(baseIdx + 1);
            indices[i + 3] = (ushort)(baseIdx + 1);
            indices[i + 4] = (ushort)(baseIdx + 3);
            indices[i + 5] = (ushort)(baseIdx + 2);
        }

        var bounds = new BoundingBox3D(new Vector3(-h), new Vector3(h));
        return CreateMeshFromData(device, "CubeMesh", vertices, indices, bounds, manager);
    }

    /// <summary>
    /// Generates a UV sphere with smooth vertex normals.
    /// </summary>
    public static Mesh CreateSphere(IGpuDevice device, float radius = 0.5f, int segments = 24, int rings = 16, ResourceManager? manager = null)
    {
        ArgumentNullException.ThrowIfNull(device);
        segments = Math.Max(3, segments);
        rings = Math.Max(2, rings);

        int vertexCount = (rings + 1) * (segments + 1);
        var vertices = new VertexPositionNormalTexture[vertexCount];
        int vIdx = 0;

        for (int r = 0; r <= rings; r++)
        {
            float v = (float)r / rings;
            float phi = v * MathF.PI; // 0 to PI

            for (int s = 0; s <= segments; s++)
            {
                float u = (float)s / segments;
                float theta = u * MathF.PI * 2f; // 0 to 2*PI

                float x = MathF.Sin(phi) * MathF.Sin(theta);
                float y = MathF.Cos(phi);
                float z = MathF.Sin(phi) * MathF.Cos(theta);

                var normal = new Vector3(x, y, z);
                var pos = normal * radius;

                vertices[vIdx++] = new VertexPositionNormalTexture(pos, normal, new Vector2(u, v));
            }
        }

        int indexCount = rings * segments * 6;
        var indices = new ushort[indexCount];
        int iIdx = 0;

        for (int r = 0; r < rings; r++)
        {
            for (int s = 0; s < segments; s++)
            {
                ushort current = (ushort)(r * (segments + 1) + s);
                ushort next = (ushort)(current + segments + 1);

                indices[iIdx++] = current;
                indices[iIdx++] = (ushort)(next + 1);
                indices[iIdx++] = next;

                indices[iIdx++] = current;
                indices[iIdx++] = (ushort)(current + 1);
                indices[iIdx++] = (ushort)(next + 1);
            }
        }

        var bounds = new BoundingBox3D(new Vector3(-radius), new Vector3(radius));
        return CreateMeshFromData(device, "SphereMesh", vertices, indices, bounds, manager);
    }

    /// <summary>
    /// Generates a horizontal ground plane facing Up (+Y).
    /// </summary>
    public static Mesh CreatePlane(IGpuDevice device, float width = 10f, float depth = 10f, ResourceManager? manager = null)
    {
        ArgumentNullException.ThrowIfNull(device);
        float hw = width * 0.5f;
        float hd = depth * 0.5f;

        var vertices = new VertexPositionNormalTexture[4]
        {
            new(new Vector3(-hw, 0f,  hd), Vector3.UnitY, new Vector2(0f, 0f)),
            new(new Vector3( hw, 0f,  hd), Vector3.UnitY, new Vector2(1f, 0f)),
            new(new Vector3( hw, 0f, -hd), Vector3.UnitY, new Vector2(1f, 1f)),
            new(new Vector3(-hw, 0f, -hd), Vector3.UnitY, new Vector2(0f, 1f))
        };

        var indices = new ushort[6] { 0, 1, 2, 0, 2, 3 };
        var bounds = new BoundingBox3D(new Vector3(-hw, 0f, -hd), new Vector3(hw, 0f, hd));
        return CreateMeshFromData(device, "PlaneMesh", vertices, indices, bounds, manager);
    }

    private static Mesh CreateMeshFromData(
        IGpuDevice device,
        string name,
        VertexPositionNormalTexture[] vertices,
        ushort[] indices,
        BoundingBox3D bounds,
        ResourceManager? manager)
    {
        var vbBytes = MemoryMarshal.AsBytes(vertices.AsSpan());
        var ibBytes = MemoryMarshal.AsBytes(indices.AsSpan());

        var vb = device.CreateBuffer($"{name}_VB", vbBytes.Length, BufferUsage.Vertex);
        vb.SetData(vbBytes);

        var ib = device.CreateBuffer($"{name}_IB", ibBytes.Length, BufferUsage.Index);
        ib.SetData(ibBytes);

        return new Mesh(name, vb, ib, vertices.Length, indices.Length, bounds, IndexFormat.UInt16, manager, ownsBuffers: true);
    }
}
