using System.Numerics;

namespace Khefest.Graphics.Camera;

/// <summary>
/// Perspective 3D camera supporting position, target, up vectors, and field-of-view settings.
/// Uses left-handed coordinate system standard for Direct3D (NDC Z in [0, 1]).
/// </summary>
public sealed class PerspectiveCamera
{
    private Vector3 _position;
    private Vector3 _target;
    private Vector3 _up;
    private float _fieldOfView;
    private float _aspectRatio;
    private float _nearPlane;
    private float _farPlane;

    private Matrix4x4 _viewMatrix;
    private Matrix4x4 _projectionMatrix;
    private Matrix4x4 _viewProjectionMatrix;
    private bool _dirty;

    public Vector3 Position
    {
        get => _position;
        set
        {
            _position = value;
            _dirty = true;
        }
    }

    public Vector3 Target
    {
        get => _target;
        set
        {
            _target = value;
            _dirty = true;
        }
    }

    public Vector3 Up
    {
        get => _up;
        set
        {
            _up = Vector3.Normalize(value);
            _dirty = true;
        }
    }

    public float FieldOfView
    {
        get => _fieldOfView;
        set
        {
            _fieldOfView = value;
            _dirty = true;
        }
    }

    public float AspectRatio
    {
        get => _aspectRatio;
        set
        {
            _aspectRatio = value;
            _dirty = true;
        }
    }

    public float NearPlane
    {
        get => _nearPlane;
        set
        {
            _nearPlane = value;
            _dirty = true;
        }
    }

    public float FarPlane
    {
        get => _farPlane;
        set
        {
            _farPlane = value;
            _dirty = true;
        }
    }

    public Vector3 Forward => Vector3.Normalize(_target - _position);

    public Matrix4x4 ViewMatrix
    {
        get
        {
            UpdateMatrices();
            return _viewMatrix;
        }
    }

    public Matrix4x4 ProjectionMatrix
    {
        get
        {
            UpdateMatrices();
            return _projectionMatrix;
        }
    }

    public Matrix4x4 ViewProjectionMatrix
    {
        get
        {
            UpdateMatrices();
            return _viewProjectionMatrix;
        }
    }

    public PerspectiveCamera(
        Vector3 position,
        Vector3 target,
        float aspectRatio,
        float fovDegrees = 60f,
        float nearPlane = 0.1f,
        float farPlane = 1000f)
    {
        _position = position;
        _target = target;
        _up = Vector3.UnitY;
        _aspectRatio = Math.Max(0.001f, aspectRatio);
        _fieldOfView = fovDegrees * (MathF.PI / 180f);
        _nearPlane = nearPlane;
        _farPlane = farPlane;
        _dirty = true;
        UpdateMatrices();
    }

    /// <summary>
    /// Orbits the camera around the target point.
    /// </summary>
    /// <param name="yawDelta">Horizontal angle change in radians.</param>
    /// <param name="pitchDelta">Vertical angle change in radians.</param>
    /// <param name="zoomDelta">Distance delta.</param>
    public void Orbit(float yawDelta, float pitchDelta, float zoomDelta)
    {
        var offset = _position - _target;
        float radius = offset.Length();
        if (radius < 0.0001f) radius = 1f;

        float yaw = MathF.Atan2(offset.X, offset.Z);
        float pitch = MathF.Asin(Math.Clamp(offset.Y / radius, -0.999f, 0.999f));

        yaw += yawDelta;
        pitch = Math.Clamp(pitch + pitchDelta, -MathF.PI * 0.48f, MathF.PI * 0.48f);
        radius = Math.Max(0.1f, radius + zoomDelta);

        _position = _target + new Vector3(
            radius * MathF.Cos(pitch) * MathF.Sin(yaw),
            radius * MathF.Sin(pitch),
            radius * MathF.Cos(pitch) * MathF.Cos(yaw));

        _dirty = true;
    }

    private void UpdateMatrices()
    {
        if (!_dirty) return;

        // Direct3D 11 is Left-Handed NDC [0, 1]
        _viewMatrix = Matrix4x4.CreateLookAtLeftHanded(_position, _target, _up);
        _projectionMatrix = Matrix4x4.CreatePerspectiveFieldOfViewLeftHanded(_fieldOfView, _aspectRatio, _nearPlane, _farPlane);
        _viewProjectionMatrix = _viewMatrix * _projectionMatrix;

        _dirty = false;
    }
}
