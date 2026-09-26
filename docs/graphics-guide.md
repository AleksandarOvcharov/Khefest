# Graphics Engine Guide

Khefest is architected around a **Two-Tier Graphics Philosophy**:
1. **High-Level API**: Instant productivity for 2D, 3D, and post-processing games without manual GPU pipeline plumbing.
2. **Low-Level API**: Direct, uncompromised control over graphics memory, pipeline state objects, shaders, and command recording.

```text
                    Khefest
                       │
          ┌────────────┴────────────┐
          │                         │
       High-level               Low-level
          API                       API
          │                         │
    ┌─────┴─────┐          ┌────────┴────────┐
    │           │          │                 │
   2D          3D       GPU resources    Commands
    │           │          │                 │
 SpriteBatch MeshRenderer  │                 │
 Camera2D    Perspective   └────────┬────────┘
 RenderTarget2D Camera              │
 PostProcessEffect                  v
                       Direct3D 11 Windows Backend
                       (internal implementation)
                                    │
                                    v
                               Physical GPU
```

> [!NOTE]
> Khefest **owns** the graphics abstraction. Direct3D 11 is strictly an internal, private Windows backend implementation. Neither high-level game code nor low-level graphics code exposes DirectX COM pointers or vendor types, making the backend completely swappable.

---

## 🎨 1. High-Level 2D Graphics

The 2D rendering pipeline centers on [`SpriteBatch`](../src/Khefest.Graphics/2D/SpriteBatch.cs), an optimized quad-batching engine.

### Using `SpriteBatch`
```csharp
// Begin batching to the current back buffer
_spriteBatch.Begin(SwapChain.CurrentBackBuffer, _camera, SpriteSortMode.Deferred);

// 1. Textured Sprites
_spriteBatch.Draw(
    texture: myTexture,
    position: new Vector2(300, 200),
    sourceRectangle: null,
    color: Color4.White,
    rotation: 0.25f,
    origin: new Vector2(32, 32),
    scale: new Vector2(1.5f, 1.5f),
    effects: SpriteEffects.None
);

// 2. Procedural Vector Shapes
_spriteBatch.DrawLine(new Vector2(10, 10), new Vector2(200, 10), Color4.Red, thickness: 2f);
_spriteBatch.DrawRectangle(new Vector2(50, 50), new Vector2(100, 80), Color4.Green, thickness: 2f);
_spriteBatch.FillRectangle(new Vector2(180, 50), new Vector2(100, 80), new Color4(0, 0.8f, 0.4f, 0.7f));
_spriteBatch.DrawCircle(new Vector2(350, 90), radius: 40f, Color4.Yellow, segments: 32);
_spriteBatch.FillCircle(new Vector2(450, 90), radius: 40f, Color4.Orange, segments: 32);

// 3. Text Rendering
_spriteBatch.DrawString("Level 1: The Mines", new Vector2(20, 20), Color4.White, scale: 1.2f);

// Flush and submit batched geometry to the GPU
_spriteBatch.End();
```

### 2D Camera (`Camera2D`)
[`Camera2D`](../src/Khefest.Graphics/Camera/Camera2D.cs) provides 2D transformation matrices with origin centering:
```csharp
var camera = new Camera2D(viewportWidth: 1280, viewportHeight: 720);

camera.Position = new Vector2(playerX, playerY);
camera.Zoom = 1.5f;          // Zoom in 150%
camera.Rotation = 0.05f;     // Camera tilt in radians

// Synchronize camera when the window is resized
camera.Resize(newWidth, newHeight);

// Passed directly into SpriteBatch.Begin()
_spriteBatch.Begin(backBuffer, camera);
```

---

## 🎯 2. Offscreen Rendering (`RenderTarget2D`)

[`RenderTarget2D`](../src/Khefest.Graphics/Texturing/RenderTarget2D.cs) provides dynamic, resize-aware offscreen rendering:

```csharp
// Create a render target with optional depth buffer
var renderTarget = new RenderTarget2D(
    GpuDevice,
    name: "SceneOffscreenRT",
    width: Window.Width,
    height: Window.Height,
    hasDepth: true,
    format: GpuFormat.R8G8B8A8_UNorm,
    manager: Resources);

// Respond to window resize events seamlessly
public override void OnResize(int width, int height)
{
    renderTarget.Resize(width, height);
}

// Render offscreen into the target
var pass = renderTarget.CreatePass(Color4.Black, clearDepth: true);
using (var recorder = GpuDevice.CreateCommandRecorder())
{
    using (recorder.BeginScopedPass(pass))
    {
        _spriteBatch.Begin(renderTarget.ColorTexture, _camera);
        // ... draw game world ...
        _spriteBatch.End();
    }
    GpuDevice.Submit(recorder);
}
```

---

## 📺 3. Screen-Space Post-Processing (`PostProcessEffect`)

[`PostProcessEffect`](../src/Khefest.Graphics/PostProcessing/PostProcessEffect.cs) compiles a custom HLSL pixel shader over an internal, zero-allocation procedural fullscreen triangle (`SV_VertexID` in the vertex shader). It eliminates all vertex buffer, index buffer, and input layout boilerplate:

```csharp
const string CrtShaderHlsl = @"
Texture2D g_Texture : register(t0);
SamplerState g_Sampler : register(s0);

cbuffer EffectUniforms : register(b0)
{
    float Time;
    float VignetteIntensity;
    float ScanlineIntensity;
    float Padding;
};

struct PSInput
{
    float4 Position : SV_POSITION;
    float2 TexCoord : TEXCOORD0;
};

float4 PSMain(PSInput input) : SV_TARGET
{
    float2 uv = input.TexCoord;
    float4 color = g_Texture.Sample(g_Sampler, uv);

    // Vignette
    float d = distance(uv, float2(0.5f, 0.5f));
    color.rgb *= (1.0f - d * VignetteIntensity);

    // Scanlines
    color.rgb *= (1.0f - sin(uv.y * 800.0f + Time * 5.0f) * ScanlineIntensity);

    return color;
}
";

// Create effect
var postFx = new PostProcessEffect(
    GpuDevice,
    name: "CrtEffect",
    pixelShaderSource: CrtShaderHlsl,
    entryPoint: "PSMain",
    blendMode: BlendMode.Opaque,
    manager: Resources);

// Update shader uniforms with zero heap allocations
var uniforms = new EffectUniforms { Time = time, VignetteIntensity = 0.5f, ScanlineIntensity = 0.1f };
postFx.SetUniforms(in uniforms);

// Apply directly from offscreen target to the swapchain back buffer
postFx.Apply(source: renderTarget, destination: SwapChain.CurrentBackBuffer);
```

---

## 🧊 4. High-Level 3D Graphics

Khefest provides a built-in 3D rendering pipeline for meshes, materials, cameras, and lighting.

### Mesh Primitives & Materials
```csharp
// 1. Procedural Primitives
var cubeMesh = MeshPrimitives.CreateCube(GpuDevice, Resources, size: 1.0f);
var sphereMesh = MeshPrimitives.CreateSphere(GpuDevice, Resources, radius: 0.75f, tessellation: 24);

// 2. Material with optional diffuse texture
var material = new Material
{
    DiffuseColor = new Vector4(0.8f, 0.8f, 0.9f, 1.0f),
    DiffuseTexture = myTexture,
    SpecularPower = 32.0f
};

var model = new Model(cubeMesh, material);
```

### 3D Camera (`PerspectiveCamera`)
```csharp
var camera = new PerspectiveCamera(
    fovRadians: MathF.PI / 4f,        // 45 degrees
    aspectRatio: 1280f / 720f,
    nearPlane: 0.1f,
    farPlane: 1000f
)
{
    Position = new Vector3(0, 3, -6),
    Target = Vector3.Zero,
    Up = Vector3.UnitY
};
```

### Lighting & Rendering with `MeshRenderer`
```csharp
var lights = new LightEnvironment
{
    Ambient = new AmbientLight(new Vector3(0.2f, 0.2f, 0.25f)),
    Directional = new DirectionalLight(
        direction: Vector3.Normalize(new Vector3(0.5f, -1.0f, 0.5f)),
        color: new Vector3(1.0f, 0.95f, 0.85f),
        intensity: 1.2f
    )
};

var meshRenderer = new MeshRenderer(GpuDevice, Resources);

// Render mesh with depth-buffering enabled
var worldTransform = Matrix4x4.CreateRotationY(time) * Matrix4x4.CreateTranslation(0, 0, 0);
meshRenderer.DrawMesh(cubeMesh, material, worldTransform, camera, lights, backBuffer, depthBuffer);
```

---

## 📐 5. Spatial Math & Culling

The [`Khefest.Graphics.Mathematics`](../src/Khefest.Graphics/Mathematics) namespace offers 2D and 3D spatial acceleration structures:

* **`Rect2D`**: Axis-aligned 2D rectangle with `Contains(Vector2)`, `Intersects(Rect2D)`, `Intersect(Rect2D)`, `Union(Rect2D)`, `Inflate`, and `Offset`.
* **`MathHelper.LerpAngle`**: Shortest-arc angle interpolation wrapping cleanly across the $-\pi / +\pi$ discontinuity.
* **`BoundingBox`**: 3D axis-aligned bounding box (AABB) with `Contains(Vector3)`, `Intersects(box)`, and `Transform(matrix)`.
* **`BoundingSphere`**: Center and radius bounding volume.
* **`BoundingFrustum`**: Extracted from a combined View-Projection matrix for 6-plane frustum culling:

```csharp
var frustum = new BoundingFrustum(camera.ViewMatrix * camera.ProjectionMatrix);

foreach (var entity in entities)
{
    if (frustum.Intersects(entity.WorldBoundingBox))
    {
        // Entity is visible inside camera view: submit to renderer
        meshRenderer.DrawMesh(entity.Mesh, entity.Material, entity.Transform, ...);
    }
}
```

---

## ⚡ 6. Low-Level Graphics API

For engine developers, custom pipelines, and special rendering techniques, [`Khefest.Graphics.LowLevel`](../src/Khefest.Graphics.LowLevel) provides direct hardware access:

### Core Abstractions
* **`IGpuDevice`**: Hardware device abstraction, buffer/texture factory, shader compiler, and command submitter.
* **`IGpuBuffer`**: Typed GPU memory (Vertex, Index, Uniform, Storage) with zero-allocation `SetData<T>(in T value)`.
* **`IGpuTexture`**: 2D textures, render targets, and depth buffers.
* **`IPipeline`**: Encapsulates vertex shader, pixel shader, optional input layout, blend state, rasterizer state, and primitive topology.
* **`ICommandRecorder`**: Encapsulates render passes, viewport configuration, resource binding, and draw call emission.
* **`ScopedRenderPass`**: Deterministic RAII render pass scope guaranteed to end pass on disposal or exception.

### Low-Level Draw Example
```csharp
// 1. Create Command Recorder
using var recorder = GpuDevice.CreateCommandRecorder();

// 2. Begin Scoped Render Pass (deterministic EndPass() on dispose)
var pass = new RenderPassDesc
{
    ColorTargets = [SwapChain.CurrentBackBuffer],
    ClearColor = new Color4(0.05f, 0.05f, 0.05f, 1.0f),
    ClearColorTarget = true,
    DepthTarget = depthBuffer,
    ClearDepth = 1.0f,
    ClearDepthTarget = true
};

using (recorder.BeginScopedPass(pass))
{
    // 3. Bind Pipeline & Geometry
    recorder.SetPipeline(customPipeline);
    recorder.SetVertexBuffer(0, vertexBuffer);
    recorder.SetIndexBuffer(indexBuffer, IndexFormat.UInt16);
    
    // Zero-allocation uniform update directly from struct
    constantBuffer.SetData(in mvpMatrix);
    recorder.SetUniformBuffer(0, constantBuffer);

    // 4. Draw
    recorder.DrawIndexed(indexCount: 36, startIndex: 0, baseVertex: 0);
}

// 5. Submit commands to GPU
GpuDevice.Submit(recorder);
```
