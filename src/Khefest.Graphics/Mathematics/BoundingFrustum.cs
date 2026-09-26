using System.Numerics;

namespace Khefest.Graphics.Mathematics;

public enum ContainmentType
{
    Disjoint,
    Contains,
    Intersects
}

/// <summary>
/// A viewing frustum defined by 6 planes (Near, Far, Left, Right, Top, Bottom) extracted from a View-Projection matrix.
/// Used for high-performance frustum culling.
/// </summary>
public sealed class BoundingFrustum
{
    private Matrix4x4 _matrix;
    private readonly Plane[] _planes = new Plane[6];

    public Plane Near => _planes[0];
    public Plane Far => _planes[1];
    public Plane Left => _planes[2];
    public Plane Right => _planes[3];
    public Plane Top => _planes[4];
    public Plane Bottom => _planes[5];

    public Matrix4x4 Matrix
    {
        get => _matrix;
        set
        {
            _matrix = value;
            ExtractPlanes();
        }
    }

    public BoundingFrustum(Matrix4x4 value)
    {
        _matrix = value;
        ExtractPlanes();
    }

    private void ExtractPlanes()
    {
        // Left
        _planes[2] = Plane.Normalize(new Plane(
            _matrix.M14 + _matrix.M11,
            _matrix.M24 + _matrix.M21,
            _matrix.M34 + _matrix.M31,
            _matrix.M44 + _matrix.M41));

        // Right
        _planes[3] = Plane.Normalize(new Plane(
            _matrix.M14 - _matrix.M11,
            _matrix.M24 - _matrix.M21,
            _matrix.M34 - _matrix.M31,
            _matrix.M44 - _matrix.M41));

        // Top
        _planes[4] = Plane.Normalize(new Plane(
            _matrix.M14 - _matrix.M12,
            _matrix.M24 - _matrix.M22,
            _matrix.M34 - _matrix.M32,
            _matrix.M44 - _matrix.M42));

        // Bottom
        _planes[5] = Plane.Normalize(new Plane(
            _matrix.M14 + _matrix.M12,
            _matrix.M24 + _matrix.M22,
            _matrix.M34 + _matrix.M32,
            _matrix.M44 + _matrix.M42));

        // Near (Direct3D 0..1 depth clip or standard clip)
        _planes[0] = Plane.Normalize(new Plane(
            _matrix.M13,
            _matrix.M23,
            _matrix.M33,
            _matrix.M43));

        // Far
        _planes[1] = Plane.Normalize(new Plane(
            _matrix.M14 - _matrix.M13,
            _matrix.M24 - _matrix.M23,
            _matrix.M34 - _matrix.M33,
            _matrix.M44 - _matrix.M43));
    }

    public bool Contains(Vector3 point)
    {
        for (int i = 0; i < 6; i++)
        {
            if (_planes[i].DotCoordinate(point) < 0.0f)
            {
                return false;
            }
        }
        return true;
    }

    public bool Intersects(BoundingSphere sphere)
    {
        for (int i = 0; i < 6; i++)
        {
            float dist = _planes[i].DotCoordinate(sphere.Center);
            if (dist < -sphere.Radius)
            {
                return false;
            }
        }
        return true;
    }

    public bool Intersects(BoundingBox box)
    {
        Span<Vector3> corners = stackalloc Vector3[8];
        box.GetCorners(corners);

        for (int i = 0; i < 6; i++)
        {
            int outsideCount = 0;
            for (int j = 0; j < 8; j++)
            {
                if (_planes[i].DotCoordinate(corners[j]) < 0.0f)
                {
                    outsideCount++;
                }
            }

            // If all 8 corners are outside this plane, the box is completely outside the frustum
            if (outsideCount == 8)
            {
                return false;
            }
        }

        return true;
    }
}
