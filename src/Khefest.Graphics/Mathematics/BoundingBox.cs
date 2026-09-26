using System.Numerics;

namespace Khefest.Graphics.Mathematics;

/// <summary>
/// Axis-Aligned Bounding Box (AABB) in 3D space for spatial acceleration, broadphase collision, and culling.
/// </summary>
public struct BoundingBox : IEquatable<BoundingBox>
{
    public Vector3 Min;
    public Vector3 Max;

    public Vector3 Center => (Min + Max) * 0.5f;
    public Vector3 Extents => (Max - Min) * 0.5f;
    public Vector3 Size => Max - Min;

    public BoundingBox(Vector3 min, Vector3 max)
    {
        Min = min;
        Max = max;
    }

    public bool Contains(Vector3 point)
    {
        return point.X >= Min.X && point.X <= Max.X &&
               point.Y >= Min.Y && point.Y <= Max.Y &&
               point.Z >= Min.Z && point.Z <= Max.Z;
    }

    public bool Intersects(BoundingBox other)
    {
        return Max.X >= other.Min.X && Min.X <= other.Max.X &&
               Max.Y >= other.Min.Y && Min.Y <= other.Max.Y &&
               Max.Z >= other.Min.Z && Min.Z <= other.Max.Z;
    }

    public static BoundingBox CreateMerged(BoundingBox a, BoundingBox b)
    {
        return new BoundingBox(
            Vector3.Min(a.Min, b.Min),
            Vector3.Max(a.Max, b.Max));
    }

    public static BoundingBox CreateFromPoints(IEnumerable<Vector3> points)
    {
        ArgumentNullException.ThrowIfNull(points);

        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        bool any = false;

        foreach (var p in points)
        {
            any = true;
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
        }

        return any ? new BoundingBox(min, max) : new BoundingBox(Vector3.Zero, Vector3.Zero);
    }

    public BoundingBox Transform(Matrix4x4 matrix)
    {
        Span<Vector3> corners = stackalloc Vector3[8];
        GetCorners(corners);

        var newMin = new Vector3(float.MaxValue);
        var newMax = new Vector3(float.MinValue);

        for (int i = 0; i < 8; i++)
        {
            var transformed = Vector3.Transform(corners[i], matrix);
            newMin = Vector3.Min(newMin, transformed);
            newMax = Vector3.Max(newMax, transformed);
        }

        return new BoundingBox(newMin, newMax);
    }

    public void GetCorners(Span<Vector3> corners)
    {
        if (corners.Length < 8)
        {
            throw new ArgumentException("Corners span must have length of at least 8.", nameof(corners));
        }

        corners[0] = new Vector3(Min.X, Max.Y, Max.Z);
        corners[1] = new Vector3(Max.X, Max.Y, Max.Z);
        corners[2] = new Vector3(Max.X, Min.Y, Max.Z);
        corners[3] = new Vector3(Min.X, Min.Y, Max.Z);
        corners[4] = new Vector3(Min.X, Max.Y, Min.Z);
        corners[5] = new Vector3(Max.X, Max.Y, Min.Z);
        corners[6] = new Vector3(Max.X, Min.Y, Min.Z);
        corners[7] = new Vector3(Min.X, Min.Y, Min.Z);
    }

    public bool Equals(BoundingBox other) => Min == other.Min && Max == other.Max;
    public override bool Equals(object? obj) => obj is BoundingBox other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Min, Max);
    public static bool operator ==(BoundingBox a, BoundingBox b) => a.Equals(b);
    public static bool operator !=(BoundingBox a, BoundingBox b) => !a.Equals(b);

    public override string ToString() => $"BoundingBox(Min: {Min}, Max: {Max})";
}
