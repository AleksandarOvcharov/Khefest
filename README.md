# Khefest (Хефест)

[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/Platform-Windows%20x64-0078D6?logo=windows)](https://microsoft.com/windows)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![NuGet Version](https://img.shields.io/badge/NuGet-1.6.2-blue)](artifacts/packages)
[![Tests](https://img.shields.io/badge/Tests-112%20Passed-brightgreen)](tests/Khefest.Tests)

> **"Simple by default. Powerful when needed."**

Named after **Hephaestus (Хефест)**, the Greek god of forging and craftsmanship, **Khefest** is a modular C# graphics and game engine engineered for serious 2D and 3D games and graphical applications. It combines approachable, intuitive high-level APIs with an uncompromised, zero-overhead low-level graphics and hardware foundation.

Khefest runs on **.NET 10** and targets Windows natively using direct Win32 and Direct3D interop without heavy external middleware or third-party wrappers.

---

## 🏛️ Architecture Overview

The repository is organized cleanly into modular subsystems located under [`src/`](src):

```text
Khefest/
├── src/
│   ├── Khefest/                    # Unified canonical metapackage (dotnet add package Khefest)
│   ├── Khefest.Core/               # Base types, logging, error handling, config, resources, plugins
│   ├── Khefest.Windowing/          # Window and display abstraction contracts
│   ├── Khefest.Input/              # Keyboard, mouse, button, and scroll state management
│   ├── Khefest.Graphics.LowLevel/  # Bespoke GPU device abstraction & native Direct3D 11 backend
│   ├── Khefest.Graphics/           # 2D/3D renderers, SpriteBatch, fonts, cameras, RenderTarget2D, PostProcessEffect
│   ├── Khefest.UI/                 # Visual widget hierarchy, layout engine, and theme system
│   └── Khefest.Windows/            # Win32 platform layer, message loop, high-precision timing, Game loop
├── templates/
│   └── Khefest.Template/           # Official 'dotnet new khefest' project template
├── samples/
│   ├── MyGame/                     # Standalone consumer verification game testing external public API usability
│   └── NuGetConsumer/              # Pure NuGet package consumer project with zero ProjectReference elements
├── examples/
│   ├── HelloWindow/                # Basic Win32 window and event pump
│   ├── HelloTriangle/              # Low-level pipeline and custom vertex shader rendering
│   ├── Hello2D/                    # 2D SpriteBatch, camera pan/zoom/rotation, fonts, and HUD
│   ├── Hello3D/                    # 3D textured mesh, directional lighting, and perspective orbit camera
│   ├── HelloUI/                    # Interactive UI showcase (widgets, live typing, sliders, dark/light theme)
│   ├── StressTest2D/               # StarSwarm 2D particle swarm & quad-buffer stress test
│   ├── CosmicVanguard3D/           # Space flight simulator with 3,000 orbiting bodies and frustum culling
│   └── RetroTankArena/             # Full playable combat game with custom HLSL CRT post-processing
├── tests/
│   └── Khefest.Tests/              # Automated headless test suite (112 tests across all milestones)
└── docs/                           # In-depth architectural specifications and user guides
```

---

## ✨ Subsystem Highlights

### 🎨 Two-Tier Graphics Architecture

Khefest **owns** the graphics abstraction. Direct3D 11 is strictly an internal Windows driver implementation; neither high-level game code nor low-level graphics code exposes DirectX COM pointers or vendor types, making the backend completely swappable:

```text
                    Khefest
                       │
          ┌────────────┴────────────┐
          │                         │
       High-Level                Low-Level
          API                       API
          │                         │
    ┌─────┴─────┐             ┌─────┴─────┐
    │           │             │           │
   2D          3D       GPU resources    Commands
    │           │             │           │
 SpriteBatch  MeshRenderer  IGpuDevice       ICommandRecorder
 Camera2D     Perspective   IGpuBuffer       IPipeline
 RenderTarget2D Camera      IGpuTexture      RenderPassDesc
 PostProcessEffect          ISwapChain       ScopedRenderPass
    │           │             │           │
    └─────┬─────┘             └─────┬─────┘
          │                         │
          └────────────┬────────────┘
                       │
             Khefest GpuDevice
                       │
             Internal Driver Layer
                       │
                Direct3D 11
```

* **Seamless Dropdown**: Use high-level `SpriteBatch` or `MeshRenderer` for standard entities, then drop straight down to `IGpuBuffer`, `IPipeline`, and `ICommandRecorder` to execute custom compute, instanced drawing, or HLSL post-processing shaders on the exact same frame.
* **RenderTarget2D**: Offscreen render targets with dynamic window resize awareness (`Resize(width, height)`), automatic underlying texture reallocation, and pass creation helpers (`CreatePass()`). Exposes `CanonicalTexture` for direct polymorphic binding into command recorders.
* **PostProcessEffect**: Procedural fullscreen triangle post-processing (`SV_VertexID`) that eliminates manual vertex buffer allocations, vertex layouts, and input assembly. Supports zero-allocation uniform buffer updates via `SetUniforms<T>(in T value)`.
* **Zero-Allocation Buffer Uploads**: Uniform and constant buffers update directly via `IGpuBuffer.SetData<T>(in T value)`.
* **Scoped Render Passes**: Deterministic RAII render pass boundaries via `recorder.BeginScopedPass(in passDesc)`.
* **Dynamic Batching**: `SpriteBatch` features auto-growing quad buffers, dynamic geometry generation, texture sorting (`Deferred` or `Immediate`), and font rendering.
* **3D Geometry & Culling**: Built-in procedural primitives (`Cube`, `Sphere`, `Quad`), Blinn-Phong lighting, and 6-plane `BoundingFrustum` culling against axis-aligned `BoundingBox` and `BoundingSphere`.

---

### 🖥️ High-Precision Windows & Input Subsystem

* **Raw Win32 Windowing**: Native `CreateWindowExW` / `DefWindowProcW` integration with zero external windowing dependencies.
* **Deterministic Input**: Direct query of keyboard keys (`Input.IsKeyDown(Key.W)`), mouse buttons (`Input.IsMouseButtonDown(MouseButton.Left)`), and vector positions via `Input.MousePosition` / `Input.MouseDelta` with tuple deconstruction support: `var (x, y) = Input.MousePosition;`.
* **Character Typing**: Pinned native `WM_CHAR` message dispatching directly into the UI text subsystem.
* **High-Precision Timing**: Microsecond-accurate `QueryPerformanceCounter` loop delivering `GameTime.DeltaTime` in fractional seconds.

---

### 🧩 UI Engine & Plugins

* **Hierarchical UI Layout**: WPF/Flutter-style two-pass layout system (`Measure` and `Arrange`) supporting `Canvas` and `StackPanel`.
* **Interactive Widgets**: Built-in `Button`, `Label`, `TextBox` (with cursor navigation and typing), `Slider`, `ProgressBar`, and `CheckBox`.
* **Pluggable Architecture**: Dynamically load and unload isolated extension assemblies using `PluginLoadContext` and topological dependency resolution.

---

## 📦 Installation & Getting Started

### Option A — Project Template (`dotnet new khefest`)
Install the official project template to scaffold and launch a working game in seconds:

```bash
# 1. Install template package
dotnet new install Khefest.Templates

# 2. Create a new game
dotnet new khefest -n MyGame

# 3. Run
cd MyGame
dotnet run
```

---

### Option B — Existing / New .NET Project
If you prefer adding Khefest manually to a new or existing .NET project, follow this 6-step workflow:

#### 1. Create a Windows .NET 10 project
```bash
dotnet new console -n MyGame
cd MyGame
```

#### 2. Set TargetFramework to `net10.0-windows`
Ensure your `MyGame.csproj` specifies `net10.0-windows` and `WinExe`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>preview</LangVersion>
  </PropertyGroup>
</Project>
```

#### 3. Add Khefest
Install the canonical metapackage via the .NET CLI:
```bash
dotnet add package Khefest --version 1.6.2
```
*(Or install granular modules as needed: `Khefest.Core`, `Khefest.Graphics`, `Khefest.Input`, `Khefest.Windows`, `Khefest.UI`)*

#### 4. Create a Game subclass
Create `Game.cs` inheriting from `Game` and implement the lifecycle methods (`Initialize`, `Update`, `Render`, `Shutdown`):
```csharp
using System.Numerics;
using Khefest.Graphics.Camera;
using Khefest.Graphics.LowLevel;
using Khefest.Graphics.TwoD;
using Khefest.Input;
using Khefest.Windows.Application;
using Khefest.Windows.Timing;

namespace MyGame;

public sealed class MyGame : Game
{
    private SpriteBatch? _spriteBatch;
    private Camera2D? _camera;
    private Vector2 _position = Vector2.Zero;

    public override void Initialize()
    {
        _spriteBatch = new SpriteBatch(GpuDevice);
        _camera = new Camera2D(Window.Width, Window.Height);
    }

    public override void OnResize(int width, int height)
    {
        _camera?.Resize(width, height);
    }

    public override void Update(GameTime gameTime)
    {
        if (Input.IsKeyDown(Key.Escape))
            Exit();

        float dt = gameTime.DeltaTime;
        float speed = 350f;

        if (Input.IsKeyDown(Key.W) || Input.IsKeyDown(Key.Up))    _position.Y -= speed * dt;
        if (Input.IsKeyDown(Key.S) || Input.IsKeyDown(Key.Down))  _position.Y += speed * dt;
        if (Input.IsKeyDown(Key.A) || Input.IsKeyDown(Key.Left))  _position.X -= speed * dt;
        if (Input.IsKeyDown(Key.D) || Input.IsKeyDown(Key.Right)) _position.X += speed * dt;
    }

    public override void Render(GameTime gameTime)
    {
        var backBuffer = SwapChain.CurrentBackBuffer;

        // 1. Scoped clear pass
        var clearPass = new RenderPassDesc
        {
            ColorTargets = [backBuffer],
            ClearColor = new Color4(0.10f, 0.12f, 0.16f, 1.0f),
            ClearColorTarget = true
        };

        using (var cmd = GpuDevice.CreateCommandRecorder())
        {
            using (cmd.BeginScopedPass(clearPass))
            {
                // Scoped pass guarantees EndPass is called even on exceptions
            }
            GpuDevice.Submit(cmd);
        }

        // 2. High-level 2D rendering in world space
        _spriteBatch?.Begin(backBuffer, _camera);

        // Background reference grid & boundary
        for (float x = -600; x <= 600; x += 100)
            _spriteBatch?.DrawLine(new Vector2(x, -350), new Vector2(x, 350), new Color4(0.15f, 0.18f, 0.24f, 1f));
        for (float y = -350; y <= 350; y += 100)
            _spriteBatch?.DrawLine(new Vector2(-600, y), new Vector2(600, y), new Color4(0.15f, 0.18f, 0.24f, 1f));

        _spriteBatch?.DrawRectangle(new Vector2(-600, -350), new Vector2(1200, 700), new Color4(0.3f, 0.4f, 0.5f, 1f), thickness: 2f);

        // Player circle
        _spriteBatch?.FillCircle(_position, 28f, Color4.RoyalBlue);
        _spriteBatch?.DrawCircle(_position, 34f, Color4.White, thickness: 2f);
        _spriteBatch?.End();

        // 3. Screen-space HUD overlay
        _spriteBatch?.Begin(backBuffer, camera: null);
        _spriteBatch?.DrawString("Welcome to Khefest!", new Vector2(24, 24), Color4.Gold, scale: 1.2f);
        _spriteBatch?.DrawString($"FPS: {gameTime.Fps:F0} | Pos: ({_position.X:F0}, {_position.Y:F0}) | Use WASD / Arrows to move", new Vector2(24, 60), Color4.White);
        _spriteBatch?.End();
    }

    public override void Shutdown()
    {
        _spriteBatch?.Dispose();
        base.Shutdown();
    }
}
```

#### 5. Configure KhefestApp
In `Program.cs`, use `KhefestConfigBuilder` to configure window properties and run the game:
```csharp
using Khefest.Core.Configuration;
using Khefest.Windows.Application;

namespace MyGame;

public static class Program
{
    [STAThread]
    public static void Main()
    {
        var config = new KhefestConfigBuilder()
            .ConfigureWindow(w => w with
            {
                Title = "My Khefest Game",
                Width = 1280,
                Height = 720,
                VSync = true
            })
            .Build();

        var game = new MyGame();
        KhefestApp.Run(game, config);
    }
}
```

#### 6. Run
```bash
dotnet run
```

---

## 🎮 Running Sample Games & Benchmarks

Run any of the included sample projects from the CLI:

```bash
# 1. Pure NuGet Consumer Sample (Zero ProjectReferences, Milestone 1.6)
dotnet run --project samples/NuGetConsumer

# 2. External Developer Validation Consumer Sample (Milestone 1.4)
dotnet run --project samples/MyGame

# 3. Retro Tank Arena (Playable combat game with custom HLSL post-processing)
dotnet run --project examples/RetroTankArena

# 4. Cosmic Vanguard 3D (3,000 orbiting bodies with 6-plane frustum culling)
dotnet run --project examples/CosmicVanguard3D

# 5. StarSwarm 2D (20,000+ particle swarm stress test)
dotnet run --project examples/StressTest2D

# 6. Interactive UI Showcase (Widgets, live keyboard typing, dark/light themes)
dotnet run --project examples/HelloUI
```

---

## 🧪 Automated Headless Test Suite

Khefest is tested with a comprehensive headless automated test suite covering batching, spatial mathematics, concurrency, resource lifecycles, API misuse resilience, DX contracts, signature-based API compatibility, and documentation verification:

```bash
dotnet test --nologo
```

```text
Passed!  - Failed: 0, Passed: 112, Skipped: 0, Total: 112
```

---

## 📚 Documentation Index

Explore the detailed architecture and implementation guides in [`docs/`](docs):

* 🧭 [Developer Guide & API Conventions](docs/developer-guide.md)
* 🏷️ [Versioning & API Compatibility Policy](docs/versioning-policy.md)
* 📘 [Getting Started Guide](docs/getting-started.md)
* 🏛️ [Architecture Overview](docs/architecture-overview.md)
* 🎨 [Graphics Engine Guide (2D, 3D & Low-Level)](docs/graphics-guide.md)
* 🖥️ [UI Subsystem Guide](docs/ui-system-guide.md)
* 🔌 [Plugins & Extensions Guide](docs/plugins-and-extensions.md)
* 🔍 [Resource Management & Debugging Guide](docs/resource-management-and-debugging.md)
* 📐 [ADR-001: Windows GPU Path and Renderer Architecture](docs/architecture/ADR-001-Windows-GPU-Path-and-Renderer-Architecture.md)

---

## 📄 License
This project is licensed under the MIT License. See [LICENSE](LICENSE) for details.
