# Khefest Developer Guide & API Conventions

This guide is designed for developers building games and graphical applications with **Khefest**. It establishes the engine's mental model, API conventions, shader workflow, resource ownership rules, and debugging patterns—enabling any C# developer to work effectively with Khefest without needing to understand its internal Direct3D 11 implementation.

---

## 🧭 1. The Mental Model: Choosing the Right Layer

Khefest is deliberately architected in distinct layers. You do not need to choose between "an engine that does everything for you" or "a low-level API where you write hundreds of lines to draw a quad." You can compose both freely.

```text
+-----------------------------------------------------------------------------------+
|  High-Level Layer (Khefest.Graphics, Khefest.UI)                                  |
|  - SpriteBatch: 2D sprites, fonts, lines, shapes                                   |
|  - RenderTarget2D: Dynamic offscreen render targets with auto-resize              |
|  - PostProcessEffect: Procedural fullscreen triangle HLSL effects                |
|  - MeshRenderer: 3D meshes, materials, Phong lighting                             |
|  - Camera2D & PerspectiveCamera: Viewport transformations                         |
|  - UI System: Visual widgets, layout engine, theming                              |
+-----------------------------------------------------------------------------------+
                                         │
                                         ▼ (Composes)
+-----------------------------------------------------------------------------------+
|  Low-Level Layer (Khefest.Graphics.LowLevel)                                      |
|  - IGpuDevice: Resource factory & command submission                              |
|  - IGpuBuffer: Vertex, index, and uniform buffers with zero-alloc SetData<T>      |
|  - IGpuTexture: 2D textures, render targets, depth-stencil targets                |
|  - IPipeline: Shaders, blend modes, rasterizer states, topologies                |
|  - ICommandRecorder: Render passes, viewports, state binding, draw calls          |
|  - ScopedRenderPass: RAII render pass lifecycle scoping                           |
+-----------------------------------------------------------------------------------+
```

### When to use High-Level:
* Drawing 2D sprites, text, UI, and procedural shapes.
* Rendering 3D models with standard materials and directional/ambient lighting.
* Offscreen rendering with `RenderTarget2D`.
* Screen-space post-processing (e.g. CRT, bloom, blur, color grading) with `PostProcessEffect`.

### When to drop down to Low-Level:
* Custom vertex attributes (e.g. skinning weights, instanced matrices).
* Multi-pass render pipelines (e.g. shadow maps, deferred G-buffers).
* Bespoke compute or custom rasterizer/blend states.
* Direct command recording when maximum control is paramount.

> [!TIP]
> **No Walls Between Layers**: You can render the world using `SpriteBatch` to a `RenderTarget2D`, pass that target to a low-level custom `IPipeline`, and then render an on-screen HUD back using `SpriteBatch` or `Khefest.UI` in the same frame! *Retro Tank Arena* (`examples/RetroTankArena`) demonstrates this exact pattern.

---

## 📦 2. Resource Ownership & Lifecycle Rules

All GPU and engine resources implement deterministic cleanup via `IDisposable`.

### Ownership Principle: "Whoever Creates, Disposes"
1. **Application-Created Resources**: Any texture, buffer, pipeline, or render target created directly in your `Initialize()` method must be disposed in your `Shutdown()` method:
   ```csharp
   public override void Shutdown()
   {
       _customPipeline?.Dispose();
       _offscreenTarget?.Dispose();
       _spriteBatch?.Dispose();
       base.Shutdown();
   }
   ```
2. **Double-Disposal is Always Safe**: All Khefest resources are idempotent—invoking `.Dispose()` multiple times will never throw an exception or corrupt memory.
3. **Engine-Owned Resources**: Do not manually dispose resources provided by the engine context, such as:
   * `SwapChain.CurrentBackBuffer` (managed automatically by the swap chain).
   * `GpuDevice` (managed automatically by `Game`).
   * `Window` (managed automatically by `Game`).

### Leak Detection in Development
In debug/test builds, pass the game's `ResourceManager` to your resources. When the application exits, the `ResourceLifetimeTracker` verifies that all allocated resources were cleanly freed:
```csharp
// If any GPU texture or buffer remains allocated on exit, the tracker
// logs the exact allocation stack trace and resource name!
var leaks = Resources.Tracker.CheckForLeaks();
if (leaks.Count > 0)
{
    Logger.Warn($"Detected {leaks.Count} unreleased resources upon shutdown!");
}
```

---

## 🎨 3. Shader Authoring Workflow

Khefest uses standard High-Level Shading Language (HLSL) compiled at runtime using the internal OS hardware compiler. You do not need to install the DirectX SDK or run offline tools like `fxc.exe`.

### Shader Register Conventions
When authoring HLSL shaders in Khefest, follow these standard slot bindings:

| Register | Purpose | Example |
| :--- | :--- | :--- |
| `register(t0)` ... `t7` | Texture input slots (SRVs) | `Texture2D g_Texture : register(t0);` |
| `register(s0)` ... `s7` | Sampler state slots | `SamplerState g_Sampler : register(s0);` |
| `register(b0)` ... `b7` | Uniform Constant Buffers (CBVs) | `cbuffer Uniforms : register(b0) { ... };` |

### Post-Processing Shaders (Zero-VB Procedural Triangle)
For screen-space effects, you do not need to define vertex buffers or vertex layouts. Use `PostProcessEffect` with a standard pixel shader:

```hlsl
Texture2D g_Texture : register(t0);
SamplerState g_Sampler : register(s0);

cbuffer EffectUniforms : register(b0)
{
    float Time;
    float Intensity;
    float2 Resolution;
};

struct PSInput
{
    float4 Position : SV_POSITION;
    float2 TexCoord : TEXCOORD0;
};

float4 PSMain(PSInput input) : SV_TARGET
{
    float2 uv = input.TexCoord;
    float4 col = g_Texture.Sample(g_Sampler, uv);
    
    // Apply grayscale
    float gray = dot(col.rgb, float3(0.299f, 0.587f, 0.114f));
    col.rgb = lerp(col.rgb, float3(gray, gray, gray), Intensity);
    
    return col;
}
```

In C#:
```csharp
// Compile effect
var effect = new PostProcessEffect(GpuDevice, "GrayscaleFx", shaderSource, "PSMain");

// Update struct uniforms with ZERO heap allocations
effect.SetUniforms(new EffectUniforms { Time = time, Intensity = 0.8f, Resolution = new Vector2(1280, 720) });

// Apply to target
effect.Apply(sourceTarget, destinationTarget);
```

---

## ⚡ 4. Zero-Allocation High-Performance APIs

Khefest is built for 60+ FPS games where garbage collection pauses must be avoided.

### Single-Struct Constant Buffer Updates
Instead of allocating a temporary array or span every frame:
```csharp
// BAD: Allocates array every frame
buffer.SetData(new[] { mvpMatrix });

// GOOD (Khefest 1.3.0+): Zero heap allocations!
buffer.SetData(in mvpMatrix);
```

### RAII Scoped Render Passes
Never leak render pass state or forget to call `EndPass()`:
```csharp
using var recorder = GpuDevice.CreateCommandRecorder();

// If an exception occurs inside the block, EndPass() is still guaranteed!
using (recorder.BeginScopedPass(passDesc))
{
    recorder.SetPipeline(pipeline);
    recorder.Draw(3, 0);
}

GpuDevice.Submit(recorder);
```

---

## 📐 5. Spatial Math & Input Conventions

### Vector2 Canonical Queries
All spatial input queries return standard `System.Numerics.Vector2` values:
```csharp
// Vector2 arithmetic
Vector2 playerPos = ...;
Vector2 aimDirection = Vector2.Normalize(Input.MousePosition - playerPos);

// Tuple deconstruction for float coordinates
var (mouseX, mouseY) = Input.MousePosition;
var (deltaX, deltaY) = Input.MouseDelta;

// Integer pixel coordinates for UI / grid calculations
var (pixelX, pixelY) = Input.MousePositionPixels;
```

### Frame-Rate Independence
Always use `gameTime.DeltaTime` (seconds as `float`):
```csharp
public override void Update(GameTime gameTime)
{
    float dt = gameTime.DeltaTime;
    position += velocity * dt;
}
```

### Shortest-Arc Angle Interpolation (`LerpAngle`)
To rotate a turret or character towards an aim angle smoothly across the $-\pi / +\pi$ boundary:
```csharp
turretAngle = MathHelper.LerpAngle(turretAngle, targetAngle, 10f * dt);
```

### Rect2D Geometry
Use `Rect2D` for bounding boxes, collision checks, and UI viewports:
```csharp
var playerBounds = new Rect2D(playerPos.X - 16, playerPos.Y - 16, 32, 32);
var bulletBounds = new Rect2D(bulletPos.X - 4, bulletPos.Y - 4, 8, 8);

if (playerBounds.Intersects(bulletBounds))
{
    TakeDamage();
}
```

---

## 🛠️ 6. Diagnostics & Debugging

When something goes wrong, Khefest provides explicit, actionable exceptions rather than native driver crashes:

| Exception / Error Code | Typical Cause | Resolution |
| :--- | :--- | :--- |
| `GraphicsCommandOutsideRenderPass` | Calling `Draw`, `SetVertexBuffer`, or `SetTexture` outside `BeginPass` / `EndPass`. | Wrap commands inside `recorder.BeginScopedPass(pass)` or `BeginPass`/`EndPass`. |
| `GraphicsPassAlreadyActive` | Calling `BeginPass` when a pass is already in progress. | End the previous pass with `recorder.EndPass()` before starting a new one. |
| `GraphicsResourceBindingFailed` | Binding a buffer, texture, or pipeline that has been `.Dispose()`d. | Check resource lifetimes; ensure the resource was not disposed prematurely. |
| `ShaderCompilationFailed` | Syntax error in HLSL source code. | Check the exception message for HLSL line numbers and compilation errors reported by the compiler. |
| `ArgumentOutOfRangeException` | Calling `buffer.SetData` with data exceeding the allocated buffer size. | Ensure the target buffer was created with at least `sizeof(T)` bytes. |

---

## 🚀 7. Starter Template: A Complete Working Game in 50 Lines

```csharp
using System;
using System.Numerics;
using Khefest.Core.Configuration;
using Khefest.Graphics.Camera;
using Khefest.Graphics.LowLevel;
using Khefest.Graphics.TwoD;
using Khefest.Input;
using Khefest.Windows.Application;
using Khefest.Windows.Timing;

namespace MyGame;

public sealed class StarterGame : Game
{
    private SpriteBatch? _spriteBatch;
    private Camera2D? _camera;
    private Vector2 _position = Vector2.Zero;

    public override void Initialize()
    {
        _spriteBatch = new SpriteBatch(GpuDevice, Resources);
        _camera = new Camera2D(Window.Width, Window.Height);
    }

    public override void OnResize(int width, int height) => _camera?.Resize(width, height);

    public override void Update(GameTime gameTime)
    {
        if (Input.IsKeyPressed(Key.Escape)) Exit();

        float dt = gameTime.DeltaTime;
        float speed = 400f;

        if (Input.IsKeyDown(Key.W)) _position.Y -= speed * dt;
        if (Input.IsKeyDown(Key.S)) _position.Y += speed * dt;
        if (Input.IsKeyDown(Key.A)) _position.X -= speed * dt;
        if (Input.IsKeyDown(Key.D)) _position.X += speed * dt;
    }

    public override void Render(GameTime gameTime)
    {
        var pass = new RenderPassDesc
        {
            ColorTargets = [SwapChain.CurrentBackBuffer],
            ClearColor = new Color4(0.12f, 0.12f, 0.16f, 1.0f),
            ClearColorTarget = true
        };

        using var recorder = GpuDevice.CreateCommandRecorder();
        using (recorder.BeginScopedPass(pass))
        {
            // Scoped pass guarantees EndPass()
        }
        GpuDevice.Submit(recorder);

        _spriteBatch?.Begin(SwapChain.CurrentBackBuffer, _camera);
        _spriteBatch?.FillCircle(_position, 28f, Color4.CornflowerBlue);
        _spriteBatch?.DrawString("Khefest Starter Game", new Vector2(24, 24), Color4.White, scale: 1.2f);
        _spriteBatch?.DrawString($"FPS: {(int)gameTime.Fps}", new Vector2(24, 56), Color4.LimeGreen);
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
        var config = new KhefestConfigBuilder()
            .ConfigureWindow(w => w with { Title = "Khefest Starter", Width = 1280, Height = 720 })
            .Build();

        var game = new StarterGame();
        KhefestApp.Run(game, config);
    }
}
```
