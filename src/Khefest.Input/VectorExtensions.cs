using System.Numerics;

namespace Khefest.Input;

/// <summary>
/// Ergonomic extension methods for spatial vector types.
/// </summary>
public static class VectorExtensions
{
    /// <summary>
    /// Deconstructs a <see cref="Vector2"/> into (X, Y) components.
    /// </summary>
    public static void Deconstruct(this Vector2 vector, out float x, out float y)
    {
        x = vector.X;
        y = vector.Y;
    }
}
