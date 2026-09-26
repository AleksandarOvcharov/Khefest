using System.Numerics;

namespace Khefest.Graphics.ThreeD.Lighting;

/// <summary>
/// Omnidirectional ambient background lighting to illuminate shadow areas.
/// </summary>
public struct AmbientLight
{
    /// <summary>
    /// Ambient light color (RGB in [0, 1]).
    /// </summary>
    public Vector3 Color { get; set; }

    /// <summary>
    /// Ambient light intensity.
    /// </summary>
    public float Intensity { get; set; }

    public AmbientLight(Vector3 color, float intensity = 0.2f)
    {
        Color = color;
        Intensity = intensity;
    }

    public static AmbientLight Default => new(new Vector3(0.2f, 0.25f, 0.3f), 0.25f);
}
