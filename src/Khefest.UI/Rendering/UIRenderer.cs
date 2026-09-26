using System.Numerics;
using Khefest.Graphics.LowLevel;
using Khefest.Graphics.Text;
using Khefest.Graphics.TwoD;
using Khefest.UI.Core;

namespace Khefest.UI.Rendering;

/// <summary>
/// High-level 2D rendering bridge for drawing styled UI primitives and typography through <see cref="SpriteBatch"/>.
/// </summary>
public sealed class UIRenderer
{
    private readonly SpriteBatch _spriteBatch;
    private readonly BitmapFont? _customFont;

    public SpriteBatch SpriteBatch => _spriteBatch;

    public UIRenderer(SpriteBatch spriteBatch, BitmapFont? font = null)
    {
        _spriteBatch = spriteBatch ?? throw new ArgumentNullException(nameof(spriteBatch));
        _customFont = font;
    }

    public void FillRectangle(in UIRect rect, Color4 color)
    {
        if (rect.Width <= 0 || rect.Height <= 0) return;
        _spriteBatch.FillRectangle(rect.Position, rect.Size, color);
    }

    public void DrawRectangle(in UIRect rect, Color4 color, float thickness = 1.0f)
    {
        if (rect.Width <= 0 || rect.Height <= 0) return;
        _spriteBatch.DrawRectangle(rect.Position, rect.Size, color, thickness);
    }

    public void DrawText(string? text, Vector2 position, Color4 color, float scale = 1.0f)
    {
        if (string.IsNullOrEmpty(text)) return;

        if (_customFont != null)
        {
            _customFont.DrawString(_spriteBatch, text, position, color, scale);
        }
        else
        {
            _spriteBatch.DrawString(text, position, color, scale);
        }
    }

    public Vector2 MeasureText(string? text, float scale = 1.0f)
    {
        if (string.IsNullOrEmpty(text)) return Vector2.Zero;

        if (_customFont != null)
        {
            return _customFont.MeasureString(text, scale);
        }

        // Approximate standard 8x8 bitmap font sizing with 1px tracking
        float charWidth = 9.0f * scale;
        float lineHeight = 12.0f * scale;

        string[] lines = text.Split('\n');
        float maxWidth = 0.0f;
        for (int i = 0; i < lines.Length; i++)
        {
            float w = lines[i].TrimEnd('\r').Length * charWidth;
            if (w > maxWidth) maxWidth = w;
        }

        return new Vector2(maxWidth, lines.Length * lineHeight);
    }
}
