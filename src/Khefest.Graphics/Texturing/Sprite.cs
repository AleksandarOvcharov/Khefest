using System.Numerics;
using Khefest.Graphics.LowLevel;
using Khefest.Graphics.TwoD;

namespace Khefest.Graphics.Texturing;

/// <summary>
/// A 2D renderable entity combining a texture region with position, rotation, scale, origin, and color tint.
/// </summary>
public sealed class Sprite
{
    public TextureRegion Region { get; set; }
    public Vector2 Position { get; set; } = Vector2.Zero;
    public Vector2 Scale { get; set; } = Vector2.One;
    public Vector2 Origin { get; set; } = Vector2.Zero;
    public float Rotation { get; set; } = 0.0f;
    public Color4 Color { get; set; } = Color4.White;
    public SpriteEffects Effects { get; set; } = SpriteEffects.None;
    public float Depth { get; set; } = 0.0f;

    public float Width => Region.Width * Math.Abs(Scale.X);
    public float Height => Region.Height * Math.Abs(Scale.Y);

    public Sprite(TextureRegion region)
    {
        Region = region ?? throw new ArgumentNullException(nameof(region));
    }

    public Sprite(Texture2D texture)
        : this(new TextureRegion(texture))
    {
    }

    public void CenterOrigin()
    {
        Origin = new Vector2(Region.Width * 0.5f, Region.Height * 0.5f);
    }
}
