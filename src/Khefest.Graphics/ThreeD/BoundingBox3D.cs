using System.Numerics;

namespace Khefest.Graphics.ThreeD;

/// <summary>
/// Axis-aligned bounding box (AABB) in 3D space.
/// </summary>
public readonly struct BoundingBox3D
{
    public Vector3 Min { get; }
    public Vector3 Max { get; }
    public Vector3 Center => (Min + Max) * 0.5f;
    public Vector3 Extents => (Max - Min) * 0.5f;

    public BoundingBox3D(Vector3 min, Vector3 max)
    {
        Min = min;
        Max = max;
    }

    public static BoundingBox3D FromPoints(ReadOnlySpan<Vector3> points)
    {
        if (points.IsEmpty)
        {
            return new BoundingBox3D(Vector3.Zero, Vector3.Zero);
        }

        var min = points[0];
        var max = points[0];

        for (int i = 1; i < points.Length; i++)
        {
            min = Vector3.Min(min, points[i]);
            max = Vector3.Max(max, points[i]);
        }

        return new BoundingBox3D(min, max);
    }
}
