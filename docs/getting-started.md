# Getting Started with Khefest

Welcome to **Khefest (Хефест)**! This guide will walk you through the fundamentals of setting up a project, initializing the engine, handling input, rendering 2D and 3D graphics, creating a UI, and gracefully shutting down.

---

## 📋 System Requirements

* **Operating System**: Windows 10 (Build 19041+) or Windows 11 (x64)
* **SDK**: [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
* **DirectX Support**: Direct3D 11 compatible GPU (Feature Level 11_0+)

---

## 🛠️ Project Setup

To create a new game or application using Khefest:

### 1. Create a .NET 10 Windows Application
```powershell
dotnet new console -n MyKhefestGame -f net10.0-windows
cd MyKhefestGame
```

### 2. Install the Khefest Engine Package
Add the canonical metapackage to your project:
```powershell
dotnet add package Khefest
```

Your `.csproj` will look like:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <LangVersion>preview</LangVersion>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Khefest" Version="1.6.0" />
  </ItemGroup>
</Project>
```

Alternatively, if you prefer installing granular modular packages:
```powershell
dotnet add package Khefest.Core
dotnet add package Khefest.Graphics
dotnet add package Khefest.Input
dotnet add package Khefest.Windows
```

---

## 🎮 The Game Lifecycle

The primary entry point of a Khefest application is the abstract base class [`Game`](../src/Khefest.Windows/Application/Game.cs). It orchestrates initialization, the platform message loop, fixed or variable time stepping, rendering, window resize events, and cleanup.

### Lifecycle Methods
* `Initialize()`: Called once when the window and GPU devices have been created. Allocate your renderers, meshes, UI hierarchies, and textures here.
* `OnResize(int width, int height)`: Called whenever the window is resized. Recreate or resize your cameras and render targets here.
* `Update(GameTime gameTime)`: Called once per frame to update simulation state, physics, and user input.
* `Render(GameTime gameTime)`: Called once per frame to submit draw commands to the swap chain back buffer.
* `Shutdown()`: Called when the game window is closing. Dispose your allocated resources here.

---

## 🚀 Building Your First Application

Create a file named `Program.cs` with the following content:

```csharp
using System.Numerics;
using Khefest.Core.Configuration;
using Khefest.Graphics.Camera;
using Khefest.Graphics.LowLevel;
using Khefest.Graphics.TwoD;
using Khefest.Input;
using Khefest.Windows.Application;
using Khefest.Windows.Timing;

namespace MyKhefestGame;

public sealed class HelloKhefest : Game
{
    private SpriteBatch? _spriteBatch;
    private Camera2D? _camera;
    private Vector2 _playerPos = new(400, 300);

    public override void Initialize()
    {
        // 1. Create a SpriteBatch bound to the GPU device and master resource manager
        _spriteBatch = new SpriteBatch(GpuDevice, Resources);

        // 2. Set up a 2D camera matching the window dimensions
        _camera = new Camera2D(Window.Width, Window.Height);
    }

    public override void OnResize(int width, int height)
    {
        // Keep camera viewport aligned with window dimensions
        _camera?.Resize(width, height);
    }

    public override void Update(GameTime gameTime)
    {
        // Exit on Escape
        if (Input.IsKeyPressed(Key.Escape))
        {
            Exit();
            return;
        }

        float dt = gameTime.DeltaTime;
        float speed = 300f;

        // Smooth keyboard movement
        if (Input.IsKeyDown(Key.W) || Input.IsKeyDown(Key.Up))    _playerPos.Y -= speed * dt;
        if (Input.IsKeyDown(Key.S) || Input.IsKeyDown(Key.Down))  _playerPos.Y += speed * dt;
        if (Input.IsKeyDown(Key.A) || Input.IsKeyDown(Key.Left))  _playerPos.X -= speed * dt;
        if (Input.IsKeyDown(Key.D) || Input.IsKeyDown(Key.Right)) _playerPos.X += speed * dt;
    }

    public override void Render(GameTime gameTime)
    {
        var backBuffer = SwapChain.CurrentBackBuffer;

        // 1. Clear the screen with a clean background color using RAII scoped pass
        var clearPass = new RenderPassDesc
        {
            ColorTargets = [backBuffer],
            ClearColor = new Color4(0.08f, 0.09f, 0.12f, 1.0f),
            ClearColorTarget = true
        };

        using (var recorder = GpuDevice.CreateCommandRecorder())
        {
            using (recorder.BeginScopedPass(clearPass))
            {
                // Scoped pass automatically guarantees EndPass()
            }
            GpuDevice.Submit(recorder);
        }

        // 2. Draw 2D entities using SpriteBatch
        _spriteBatch?.Begin(backBuffer, _camera);

        // Draw Player circle
        _spriteBatch?.FillCircle(_playerPos, 24f, Color4.CornflowerBlue);

        // Draw text
        _spriteBatch?.DrawString(
            "Hello, Khefest!",
            new Vector2(20, 20),
            Color4.White,
            scale: 1.5f
        );

        _spriteBatch?.DrawString(
            $"FPS: {(int)gameTime.Fps} | Pos: ({_playerPos.X:F0}, {_playerPos.Y:F0})",
            new Vector2(20, 50),
            Color4.LimeGreen
        );

        _spriteBatch?.End();
    }

    public override void Shutdown()
    {
        _spriteBatch?.Dispose();
        base.Shutdown();
    }
}

public static class Program
{
    [STAThread]
    public static void Main()
    {
        // Fluent configuration builder
        var config = new KhefestConfigBuilder()
            .ConfigureWindow(w => w with
            {
                Title = "My First Khefest Application",
                Width = 1280,
                Height = 720,
                VSync = true
            })
            .Build();

        var game = new HelloKhefest();
        KhefestApp.Run(game, config);
    }
}
```

---

## 🏃 Running the Application

Execute from your project root:
```powershell
dotnet run
```

You will see a smooth 60+ FPS window with a movable player circle, live FPS counter, and responsive keyboard controls!
