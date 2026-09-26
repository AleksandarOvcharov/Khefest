using System.Numerics;

namespace Khefest.Graphics.Mathematics;

/// <summary>
/// Bounding sphere in 3D space defined by a center and radius.
/// </summary>
public struct BoundingSphere : IEquatable<BoundingSphere>
{
    public Vector3 Center;
    public float Radius;

    public BoundingSphere(Vector3 center, float radius)
    {
        Center = center;
        Radius = MathF.Max(0.0f, radius);
    }

    public bool Contains(Vector3 point)
    {
        return Vector3.DistanceSquared(point, Center) <= Radius * Radius;
    }

    public bool Intersects(BoundingSphere other)
    {
        float totalRadius = Radius + other.Radius;
        return Vector3.DistanceSquared(Center, other.Center) <= totalRadius * totalRadius;
    }

    public bool Intersects(BoundingBox box)
    {
        var clamped = Vector3.Clamp(Center, box.Min, box.Max);
        return Vector3.DistanceSquared(Center, clamped) <= Radius * Radius;
    }

    public static BoundingSphere CreateFromPoints(IEnumerable<Vector3> points)
    {
        var box = BoundingBox.CreateFromPoints(points);
        var center = box.Center;
        float maxDistSq = 0.0f;

        foreach (var p in points)
        {
            float distSq = Vector3.DistanceSquared(center, p);
            if (distSq > maxDistSq)
            {
                maxDistSq = distSq;
            }
        }

        return new BoundingSphere(center, MathF.Sqrt(maxDistSq));
    }

    public bool Equals(BoundingSphere other) => Center == other.Center && Radius == other.Radius;
    public override bool Equals(object? obj) => obj is BoundingSphere other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Center, Radius);
    public static bool operator ==(BoundingSphere a, BoundingSphere b) => a.Equals(b);
    public static bool operator !=(BoundingSphere a, BoundingSphere b) => !a.Equals(b);

    public override string ToString() => $"BoundingSphere(Center: {Center}, Radius: {Radius})";
}
