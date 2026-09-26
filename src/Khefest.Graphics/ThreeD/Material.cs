using System.Numerics;
using Khefest.Core.Resources;
using Khefest.Graphics.LowLevel;

namespace Khefest.Graphics.ThreeD;

/// <summary>
/// Surface material describing diffuse color/texture and specular highlight parameters.
/// </summary>
public sealed class Material : ResourceBase
{
    public IGpuTexture? DiffuseTexture { get; set; }
    public Vector4 Color { get; set; } = Vector4.One;
    public float SpecularPower { get; set; } = 32.0f;
    public float SpecularIntensity { get; set; } = 0.5f;

    public Material(
        string name,
        IGpuTexture? diffuseTexture = null,
        Vector4? color = null,
        float specularPower = 32.0f,
        float specularIntensity = 0.5f,
        ResourceManager? manager = null)
        : base(name, ResourceType.Custom, 0, manager?.Tracker.EnableLeakTracking ?? false)
    {
        DiffuseTexture = diffuseTexture;
        if (color.HasValue) Color = color.Value;
        SpecularPower = specularPower;
        SpecularIntensity = specularIntensity;

        manager?.Register(this);
    }

    protected override void Dispose(bool disposing)
    {
        // Material references textures, but lifetime is governed by the owner or resource manager.
    }
}
