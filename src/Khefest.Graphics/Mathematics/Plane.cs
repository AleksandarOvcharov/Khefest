using System.Numerics;

namespace Khefest.Graphics.Mathematics;

/// <summary>
/// Representation of a plane in 3D space: Ax + By + Cz + D = 0.
/// </summary>
public struct Plane : IEquatable<Plane>
{
    public Vector3 Normal;
    public float D;

    public Plane(Vector3 normal, float d)
    {
        Normal = normal;
        D = d;
    }

    public Plane(float a, float b, float c, float d)
    {
        Normal = new Vector3(a, b, c);
        D = d;
    }

    public void Normalize()
    {
        float factor = 1.0f / Normal.Length();
        Normal *= factor;
        D *= factor;
    }

    public static Plane Normalize(Plane plane)
    {
        float factor = 1.0f / plane.Normal.Length();
        return new Plane(plane.Normal * factor, plane.D * factor);
    }

    public float DotCoordinate(Vector3 point)
    {
        return Vector3.Dot(Normal, point) + D;
    }

    public bool Equals(Plane other) => Normal == other.Normal && D == other.D;
    public override bool Equals(object? obj) => obj is Plane other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Normal, D);
    public static bool operator ==(Plane a, Plane b) => a.Equals(b);
    public static bool operator !=(Plane a, Plane b) => !a.Equals(b);

    public override string ToString() => $"Plane(Normal: {Normal}, D: {D})";
}
