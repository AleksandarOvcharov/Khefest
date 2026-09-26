namespace Khefest.Core.Resources;

/// <summary>
/// Primary resource types recognized by the Khefest resource management system.
/// </summary>
public enum ResourceType
{
    Buffer,
    Texture,
    Shader,
    Pipeline,
    Mesh,
    Model,
    Font,
    Audio,
    RenderTarget,
    Custom
}

/// <summary>
/// Current lifecycle state of an engine resource.
/// </summary>
public enum ResourceState
{
    Uninitialized,
    Allocated,
    Active,
    Disposed
}
