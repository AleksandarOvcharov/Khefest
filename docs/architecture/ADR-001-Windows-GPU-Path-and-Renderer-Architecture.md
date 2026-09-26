# ADR 001: Windows GPU Communication Path and Khefest Renderer Architecture

## Status
**Accepted & Implemented in Khefest v1.0.0**

## Context & Objectives
The central requirement of Khefest Version 1 is to deliver a modular, high-performance game and graphics framework on Windows using C# with:
1. A simple, approachable high-level API for beginners.
2. An uncompromised, powerful low-level API for advanced developers.
3. No reliance on OpenGL, Vulkan, or external middleware runtimes.
4. An authentic, bespoke Khefest graphics abstraction that **avoids accidentally becoming a thin Direct3D 12 or Vulkan wrapper**.

To guarantee technical feasibility before committing to the renderer implementation, this milestone investigated the Windows GPU driver architecture, analyzed the viability of available pathways, and defined the Khefest GPU abstraction.

---

## 1. Investigation: The Windows GPU Driver Architecture

On Microsoft Windows (from Windows Vista through Windows 10/11 WDDM 1.0 to 3.2), the operating system strictly isolates hardware access through the **Windows Display Driver Model (WDDM)**.

```text
+-------------------------------------------------------------------------+
|                         User Mode Application                           |
|                         (Khefest C# Runtime)                           |
+-------------------------------------------------------------------------+
                                     |
                                     v
+-------------------------------------------------------------------------+
|                  Khefest Native Graphics Abstraction                    |
|           (IGpuDevice, IGpuBuffer, IPipeline, ICommandRecorder)         |
+-------------------------------------------------------------------------+
                                     |
                                     v
+-------------------------------------------------------------------------+
|                      Windows User-Mode GPU Layer                        |
|   - DXGI (dxgi.dll): SwapChain, Output Presentation, Flip Model         |
|   - OS Hardware Driver Interface: Shader bytecode compiler & dispatcher |
+-------------------------------------------------------------------------+
                                     |
                                     v (GDI32 / D3DKMT Thunks)
~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
                             Kernel Boundary
~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
                                     |
                                     v
+-------------------------------------------------------------------------+
|                   DirectX Graphics Kernel (dxgkrnl.sys)                 |
|             (GPU Virtual Memory, Context Scheduling, Arbitration)       |
+-------------------------------------------------------------------------+
                                     |
                                     v
+-------------------------------------------------------------------------+
|                     Vendor Kernel-Mode Driver (KMD)                     |
|           (nvlddmkm.sys / amdkmdag.sys / igdkm64.sys / etc.)           |
+-------------------------------------------------------------------------+
                                     |
                                     v
+-------------------------------------------------------------------------+
|                          Physical GPU Hardware                          |
+-------------------------------------------------------------------------+
```

### 1.1 Can User Mode Bypass OS Driver Facilities via Raw WDDM Thunks?
Windows exposes raw kernel thunks in `gdi32.dll` / `d3dkmthk.h` (`D3DKMTOpenAdapterFromLuid`, `D3DKMTCreateDevice`, `D3DKMTCreateContextVirtual`, `D3DKMTSubmitCommand`, `D3DKMTPresent`).

Our investigation reveals why directly issuing raw `D3DKMTSubmitCommand` from user space without vendor user-mode drivers is **infeasible for a portable 3D game framework**:
1. **Proprietary GPU Instruction Set Architectures (ISA)**: Modern GPUs (NVIDIA Ada/Blackwell, AMD RDNA3/4, Intel Arc Xe/Xe2) do not execute a universal public machine code. Each vendor compiles intermediate shader representation into hardware microcode via closed, proprietary user-mode compiler modules.
2. **Proprietary Command Ring Format**: The command packet structure submitted via `D3DKMTSubmitCommand` is vendor-specific, architecture-specific, and undocumented. Bypassing vendor driver modules would require reverse-engineering and writing bespoke microcode compilers for every GPU generation from every vendor.
3. **OS Desktop Window Manager (DWM) Integration**: Windowed rendering on Windows requires cooperation with the DWM desktop compositor. Direct hardware presentation without DXGI bypasses the DWM flip queue, resulting in severe tearing, window composition failure, and lack of multi-monitor synchronization.

### 1.2 The Role of DXGI (DirectX Graphics Infrastructure)
DXGI (`dxgi.dll`) is the native Windows presentation and adapter management layer. It is built into the Windows OS itself (not an external third-party SDK or middleware).
- DXGI provides direct access to GPU adapters (`IDXGIAdapter`).
- DXGI provides the **Modern Flip Model SwapChain** (`DXGI_SWAP_EFFECT_FLIP_DISCARD` / `DXGI_SWAP_EFFECT_FLIP_SEQUENTIAL`), which bypasses legacy GDI blits and sends backbuffers directly to the DWM compositor for sub-millisecond latency.

---

## 2. Abstraction Ownership vs. Backend Implementation

> [!IMPORTANT]
> **Khefest owns the graphics abstraction.**
> Direct3D 11 is strictly an **internal, private Windows implementation detail** located in `src/Khefest.Graphics.LowLevel/Windows/`.

```text
                    Khefest
                       │
          ┌────────────┴────────────┐
          │                         │
       High-level               Low-level
          API                       API
          │                         │
    ┌─────┴─────┐          ┌────────┴────────┐
    │            │          │                 │
   2D           3D       GPU resources    Commands
    │            │          │                 │
 SpriteBatch  MeshRenderer │                 │
 Camera2D     Camera3D      └────────┬────────┘
                                     │
                        Direct3D 11 Windows Backend
                        (internal implementation)
                                     │
                                    GPU
```

### 2.1 Why Direct3D 11 Was Chosen as the Initial Windows Backend
1. **Zero External Dependencies / True Native OS Integration**: Direct3D 11 and DXGI runtime libraries (`d3d11.dll`, `dxgi.dll`) are built directly into every modern installation of Windows (Windows 10/11). The engine requires no third-party redistributables, Vulkan runtime installers, or external wrapper packages.
2. **Rock-Solid Hardware Stability**: Direct3D 11 driver implementations across NVIDIA, AMD, and Intel are mature, reliable, and free of the fragile undefined behaviors and driver crashes that plague early-stage low-level Vulkan or D3D12 engines.
3. **Purity of the Khefest Abstraction**: By implementing our own lightweight P/Invoke bindings without third-party wrapper dependencies (such as SharpDX, Silk.NET, or Vortice), Khefest maintains complete sovereignty over its memory layout, COM lifecycle, and error boundaries.

### 2.2 Complete Isolation from High-Level Code
- **Zero Leaked Types**: The public Khefest API exposes **zero Direct3D, COM, or DirectX types**. Neither `SpriteBatch`, `MeshRenderer`, nor `IGpuDevice` exposes `ID3D11Device`, `ID3D11DeviceContext`, or DirectX enums.
- **Swappable Architecture**: Because Khefest owns all interface contracts (`IGpuDevice`, `IGpuBuffer`, `IGpuTexture`, `IPipeline`, `ICommandRecorder`, `ISwapChain`), the backend is fully pluggable. Future backends (such as Vulkan on Linux, Metal on macOS, or a custom Direct3D 12/DirectX Next backend) can be added without altering a single line of high-level graphics, UI, or game application code.

---

## 3. The Anti-Wrapper Strategy: How Khefest Avoids Becoming a D3D12/Vulkan Wrapper

The primary architectural trap for modern graphics frameworks is becoming a "thin wrapper" that simply mirrors Vulkan or Direct3D 12:

| Aspect | Thin D3D12 / Vulkan Clone (Rejected) | Khefest Native Mental Model (Adopted) |
| :--- | :--- | :--- |\n| **Pipeline Setup** | Requires Root Signatures, Root Descriptors, Descriptor Tables, and Pipeline State Objects (PSO) separately. | **`PipelineDesc`**: Unified description combining shader stages, vertex layouts, rasterizer state, and blend mode in a single, reusable object. |
| **Descriptor Management** | Forces developer to manage CBV/SRV/UAV descriptor heaps, descriptor rings, and visibility masks manually. | **Direct Resource Binding**: High-level binds resources directly (`recorder.SetTexture(slot, texture)`). Low-level handles heap indexing automatically behind the scenes. |
| **Resource Barriers** | Forces developer to track before/after states, subresource indices, transition flags, and barrier synchronization groups. | **Automatic State Tracking**: Resources declare intent (`BufferUsage`, `TextureUsage`); command recorders manage transitions at pass boundaries. |
| **Presentation** | Complex swapchain chain recreation, backbuffer synchronization fences, and present flags. | **`ISwapChain`**: Clean `Present(vsync)` with automatic resize handling attached directly to `IWindow`. |
| **Memory Allocation** | Explicit GPU heap pooling, suballocation, memory type bitmask queries. | **Managed Buffers & Textures**: Clean buffer creation (`BufferUsage.Vertex`, `BufferUsage.Index`, `BufferUsage.Constant`) with integrated lifetime tracking and pooling. |

---

## 4. The Khefest Graphics Abstraction Specification

The graphics system is structured in three strictly separated tiers:

```text
+--------------------------------------------------------------------+
|                    Khefest.Graphics (High-Level)                   |
| (SpriteBatch, MeshRenderer, FontRenderer, MaterialSystem, Cameras) |
+--------------------------------------------------------------------+
                                 |
                                 v
+--------------------------------------------------------------------+
|               Khefest.Graphics.LowLevel (Contracts)                |
|   (IGpuDevice, IGpuBuffer, IGpuTexture, IPipeline, ISwapChain,     |
|             ICommandRecorder, Resource Lifetime Tracking)          |
+--------------------------------------------------------------------+
                                 |
                                 v
+--------------------------------------------------------------------+
|             Internal Windows Backend (Implementation)              |
|        (Win32 Native Backend, DXGI Flip-Model Presentation)        |
+--------------------------------------------------------------------+
```

### 4.1 Core Low-Level Interfaces
1. **`IGpuDevice`**:\n   - Represents the physical GPU and execution context.
   - Creates buffers, textures, shaders, pipelines, and swapchains.
   - Provides device capabilities (MaxTextureDimension, DedicatedMemoryBytes, AdapterName).
2. **`IGpuBuffer`**:\n   - Encapsulates GPU memory for vertices, indices, uniform/constant data, and compute storage.
   - Exposes `SetData<T>(ReadOnlySpan<T> data, int offset)`.
3. **`IGpuTexture`**:\n   - Encapsulates 2D/3D textures and render target backbuffers.
   - Supports `TextureUsage` (Sampled, RenderTarget, DepthStencil).
4. **`IPipeline`**:\n   - Pre-compiled state object specifying:
     - Vertex layout (attributes, formats, offsets)
     - Shaders (vertex, pixel/fragment)
     - Rasterizer state (CullMode, FillMode)
     - Blend state (Opaque, AlphaBlend, Additive)
     - Depth/stencil state (DepthTest, DepthWrite, CompareOp)
5. **`ICommandRecorder`**:\n   - State-machine command list builder:
     - `BeginPass(in RenderPassDesc pass)`
     - `SetPipeline(IPipeline pipeline)`
     - `SetVertexBuffer(int slot, IGpuBuffer buffer)`
     - `SetIndexBuffer(IGpuBuffer buffer, IndexFormat format)`
     - `SetConstantBuffer(int slot, ShaderStage stage, IGpuBuffer buffer)`
     - `SetTexture(int slot, IGpuTexture texture)`
     - `Draw(int vertexCount, int firstVertex)`
     - `DrawIndexed(int indexCount, int firstIndex, int vertexOffset)`
     - `EndPass()`
6. **`ISwapChain`**:\n   - Coordinates window frame presentation with DXGI Modern Flip Model.
   - Exposes `IGpuTexture CurrentBackBuffer { get; }`.
   - `void Resize(int width, int height)`.
   - `void Present(bool vsync)`.

---

## 5. Dual-Mode Resource Management Integration
The graphics layer directly honors the framework configuration:
- **`AutoMemManagement = true`**:
  The framework registers all GPU buffers, textures, shaders, and pipelines in the `ResourceManager`. When the device or manager is disposed, all GPU allocations are cleanly unmapped, released, and finalized.
- **`AutoMemManagement = false`**:
  Advanced developers retain explicit ownership. Each resource must be explicitly disposed. On shutdown, `ResourceLifetimeTracker.EnforceNoLeaks()` verifies zero outstanding allocations; any unfreed resource triggers a detailed exception with the originating call stack.

---

## 6. Conclusion
Khefest Version 1.0 achieves its vision of an authentic, bespoke graphics abstraction. The engine **owns** its contracts; Direct3D 11 serves as an isolated Windows implementation vehicle, and neither high-level game code nor low-level graphics code is polluted with vendor- or platform-specific types.
