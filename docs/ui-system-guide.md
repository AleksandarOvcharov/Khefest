# UI Subsystem Guide

[`Khefest.UI`](../src/Khefest.UI) provides a lightweight, retained-mode visual element system tailored for games, in-game menus, HUDs, and interactive tooling. It utilizes a two-pass layout system and renders entirely through [`SpriteBatch`](../src/Khefest.Graphics/2D/SpriteBatch.cs) for maximum performance.

---

## 📐 Layout Architecture

The layout lifecycle is separated into two recursive passes:

1. **Measure Pass (`Measure(availableSize)`)**:
   * Each widget determines its desired dimensions given the bounding size constraints of its parent.
   * Considers `Margin`, `Width`, `Height`, and children sizes.
2. **Arrange Pass (`Arrange(finalRect)`)**:
   * Parents position their children by assigning final geometric bounds (`Bounds`).
   * Honors `HorizontalAlignment` (`Left`, `Center`, `Right`, `Stretch`) and `VerticalAlignment` (`Top`, `Center`, `Bottom`, `Stretch`).

---

## 🗂️ Layout Containers

### `StackPanel`
Arranges child elements sequentially in a single line, either vertically or horizontally:

```csharp
var menuStack = new StackPanel
{
    Orientation = Orientation.Vertical,
    Spacing = 10,                          // Pixels between items
    Padding = new Thickness(16),           // Internal padding
    HorizontalAlignment = HorizontalAlignment.Center,
    VerticalAlignment = VerticalAlignment.Center
};

menuStack.AddChild(new Label("Main Menu"));
menuStack.AddChild(playButton);
menuStack.AddChild(exitButton);
```

### `Canvas`
Allows explicit pixel placement of child elements using `Left` and `Top` coordinates:

```csharp
var hudCanvas = new Canvas();

var healthBar = new ProgressBar { Width = 200, Height = 20, Left = 20, Top = 20 };
var miniMap = new Panel { Width = 150, Height = 150, Left = 1050, Top = 20 };

hudCanvas.AddChild(healthBar);
hudCanvas.AddChild(miniMap);
```

---

## 🔘 Built-in Widgets

### 1. `Button`
Supports idle, hover, and pressed states with event dispatching:
```csharp
var startButton = new Button
{
    Text = "Start Game",
    Width = 200,
    Height = 40
};

startButton.Clicked += btn =>
{
    Console.WriteLine("Button pressed!");
};
```

### 2. `TextBox`
Interactive single-line text input with blinking caret and native keyboard typing:
```csharp
var nameField = new TextBox
{
    Text = "Hero",
    Width = 250,
    Height = 36
};

nameField.TextChanged += (sender, newText) =>
{
    Console.WriteLine($"Name changed to: {newText}");
};
```

### 3. `Slider` & `ProgressBar`
```csharp
var volumeSlider = new Slider
{
    Minimum = 0,
    Maximum = 100,
    Value = 80,
    Width = 220,
    Height = 24
};

var volumeProgress = new ProgressBar
{
    Value = 80,
    Width = 220,
    Height = 16
};

volumeSlider.ValueChanged += (_, val) =>
{
    volumeProgress.Value = val;
};
```

### 4. `CheckBox`
```csharp
var vsyncCheck = new CheckBox
{
    Text = "Enable V-Sync",
    IsChecked = true
};

vsyncCheck.CheckedChanged += (_, isChecked) =>
{
    Console.WriteLine($"V-Sync toggled: {isChecked}");
};
```

---

## 🎨 Theming (`UITheme`)

The UI system provides built-in dark and light themes, or custom color palettes:

```csharp
// Switch theme at runtime
_uiSystem.Theme = UITheme.Light;

// Custom Theme
var customTheme = new UITheme
{
    Background = new Color4(0.12f, 0.12f, 0.15f, 1.0f),
    CardBackground = new Color4(0.18f, 0.18f, 0.22f, 1.0f),
    Primary = new Color4(0.2f, 0.6f, 1.0f, 1.0f),
    Text = Color4.White,
    Border = new Color4(0.3f, 0.3f, 0.35f, 1.0f)
};
_uiSystem.Theme = customTheme;
```

---

## 🚀 Integrating with `Game`

```csharp
public class MyGame : Game
{
    private UISystem? _uiSystem;
    private SpriteBatch? _spriteBatch;

    public override void Initialize()
    {
        _spriteBatch = new SpriteBatch(GpuDevice, Resources);

        var root = new Canvas();
        // ... build UI tree ...

        // Pass Input directly to hook keyboard and mouse events
        _uiSystem = new UISystem(root, UITheme.Dark, Input);
    }

    public override void Update(GameTime gameTime)
    {
        var viewport = new Vector2(Window.ClientWidth, Window.ClientHeight);
        _uiSystem?.ProcessInput(Input);
        _uiSystem?.Update(gameTime.DeltaTimeSeconds, viewport);
    }

    public override void Render(GameTime gameTime)
    {
        _spriteBatch?.Begin(SwapChain.CurrentBackBuffer);
        _uiSystem?.Render(_spriteBatch!);
        _spriteBatch?.End();
    }

    public override void Shutdown()
    {
        _uiSystem?.Dispose();
        _spriteBatch?.Dispose();
    }
}
```
