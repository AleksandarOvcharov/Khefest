using System.Numerics;
using Khefest.Input;
using Khefest.UI;
using Khefest.UI.Core;
using Khefest.UI.Panels;
using Khefest.UI.Widgets;
using Xunit;

namespace Khefest.Tests;

public sealed class Phase9UISystemTests
{
    [Fact]
    public void Widget_HierarchyAndLayout_MeasuresAndArrangesCorrectly()
    {
        var stack = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 10f,
            Padding = new Thickness(5f),
            Width = 200f
        };

        var label = new Label("Header Text") { Height = 20f };
        var button = new Button("Click Me") { Height = 30f };
        var progress = new ProgressBar(0.5f) { Height = 10f };

        stack.AddChild(label);
        stack.AddChild(button);
        stack.AddChild(progress);

        Assert.Equal(3, stack.Children.Count);
        Assert.Equal(stack, label.Parent);

        // Measure with available 800x600
        var desired = stack.Measure(new Vector2(800, 600));
        Assert.True(desired.X >= 200f);
        // Desired height: 20 + 30 + 10 + 2*10 (spacing) + 10 (padding) = 90
        Assert.Equal(90f, desired.Y);

        // Arrange into 0,0, 200, 300
        stack.Arrange(new UIRect(0, 0, 200, 300));

        Assert.Equal(5f, label.Bounds.X);
        Assert.Equal(5f, label.Bounds.Y);
        Assert.Equal(20f, label.Bounds.Height);

        Assert.Equal(5f, button.Bounds.X);
        Assert.Equal(35f, button.Bounds.Y); // 5 + 20 + 10
        Assert.Equal(30f, button.Bounds.Height);

        Assert.Equal(5f, progress.Bounds.X);
        Assert.Equal(75f, progress.Bounds.Y); // 35 + 30 + 10
        Assert.Equal(10f, progress.Bounds.Height);
    }

    [Fact]
    public void Canvas_AbsolutePositioning_PositionsChildrenAccurately()
    {
        var canvas = new Canvas();
        var btn1 = new Button("Btn1") { Width = 80f, Height = 30f };
        var btn2 = new Button("Btn2") { Width = 100f, Height = 40f };

        Canvas.SetPosition(btn1, new Vector2(50f, 60f));
        Canvas.SetPosition(btn2, new Vector2(200f, 150f));

        canvas.AddChild(btn1);
        canvas.AddChild(btn2);

        canvas.Measure(new Vector2(1000, 1000));
        canvas.Arrange(new UIRect(0, 0, 1000, 1000));

        Assert.Equal(50f, btn1.Bounds.X);
        Assert.Equal(60f, btn1.Bounds.Y);
        Assert.Equal(80f, btn1.Bounds.Width);
        Assert.Equal(30f, btn1.Bounds.Height);

        Assert.Equal(200f, btn2.Bounds.X);
        Assert.Equal(150f, btn2.Bounds.Y);
        Assert.Equal(100f, btn2.Bounds.Width);
        Assert.Equal(40f, btn2.Bounds.Height);
    }

    [Fact]
    public void HitTesting_FindsTopmostVisibleInteractiveWidget()
    {
        var canvas = new Canvas();
        var panel = new Canvas { Width = 300f, Height = 200f };
        var button = new Button("Click") { Width = 100f, Height = 40f };

        Canvas.SetPosition(panel, new Vector2(10, 10));
        Canvas.SetPosition(button, new Vector2(20, 20));

        panel.AddChild(button);
        canvas.AddChild(panel);

        canvas.Measure(new Vector2(800, 600));
        canvas.Arrange(new UIRect(0, 0, 800, 600));

        // 1. Inside button (at 35, 35) -> finds button
        var hitBtn = canvas.HitTest(new Vector2(35, 35));
        Assert.Equal(button, hitBtn);

        // 2. Inside panel but outside button (at 200, 100) -> finds panel
        var hitPanel = canvas.HitTest(new Vector2(200, 100));
        Assert.Equal(panel, hitPanel);

        // 3. Outside panel (at 500, 500) -> returns canvas (since canvas covers 800x600)
        var hitCanvas = canvas.HitTest(new Vector2(500, 500));
        Assert.Equal(canvas, hitCanvas);

        // 4. Outside all bounds -> returns null
        var hitNull = canvas.HitTest(new Vector2(-10, -10));
        Assert.Null(hitNull);

        // 5. Invisible button -> falls back to panel
        button.IsVisible = false;
        var hitInvisible = canvas.HitTest(new Vector2(35, 35));
        Assert.Equal(panel, hitInvisible);
    }

    [Fact]
    public void Button_ClickEvent_FiresOnPointerRelease()
    {
        var canvas = new Canvas();
        var button = new Button("Press Me") { Width = 100f, Height = 40f };
        Canvas.SetPosition(button, new Vector2(50, 50));
        canvas.AddChild(button);

        var ui = new UISystem(canvas);
        ui.Update(0.016f, new Vector2(800, 600));

        bool clicked = false;
        button.Clicked += _ => clicked = true;

        // Hover over button
        ui.HandlePointerMove(new Vector2(60, 60));
        Assert.True(button.IsHovered);

        // Press down
        ui.HandlePointerDown(new Vector2(60, 60), MouseButton.Left);
        Assert.True(button.IsPressed);
        Assert.False(clicked);

        // Release
        ui.HandlePointerUp(new Vector2(60, 60), MouseButton.Left);
        Assert.False(button.IsPressed);
        Assert.True(clicked);
    }

    [Fact]
    public void CheckBox_ToggleState_FiresCheckedChanged()
    {
        var canvas = new Canvas();
        var checkBox = new CheckBox("Enable VSync", isChecked: false) { Width = 150f, Height = 24f };
        Canvas.SetPosition(checkBox, new Vector2(10, 10));
        canvas.AddChild(checkBox);

        var ui = new UISystem(canvas);
        ui.Update(0.016f, new Vector2(800, 600));

        bool eventFired = false;
        bool lastState = false;
        checkBox.CheckedChanged += (_, isChecked) =>
        {
            eventFired = true;
            lastState = isChecked;
        };

        // First click -> toggles to true
        ui.HandlePointerMove(new Vector2(15, 15));
        ui.HandlePointerDown(new Vector2(15, 15), MouseButton.Left);
        ui.HandlePointerUp(new Vector2(15, 15), MouseButton.Left);

        Assert.True(checkBox.IsChecked);
        Assert.True(eventFired);
        Assert.True(lastState);

        // Second click -> toggles to false
        eventFired = false;
        ui.HandlePointerDown(new Vector2(15, 15), MouseButton.Left);
        ui.HandlePointerUp(new Vector2(15, 15), MouseButton.Left);

        Assert.False(checkBox.IsChecked);
        Assert.True(eventFired);
        Assert.False(lastState);
    }

    [Fact]
    public void Slider_DragInteraction_UpdatesValueWithinRange()
    {
        var canvas = new Canvas();
        var slider = new Slider(minimum: 0f, maximum: 100f, value: 0f)
        {
            Width = 200f,
            Height = 30f,
            ThumbWidth = 20f
        };
        Canvas.SetPosition(slider, new Vector2(0, 0));
        canvas.AddChild(slider);

        var ui = new UISystem(canvas);
        ui.Update(0.016f, new Vector2(800, 600));

        float recordedValue = 0f;
        slider.ValueChanged += (_, val) => recordedValue = val;

        // Click halfway across the available track (track is 200 - 20 = 180, midpoint is 10 + 90 = 100)
        ui.HandlePointerMove(new Vector2(100, 15));
        ui.HandlePointerDown(new Vector2(100, 15), MouseButton.Left);

        Assert.InRange(slider.Value, 48f, 52f);
        Assert.Equal(slider.Value, recordedValue);

        // Drag to right edge
        ui.HandlePointerMove(new Vector2(250, 15));
        Assert.Equal(100f, slider.Value);

        // Drag to negative left
        ui.HandlePointerMove(new Vector2(-50, 15));
        Assert.Equal(0f, slider.Value);
    }

    [Fact]
    public void TextBox_TypingAndEditing_UpdatesTextAndCaret()
    {
        var canvas = new Canvas();
        var textBox = new TextBox("") { Width = 200f, Height = 30f };
        Canvas.SetPosition(textBox, new Vector2(20, 20));
        canvas.AddChild(textBox);

        var ui = new UISystem(canvas);
        ui.Update(0.016f, new Vector2(800, 600));

        // Click to focus
        ui.HandlePointerMove(new Vector2(30, 30));
        ui.HandlePointerDown(new Vector2(30, 30), MouseButton.Left);
        ui.HandlePointerUp(new Vector2(30, 30), MouseButton.Left);

        Assert.Equal(textBox, ui.FocusedWidget);
        Assert.True(textBox.IsFocused);

        // Type "Hello"
        ui.HandleCharInput('H');
        ui.HandleCharInput('e');
        ui.HandleCharInput('l');
        ui.HandleCharInput('l');
        ui.HandleCharInput('o');

        Assert.Equal("Hello", textBox.Text);
        Assert.Equal(5, textBox.CaretIndex);

        // Backspace
        ui.HandleKeyDown(Key.Backspace);
        Assert.Equal("Hell", textBox.Text);
        Assert.Equal(4, textBox.CaretIndex);

        // Move Left twice
        ui.HandleKeyDown(Key.Left);
        ui.HandleKeyDown(Key.Left);
        Assert.Equal(2, textBox.CaretIndex);

        // Insert 'x'
        ui.HandleCharInput('x');
        Assert.Equal("Hexll", textBox.Text);
        Assert.Equal(3, textBox.CaretIndex);

        // Home key
        ui.HandleKeyDown(Key.Home);
        Assert.Equal(0, textBox.CaretIndex);

        // Delete key (deletes first char 'H')
        ui.HandleKeyDown(Key.Delete);
        Assert.Equal("exll", textBox.Text);
    }

    [Fact]
    public void UISystem_FocusManagement_TransitionsCorrectly()
    {
        var canvas = new Canvas();
        var box1 = new TextBox("First") { Width = 100f, Height = 30f };
        var box2 = new TextBox("Second") { Width = 100f, Height = 30f };

        Canvas.SetPosition(box1, new Vector2(10, 10));
        Canvas.SetPosition(box2, new Vector2(10, 50));

        canvas.AddChild(box1);
        canvas.AddChild(box2);

        var ui = new UISystem(canvas);
        ui.Update(0.016f, new Vector2(800, 600));

        // Click box1
        ui.HandlePointerMove(new Vector2(20, 20));
        ui.HandlePointerDown(new Vector2(20, 20), MouseButton.Left);
        ui.HandlePointerUp(new Vector2(20, 20), MouseButton.Left);

        Assert.Equal(box1, ui.FocusedWidget);
        Assert.True(box1.IsFocused);
        Assert.False(box2.IsFocused);

        // Click box2
        ui.HandlePointerMove(new Vector2(20, 60));
        ui.HandlePointerDown(new Vector2(20, 60), MouseButton.Left);
        ui.HandlePointerUp(new Vector2(20, 60), MouseButton.Left);

        Assert.Equal(box2, ui.FocusedWidget);
        Assert.False(box1.IsFocused);
        Assert.True(box2.IsFocused);

        // Click background canvas (non-focusable)
        ui.HandlePointerMove(new Vector2(500, 500));
        ui.HandlePointerDown(new Vector2(500, 500), MouseButton.Left);
        ui.HandlePointerUp(new Vector2(500, 500), MouseButton.Left);

        Assert.Null(ui.FocusedWidget);
        Assert.False(box2.IsFocused);
    }
}
