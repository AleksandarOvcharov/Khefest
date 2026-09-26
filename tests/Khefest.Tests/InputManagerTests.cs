using System.Numerics;
using Khefest.Input;
using Xunit;

namespace Khefest.Tests;

public class InputManagerTests
{
    [Fact]
    public void KeyPressAndRelease_TransitionsCorrectlyAcrossFrames()
    {
        var input = new InputManager();

        Assert.False(input.IsKeyDown(Key.Space));
        Assert.False(input.IsKeyPressed(Key.Space));
        Assert.False(input.IsKeyReleased(Key.Space));

        // Frame 1: Key pressed down
        input.OnKeyDown(Key.Space);
        Assert.True(input.IsKeyDown(Key.Space));
        Assert.True(input.IsKeyPressed(Key.Space));
        Assert.False(input.IsKeyReleased(Key.Space));

        // Frame 2: Next frame begins, key held down
        input.Update();
        Assert.True(input.IsKeyDown(Key.Space));
        Assert.False(input.IsKeyPressed(Key.Space)); // No longer pressed this frame
        Assert.False(input.IsKeyReleased(Key.Space));

        // Frame 3: Key released
        input.OnKeyUp(Key.Space);
        Assert.False(input.IsKeyDown(Key.Space));
        Assert.False(input.IsKeyPressed(Key.Space));
        Assert.True(input.IsKeyReleased(Key.Space));

        // Frame 4: Next frame begins, key still up
        input.Update();
        Assert.False(input.IsKeyDown(Key.Space));
        Assert.False(input.IsKeyPressed(Key.Space));
        Assert.False(input.IsKeyReleased(Key.Space));
    }

    [Fact]
    public void MouseMovementAndDelta_ComputesAccuratelyAcrossFrames()
    {
        var input = new InputManager();

        input.OnMouseMove(100, 200);
        Assert.Equal(new Vector2(100, 200), input.MousePosition);
        Assert.Equal((100, 200), input.MousePositionPixels);

        // Advance frame
        input.Update();

        // Move to (150, 180) -> delta should be (+50, -20)
        input.OnMouseMove(150, 180);
        Assert.Equal(new Vector2(150, 180), input.MousePosition);
        Assert.Equal(new Vector2(50, -20), input.MouseDelta);
        Assert.Equal((150, 180), input.MousePositionPixels);
        Assert.Equal((50, -20), input.MouseDeltaPixels);

        // Advance frame -> delta returns to zero
        input.Update();
        Assert.Equal(Vector2.Zero, input.MouseDelta);
        Assert.Equal((0, 0), input.MouseDeltaPixels);
    }

    [Fact]
    public void MouseScroll_AccumulatesAndResetsOnUpdate()
    {
        var input = new InputManager();

        input.OnMouseScroll(1.0f);
        input.OnMouseScroll(0.5f);

        input.Update();
        Assert.Equal(1.5f, input.ScrollDelta);

        input.Update();
        Assert.Equal(0.0f, input.ScrollDelta);
    }

    [Fact]
    public void MouseButtons_TransitionCorrectly()
    {
        var input = new InputManager();

        input.OnMouseDown(MouseButton.Left);
        Assert.True(input.IsMouseButtonDown(MouseButton.Left));
        Assert.True(input.IsMouseButtonPressed(MouseButton.Left));

        input.Update();
        Assert.True(input.IsMouseButtonDown(MouseButton.Left));
        Assert.False(input.IsMouseButtonPressed(MouseButton.Left));

        input.OnMouseUp(MouseButton.Left);
        Assert.False(input.IsMouseButtonDown(MouseButton.Left));
        Assert.True(input.IsMouseButtonReleased(MouseButton.Left));
    }
}
