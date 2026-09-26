namespace Khefest.Core.Errors;

/// <summary>
/// Identifies the subsystem where an error or diagnostic event originated.
/// </summary>
public enum KhefestSubsystem
{
    Core,
    Platform,
    Graphics,
    GraphicsLowLevel,
    GraphicsHighLevel,
    Resource,
    Audio,
    Input,
    Windowing,
    Assets,
    UI,
    Debug,
    Plugins
}
