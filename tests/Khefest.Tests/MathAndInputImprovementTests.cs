using System.Numerics;
using Khefest.Graphics.Mathematics;
using Khefest.Input;
using Xunit;

namespace Khefest.Tests;

public sealed class MathAndInputImprovementTests
{
    [Fact]
    public void MathHelper_AngleConversionsAndClamping()
    {
        float rad = MathHelper.ToRadians(180f);
        Assert.Equal(MathF.PI, rad, 4);

        float deg = MathHelper.ToDegrees(MathF.PI);
        Assert.Equal(180f, deg, 4);

        float clamped = MathHelper.Clamp(15f, 0f, 10f);
        Assert.Equal(10f, clamped);

        float lerped = MathHelper.Lerp(10f, 20f, 0.5f);
        Assert.Equal(15f, lerped);
    }

    [Fact]
    public void BoundingBox_ContainsAndIntersects()
    {
        var box1 = new BoundingBox(new Vector3(-1, -1, -1), new Vector3(1, 1, 1));
        var box2 = new BoundingBox(new Vector3(0.5f, 0.5f, 0.5f), new Vector3(2, 2, 2));
        var box3 = new BoundingBox(new Vector3(5, 5, 5), new Vector3(6, 6, 6));

        Assert.True(box1.Contains(Vector3.Zero));
        Assert.False(box1.Contains(new Vector3(2, 0, 0)));

        Assert.True(box1.Intersects(box2));
        Assert.False(box1.Intersects(box3));

        var merged = BoundingBox.CreateMerged(box1, box3);
        Assert.Equal(new Vector3(-1, -1, -1), merged.Min);
        Assert.Equal(new Vector3(6, 6, 6), merged.Max);
    }

    [Fact]
    public void BoundingFrustum_CullingIntersections()
    {
        // Perspective matrix looking down -Z
        var view = Matrix4x4.CreateLookAt(new Vector3(0, 0, 5), Vector3.Zero, Vector3.UnitY);
        var proj = Matrix4x4.CreatePerspectiveFieldOfView(MathHelper.ToRadians(60f), 1.0f, 0.1f, 100f);
        var vp = view * proj;

        var frustum = new BoundingFrustum(vp);

        // Object in front of camera
        var inFrontBox = new BoundingBox(new Vector3(-1, -1, -1), new Vector3(1, 1, 1));
        Assert.True(frustum.Intersects(inFrontBox));

        // Object far behind camera
        var behindBox = new BoundingBox(new Vector3(0, 0, 10), new Vector3(1, 1, 12));
        Assert.False(frustum.Intersects(behindBox));
    }

    [Fact]
    public void InputManager_Events_CharTypedAndKeyEventsFire()
    {
        var input = new InputManager();

        char typedChar = '\0';
        Key pressedKey = Key.None;
        Key releasedKey = Key.None;

        input.CharTyped += c => typedChar = c;
        input.KeyDown += k => pressedKey = k;
        input.KeyUp += k => releasedKey = k;

        input.OnKeyDown(Key.Space);
        Assert.Equal(Key.Space, pressedKey);
        Assert.True(input.IsKeyDown(Key.Space));

        input.OnChar('A');
        Assert.Equal('A', typedChar);

        input.OnKeyUp(Key.Space);
        Assert.Equal(Key.Space, releasedKey);
        Assert.False(input.IsKeyDown(Key.Space));
    }
}
