namespace Khefest.Graphics.Mathematics;

/// <summary>
/// Common mathematical utility functions and angle conversions.
/// </summary>
public static class MathHelper
{
    public const float Pi = System.MathF.PI;
    public const float TwoPi = System.MathF.PI * 2.0f;
    public const float PiOver2 = System.MathF.PI / 2.0f;
    public const float PiOver4 = System.MathF.PI / 4.0f;

    public static float ToRadians(float degrees) => degrees * (Pi / 180.0f);
    public static float ToDegrees(float radians) => radians * (180.0f / Pi);

    public static float Clamp(float value, float min, float max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }

    public static float Lerp(float value1, float value2, float amount)
    {
        return value1 + (value2 - value1) * amount;
    }

    /// <summary>
    /// Linearly interpolates between two angles in radians along the shortest arc, returning a wrapped angle in (-Pi, Pi].
    /// </summary>
    public static float LerpAngle(float from, float to, float amount)
    {
        float diff = WrapAngle(to - from);
        return WrapAngle(from + diff * Clamp(amount, 0f, 1f));
    }

    public static float SmoothStep(float edge0, float edge1, float x)
    {
        float t = Clamp((x - edge0) / (edge1 - edge0), 0.0f, 1.0f);
        return t * t * (3.0f - 2.0f * t);
    }

    public static float WrapAngle(float angle)
    {
        angle = System.MathF.IEEERemainder(angle, TwoPi);
        if (angle <= -Pi) angle += TwoPi;
        else if (angle > Pi) angle -= TwoPi;
        return angle;
    }
}
