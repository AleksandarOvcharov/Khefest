using System.Numerics;
using Khefest.Core.Configuration;
using Khefest.Core.Logging;
using Khefest.Graphics.LowLevel;
using Khefest.Graphics.TwoD;
using Khefest.Input;
using Khefest.UI;
using Khefest.UI.Core;
using Khefest.UI.Panels;
using Khefest.UI.Styling;
using Khefest.UI.Widgets;
using Khefest.Windows.Application;
using Khefest.Windows.Timing;

namespace Khefest.Examples.HelloUI;

public sealed class HelloUIGame : Game
{
    private SpriteBatch? _spriteBatch;
    private UISystem? _uiSystem;
    private int _clickCount;

    public override void Initialize()
    {
        LogManager.GlobalMinimumLevel = LogLevel.Info;

        _spriteBatch = new SpriteBatch(GpuDevice, Resources);

        // Build UI Visual Hierarchy
        var rootPanel = new Canvas();

        var mainCard = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 12,
            Padding = new Thickness(24),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        // 1. Header
        var titleLabel = new Label("Khefest Engine — UI Subsystem")
        {
            HorizontalAlignment = HorizontalAlignment.Center
        };
        mainCard.AddChild(titleLabel);

        // 2. Interactive Click Counter
        var countLabel = new Label("Button Clicks: 0");
        var clickButton = new Button
        {
            Text = "Click Me!",
            Width = 240,
            Height = 36
        };
        clickButton.Clicked += _ =>
        {
            _clickCount++;
            countLabel.Text = $"Button Clicks: {_clickCount}";
        };
        mainCard.AddChild(clickButton);
        mainCard.AddChild(countLabel);

        // 3. Text Input (with real WM_CHAR typing & cursor)
        var nameLabel = new Label("Player Name:");
        var textBox = new TextBox
        {
            Text = "Commander",
            Width = 260,
            Height = 36
        };
        mainCard.AddChild(nameLabel);
        mainCard.AddChild(textBox);

        // 4. Slider & Progress Bar
        var sliderLabel = new Label("Audio Volume: 75%");
        var progressBar = new ProgressBar
        {
            Value = 75,
            Width = 260,
            Height = 20
        };
        var slider = new Slider
        {
            Minimum = 0,
            Maximum = 100,
            Value = 75,
            Width = 260,
            Height = 24
        };
        slider.ValueChanged += (_, val) =>
        {
            progressBar.Value = val;
            sliderLabel.Text = $"Audio Volume: {(int)val}%";
        };
        mainCard.AddChild(sliderLabel);
        mainCard.AddChild(slider);
        mainCard.AddChild(progressBar);

        // 5. CheckBox
        var vsyncCheckbox = new CheckBox
        {
            Text = "Enable V-Sync Throttling",
            IsChecked = true
        };
        mainCard.AddChild(vsyncCheckbox);

        // 6. Theme Toggle Button
        var themeButton = new Button
        {
            Text = "Toggle Theme (Dark / Light)",
            Width = 260,
            Height = 36
        };
        themeButton.Clicked += _ =>
        {
            if (_uiSystem != null)
            {
                _uiSystem.Theme = _uiSystem.Theme == UITheme.Dark ? UITheme.Light : UITheme.Dark;
            }
        };
        mainCard.AddChild(themeButton);

        rootPanel.AddChild(mainCard);

        // Initialize UI System attached to input
        _uiSystem = new UISystem(rootPanel, UITheme.Dark, Input);
    }

    public override void Update(GameTime gameTime)
    {
        if (Input.IsKeyPressed(Key.Escape))
        {
            Exit();
            return;
        }

        var dt = (float)gameTime.ElapsedTime.TotalSeconds;
        var viewport = new Vector2(Window.ClientWidth, Window.ClientHeight);

        // Process mouse continuous state & update UI layout
        _uiSystem?.ProcessInput(Input);
        _uiSystem?.Update(dt, viewport);
    }

    public override void Render(GameTime gameTime)
    {
        if (_spriteBatch == null || _uiSystem == null)
            return;

        var backBuffer = SwapChain.CurrentBackBuffer;

        var clearColor = _uiSystem.Theme == UITheme.Light
            ? new Color4(0.92f, 0.93f, 0.95f, 1.0f)
            : new Color4(0.10f, 0.10f, 0.12f, 1.0f);

        var clearPass = new RenderPassDesc
        {
            ColorTarget = backBuffer,
            ClearColor = clearColor,
            ClearColorTarget = true
        };

        var clearRecorder = GpuDevice.CreateCommandRecorder();
        clearRecorder.BeginPass(clearPass);
        clearRecorder.EndPass();
        GpuDevice.Submit(clearRecorder);
        clearRecorder.Dispose();

        _spriteBatch.Begin(backBuffer);
        _uiSystem.Render(_spriteBatch);
        _spriteBatch.End();
    }

    public override void Shutdown()
    {
        _uiSystem?.Dispose();
        _spriteBatch?.Dispose();
        base.Shutdown();
    }
}

public static class Program
{
    [STAThread]
    public static void Main()
    {
        var config = new KhefestConfigBuilder()
            .ConfigureWindow(w => w with
            {
                Title = "Khefest UI Showcase",
                Width = 1024,
                Height = 768,
                VSync = true
            })
            .Build();

        var game = new HelloUIGame();
        KhefestApp.Run(game, config);
    }
}
