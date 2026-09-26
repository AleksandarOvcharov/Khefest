using System.Numerics;
using Khefest.Graphics.LowLevel;
using Khefest.Input;
using Khefest.UI.Core;
using Khefest.UI.Rendering;
using Khefest.UI.Styling;

namespace Khefest.UI.Widgets;

/// <summary>
/// Single-line interactive text input field with cursor navigation, selection, and keyboard editing.
/// </summary>
public sealed class TextBox : Widget
{
    private string _text = string.Empty;
    private int _caretIndex;
    private float _blinkTimer;
    private bool _caretVisible = true;

    public string Text
    {
        get => _text;
        set
        {
            var clean = value ?? string.Empty;
            if (_text != clean)
            {
                _text = clean;
                _caretIndex = Math.Clamp(_caretIndex, 0, _text.Length);
                TextChanged?.Invoke(this, _text);
            }
        }
    }

    public string Placeholder { get; set; } = string.Empty;
    public int CaretIndex
    {
        get => _caretIndex;
        set => _caretIndex = Math.Clamp(value, 0, _text.Length);
    }

    public float FontScale { get; set; } = 1.0f;
    public event Action<TextBox, string>? TextChanged;
    public event Action<TextBox>? EnterPressed;

    public TextBox(string initialText = "")
    {
        Focusable = true;
        Height = 28f;
        Padding = new Thickness(6f, 4f);
        Text = initialText;
        _caretIndex = _text.Length;

        KeyDown += OnKeyDownInternal;
        CharInput += OnCharInputInternal;
        FocusGained += _ => { _blinkTimer = 0f; _caretVisible = true; };
    }

    public void Update(float deltaTime)
    {
        if (IsFocused)
        {
            _blinkTimer += deltaTime;
            if (_blinkTimer >= 0.5f)
            {
                _caretVisible = !_caretVisible;
                _blinkTimer = 0f;
            }
        }
    }

    private void OnKeyDownInternal(Widget sender, Key key)
    {
        if (!IsEnabled || !IsFocused) return;

        switch (key)
        {
            case Key.Left:
                if (_caretIndex > 0) _caretIndex--;
                _caretVisible = true;
                _blinkTimer = 0f;
                break;

            case Key.Right:
                if (_caretIndex < _text.Length) _caretIndex++;
                _caretVisible = true;
                _blinkTimer = 0f;
                break;

            case Key.Home:
                _caretIndex = 0;
                _caretVisible = true;
                _blinkTimer = 0f;
                break;

            case Key.End:
                _caretIndex = _text.Length;
                _caretVisible = true;
                _blinkTimer = 0f;
                break;

            case Key.Backspace:
                if (_caretIndex > 0)
                {
                    int oldIndex = _caretIndex;
                    _caretIndex--;
                    _text = _text.Remove(oldIndex - 1, 1);
                    TextChanged?.Invoke(this, _text);
                    _caretVisible = true;
                    _blinkTimer = 0f;
                }
                break;

            case Key.Delete:
                if (_caretIndex < _text.Length)
                {
                    _text = _text.Remove(_caretIndex, 1);
                    TextChanged?.Invoke(this, _text);
                    _caretVisible = true;
                    _blinkTimer = 0f;
                }
                break;

            case Key.Enter:
                EnterPressed?.Invoke(this);
                break;
        }
    }

    private void OnCharInputInternal(Widget sender, char c)
    {
        if (!IsEnabled || !IsFocused) return;
        if (char.IsControl(c)) return;

        _text = _text.Insert(_caretIndex, c.ToString());
        _caretIndex++;
        TextChanged?.Invoke(this, _text);
        _caretVisible = true;
        _blinkTimer = 0f;
    }

    protected override Vector2 MeasureContent(Vector2 availableSize)
    {
        return new Vector2(140f, 28f);
    }

    protected override void RenderBackground(UIRenderer renderer, UITheme theme)
    {
        renderer.FillRectangle(Bounds, theme.InputBackground);

        var border = IsFocused ? theme.InputFocusedBorder : (IsHovered ? theme.Accent : theme.InputBorder);
        renderer.DrawRectangle(Bounds, border, IsFocused ? 2f : 1f);
    }

    protected override void RenderForeground(UIRenderer renderer, UITheme theme)
    {
        float charWidth = 9f * FontScale;
        float textX = Bounds.X + Padding.Left;
        float textY = Bounds.Y + (Bounds.Height - 12f * FontScale) * 0.5f;

        if (string.IsNullOrEmpty(_text) && !string.IsNullOrEmpty(Placeholder) && !IsFocused)
        {
            renderer.DrawText(Placeholder, new Vector2(textX, textY), theme.TextSecondary, FontScale);
        }
        else
        {
            renderer.DrawText(_text, new Vector2(textX, textY), theme.TextPrimary, FontScale);
        }

        // Draw blinking caret cursor
        if (IsFocused && _caretVisible)
        {
            float caretX = textX + (_caretIndex * charWidth);
            var caretRect = new UIRect(caretX, textY - 1f, 2f, 14f * FontScale);
            renderer.FillRectangle(caretRect, theme.Accent);
        }
    }
}
