using System.Numerics;

namespace Khefest.Graphics.ThreeD.Lighting;

/// <summary>
/// Directional light source (e.g. sunlight) with uniform direction across the entire scene.
/// </summary>
public struct DirectionalLight
{
    private Vector3 _direction;

    /// <summary>
    /// Direction the light rays travel. Automatically normalized.
    /// </summary>
    public Vector3 Direction
    {
        get => _direction;
        set => _direction = Vector3.Normalize(value);
    }

    /// <summary>
    /// Light color (RGB in [0, 1]).
    /// </summary>
    public Vector3 Color { get; set; }

    /// <summary>
    /// Scalar light intensity.
    /// </summary>
    public float Intensity { get; set; }

    public DirectionalLight(Vector3 direction, Vector3 color, float intensity = 1.0f)
    {
        _direction = Vector3.Normalize(direction);
        Color = color;
        Intensity = intensity;
    }

    public static DirectionalLight Default => new(
        new Vector3(-0.5f, -1.0f, 0.5f),
        Vector3.One,
        1.0f);
}
