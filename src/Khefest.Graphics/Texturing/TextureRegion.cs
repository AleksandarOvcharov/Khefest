using System.Numerics;

namespace Khefest.Graphics.Texturing;

/// <summary>
/// Defines a rectangular region within a <see cref="Texture2D"/> in pixel and normalized UV coordinates.
/// </summary>
public sealed class TextureRegion
{
    public Texture2D Texture { get; }
    public int X { get; }
    public int Y { get; }
    public int Width { get; }
    public int Height { get; }

    public float U0 { get; }
    public float V0 { get; }
    public float U1 { get; }
    public float V1 { get; }

    public TextureRegion(Texture2D texture)
        : this(texture, 0, 0, texture.Width, texture.Height)
    {
    }

    public TextureRegion(Texture2D texture, int x, int y, int width, int height)
    {
        Texture = texture ?? throw new ArgumentNullException(nameof(texture));
        X = x;
        Y = y;
        Width = width;
        Height = height;

        float invW = 1.0f / Math.Max(1, texture.Width);
        float invH = 1.0f / Math.Max(1, texture.Height);

        U0 = x * invW;
        V0 = y * invH;
        U1 = (x + width) * invW;
        V1 = (y + height) * invH;
    }

    public TextureRegion(Texture2D texture, float u0, float v0, float u1, float v1)
    {
        Texture = texture ?? throw new ArgumentNullException(nameof(texture));
        U0 = u0;
        V0 = v0;
        U1 = u1;
        V1 = v1;

        X = (int)(u0 * texture.Width);
        Y = (int)(v0 * texture.Height);
        Width = (int)((u1 - u0) * texture.Width);
        Height = (int)((v1 - v0) * texture.Height);
    }
}
