using System.Numerics;

namespace Khefest.Graphics.Camera;

/// <summary>
/// A 2D orthographic camera providing transformation matrices, zooming, panning, rotation,
/// and screen-to-world coordinate projections.
/// </summary>
public sealed class Camera2D
{
    private Vector2 _position;
    private float _zoom = 1.0f;
    private float _rotation = 0.0f;
    private Vector2 _origin;
    private int _viewportWidth;
    private int _viewportHeight;

    private Matrix4x4 _viewMatrix;
    private Matrix4x4 _inverseViewMatrix;
    private Matrix4x4 _projectionMatrix;
    private Matrix4x4 _viewProjectionMatrix;
    private bool _isDirty = true;

    public Vector2 Position
    {
        get => _position;
        set
        {
            if (_position != value)
            {
                _position = value;
                _isDirty = true;
            }
        }
    }

    public float Zoom
    {
        get => _zoom;
        set
        {
            var clamped = Math.Clamp(value, 0.01f, 100.0f);
            if (Math.Abs(_zoom - clamped) > 0.0001f)
            {
                _zoom = clamped;
                _isDirty = true;
            }
        }
    }

    public float Rotation
    {
        get => _rotation;
        set
        {
            if (Math.Abs(_rotation - value) > 0.00001f)
            {
                _rotation = value;
                _isDirty = true;
            }
        }
    }

    public Vector2 Origin
    {
        get => _origin;
        set
        {
            if (_origin != value)
            {
                _origin = value;
                _isDirty = true;
            }
        }
    }

    public int ViewportWidth
    {
        get => _viewportWidth;
        set
        {
            if (_viewportWidth != value)
            {
                _viewportWidth = Math.Max(1, value);
                _isDirty = true;
            }
        }
    }

    public int ViewportHeight
    {
        get => _viewportHeight;
        set
        {
            if (_viewportHeight != value)
            {
                _viewportHeight = Math.Max(1, value);
                _isDirty = true;
            }
        }
    }

    public Camera2D(int viewportWidth, int viewportHeight)
    {
        _viewportWidth = Math.Max(1, viewportWidth);
        _viewportHeight = Math.Max(1, viewportHeight);
        _origin = new Vector2(_viewportWidth * 0.5f, _viewportHeight * 0.5f);
        _isDirty = true;
    }

    /// <summary>
    /// Resizes the camera's viewport and updates the center origin.
    /// </summary>
    public void Resize(int width, int height)
    {
        _viewportWidth = Math.Max(1, width);
        _viewportHeight = Math.Max(1, height);
        _origin = new Vector2(_viewportWidth * 0.5f, _viewportHeight * 0.5f);
        _isDirty = true;
    }

    public void Move(Vector2 delta) => Position += delta;

    public void Rotate(float deltaRadians) => Rotation += deltaRadians;

    public void ZoomBy(float factor) => Zoom *= factor;

    public Matrix4x4 GetViewMatrix()
    {
        UpdateMatrices();
        return _viewMatrix;
    }

    public Matrix4x4 GetProjectionMatrix()
    {
        UpdateMatrices();
        return _projectionMatrix;
    }

    public Matrix4x4 GetViewProjectionMatrix()
    {
        UpdateMatrices();
        return _viewProjectionMatrix;
    }

    public Vector2 ScreenToWorld(Vector2 screenCoord)
    {
        UpdateMatrices();
        var transformed = Vector4.Transform(new Vector4(screenCoord.X, screenCoord.Y, 0.0f, 1.0f), _inverseViewMatrix);
        return new Vector2(transformed.X, transformed.Y);
    }

    public Vector2 WorldToScreen(Vector2 worldCoord)
    {
        UpdateMatrices();
        var transformed = Vector4.Transform(new Vector4(worldCoord.X, worldCoord.Y, 0.0f, 1.0f), _viewMatrix);
        return new Vector2(transformed.X, transformed.Y);
    }

    public (Vector2 Min, Vector2 Max) GetVisibleBounds()
    {
        var tl = ScreenToWorld(new Vector2(0, 0));
        var tr = ScreenToWorld(new Vector2(_viewportWidth, 0));
        var bl = ScreenToWorld(new Vector2(0, _viewportHeight));
        var br = ScreenToWorld(new Vector2(_viewportWidth, _viewportHeight));

        var minX = Math.Min(Math.Min(tl.X, tr.X), Math.Min(bl.X, br.X));
        var maxX = Math.Max(Math.Max(tl.X, tr.X), Math.Max(bl.X, br.X));
        var minY = Math.Min(Math.Min(tl.Y, tr.Y), Math.Min(bl.Y, br.Y));
        var maxY = Math.Max(Math.Max(tl.Y, tr.Y), Math.Max(bl.Y, br.Y));

        return (new Vector2(minX, minY), new Vector2(maxX, maxY));
    }

    private void UpdateMatrices()
    {
        if (!_isDirty) return;

        // View Matrix: Translate to negative position, rotate, scale, translate back to origin
        _viewMatrix =
            Matrix4x4.CreateTranslation(-_position.X, -_position.Y, 0.0f) *
            Matrix4x4.CreateRotationZ(_rotation) *
            Matrix4x4.CreateScale(_zoom, _zoom, 1.0f) *
            Matrix4x4.CreateTranslation(_origin.X, _origin.Y, 0.0f);

        if (!Matrix4x4.Invert(_viewMatrix, out _inverseViewMatrix))
        {
            _inverseViewMatrix = Matrix4x4.Identity;
        }

        // Orthographic Projection: (0,0) top-left to (ViewportWidth, ViewportHeight) bottom-right
        _projectionMatrix = Matrix4x4.CreateOrthographicOffCenter(
            0.0f,
            _viewportWidth,
            _viewportHeight,
            0.0f,
            0.0f,
            1.0f);

        _viewProjectionMatrix = _viewMatrix * _projectionMatrix;
        _isDirty = false;
    }
}
