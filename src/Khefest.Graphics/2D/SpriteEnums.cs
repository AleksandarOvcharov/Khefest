namespace Khefest.Graphics.TwoD;

/// <summary>
/// Defines visual mirroring effects applied during sprite rendering.
/// </summary>
[Flags]
public enum SpriteEffects
{
    None = 0,
    FlipHorizontally = 1,
    FlipVertically = 2
}

/// <summary>
/// Determines how sprites are ordered and batched before submitting draw calls.
/// </summary>
public enum SpriteSortMode
{
    /// <summary>
    /// Sprites are drawn in the order they were submitted (default).
    /// </summary>
    Deferred,

    /// <summary>
    /// Sprites are sorted by texture to minimize GPU state changes and batch flushes.
    /// </summary>
    Texture,

    /// <summary>
    /// Sprites are sorted from front to back.
    /// </summary>
    FrontToBack,

    /// <summary>
    /// Sprites are sorted from back to front (recommended for alpha transparency).
    /// </summary>
    BackToFront
}
