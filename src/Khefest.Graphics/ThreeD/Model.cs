using System.Numerics;
using Khefest.Core.Resources;

namespace Khefest.Graphics.ThreeD;

/// <summary>
/// A constituent part of a 3D model containing a mesh, material, and local transformation.
/// </summary>
public sealed class ModelPart
{
    public Mesh Mesh { get; set; }
    public Material Material { get; set; }
    public Matrix4x4 Transform { get; set; } = Matrix4x4.Identity;

    public ModelPart(Mesh mesh, Material material, Matrix4x4? transform = null)
    {
        Mesh = mesh ?? throw new ArgumentNullException(nameof(mesh));
        Material = material ?? throw new ArgumentNullException(nameof(material));
        if (transform.HasValue) Transform = transform.Value;
    }
}

/// <summary>
/// Composite 3D model consisting of one or more mesh parts and materials.
/// </summary>
public sealed class Model : ResourceBase
{
    private readonly List<ModelPart> _parts = new();
    private readonly bool _ownsResources;

    public IReadOnlyList<ModelPart> Parts => _parts;

    public Model(string name, Mesh mesh, Material material, ResourceManager? manager = null, bool ownsResources = true)
        : base(name, ResourceType.Model, mesh.SizeInBytes, manager?.Tracker.EnableLeakTracking ?? false)
    {
        _ownsResources = ownsResources;
        _parts.Add(new ModelPart(mesh, material));
        manager?.Register(this);
    }

    public Model(string name, IEnumerable<ModelPart> parts, ResourceManager? manager = null, bool ownsResources = true)
        : base(name, ResourceType.Model, 0, manager?.Tracker.EnableLeakTracking ?? false)
    {
        _ownsResources = ownsResources;
        _parts.AddRange(parts);
        manager?.Register(this);
    }

    public void AddPart(ModelPart part)
    {
        ArgumentNullException.ThrowIfNull(part);
        _parts.Add(part);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && _ownsResources)
        {
            foreach (var part in _parts)
            {
                part.Mesh.Dispose();
                part.Material.Dispose();
            }
        }
    }
}
