using System.Numerics;
using Khefest.Graphics.LowLevel;
using Khefest.Graphics.Texturing;
using Khefest.Graphics.TwoD;

namespace Khefest.Graphics.Text;

/// <summary>
/// Represents a single character glyph within a font texture atlas.
/// </summary>
public sealed class Glyph
{
    public char Character { get; }
    public TextureRegion Region { get; }
    public int XOffset { get; }
    public int YOffset { get; }
    public int XAdvance { get; }

    public Glyph(char character, TextureRegion region, int xOffset, int yOffset, int xAdvance)
    {
        Character = character;
        Region = region;
        XOffset = xOffset;
        YOffset = yOffset;
        XAdvance = xAdvance;
    }
}

/// <summary>
/// High-performance bitmap font supporting string measurement and multi-line batch rendering.
/// </summary>
public sealed class BitmapFont
{
    private readonly Dictionary<char, Glyph> _glyphs;
    public Texture2D Texture { get; }
    public int LineHeight { get; }
    public Glyph? FallbackGlyph { get; set; }

    public BitmapFont(Texture2D texture, int lineHeight, Dictionary<char, Glyph> glyphs)
    {
        Texture = texture ?? throw new ArgumentNullException(nameof(texture));
        LineHeight = lineHeight;
        _glyphs = glyphs ?? throw new ArgumentNullException(nameof(glyphs));

        if (_glyphs.TryGetValue('?', out var fallback) || _glyphs.TryGetValue(' ', out fallback))
        {
            FallbackGlyph = fallback;
        }
    }

    public bool TryGetGlyph(char c, out Glyph glyph)
    {
        if (_glyphs.TryGetValue(c, out glyph!))
        {
            return true;
        }

        if (FallbackGlyph != null)
        {
            glyph = FallbackGlyph;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Calculates the 2D bounding size in pixels for a given string.
    /// </summary>
    public Vector2 MeasureString(string? text, float scale = 1.0f)
    {
        if (string.IsNullOrEmpty(text)) return Vector2.Zero;

        float maxLineWidth = 0.0f;
        float currentLineWidth = 0.0f;
        int lines = 1;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '\r') continue;
            if (c == '\n')
            {
                if (currentLineWidth > maxLineWidth) maxLineWidth = currentLineWidth;
                currentLineWidth = 0.0f;
                lines++;
                continue;
            }

            if (TryGetGlyph(c, out var glyph))
            {
                currentLineWidth += glyph.XAdvance * scale;
            }
        }

        if (currentLineWidth > maxLineWidth) maxLineWidth = currentLineWidth;
        return new Vector2(maxLineWidth, lines * LineHeight * scale);
    }

    /// <summary>
    /// Renders text through the provided <see cref="SpriteBatch"/>.
    /// </summary>
    public void DrawString(
        SpriteBatch batch,
        string? text,
        Vector2 position,
        Color4 color,
        float scale = 1.0f)
    {
        if (string.IsNullOrEmpty(text)) return;
        ArgumentNullException.ThrowIfNull(batch);

        float cursorX = position.X;
        float cursorY = position.Y;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '\r') continue;
            if (c == '\n')
            {
                cursorX = position.X;
                cursorY += LineHeight * scale;
                continue;
            }

            if (TryGetGlyph(c, out var glyph))
            {
                var glyphPos = new Vector2(
                    cursorX + glyph.XOffset * scale,
                    cursorY + glyph.YOffset * scale);

                var glyphSize = new Vector2(
                    glyph.Region.Width * scale,
                    glyph.Region.Height * scale);

                batch.Draw(glyph.Region, glyphPos, glyphSize, color);
                cursorX += glyph.XAdvance * scale;
            }
        }
    }
}
