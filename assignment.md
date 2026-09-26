# Khefest

## 1. Project Vision

Khefest is a modular C# graphics and game framework designed to provide the power and flexibility needed for serious 2D and 3D games while maintaining a significantly simpler API than low-level graphics APIs such as Vulkan.

The central philosophy is:

> **Simple by default. Powerful when needed.**

Khefest should be approachable for beginners while allowing experienced users to progressively move into lower-level graphics concepts without abandoning the framework.

The name Khefest is derived from **Hephaestus (Хефест)**, the Greek god associated with forging and craftsmanship. The framework should embrace this identity: users "forge" games and graphical applications using Khefest.

Khefest is intended primarily for games and graphical applications.

---

# 2. Target Platform

## Version 1

Windows only.

The platform layer should use the **Win32 API** directly.

Initial platform functionality:

* Window creation
* Window destruction
* Window resizing
* Fullscreen mode
* Borderless window mode
* Keyboard input
* Mouse input
* Mouse position
* Mouse buttons
* Window events
* Multiple monitor awareness where practical
* Application message processing
* Timing

Do not introduce OpenGL, Vulkan, Direct3D 11, or Direct3D 12 as hidden middleware.

The Khefest graphics architecture must be designed around its own graphics abstraction. Because Version 1 targets Windows without relying on external graphics runtimes or third-party wrappers, the underlying Windows GPU communication path must be treated as a dedicated research milestone before finalizing the renderer architecture (see Section 23, Phase 3).

Future platforms may be added later.

---

# 3. Language

Primary language:

**C#**

Native code may be introduced when required for:

* Win32 interoperability
* Native graphics functionality
* Native performance-critical functionality
* OS integration
* functionality that cannot reasonably be implemented from managed C#

Native components should preferably be written in C or C++.

The public Khefest API should remain primarily C#.

---

# 4. Architecture

Khefest must be modular.

Suggested initial structure:

```text
Khefest/
├── Khefest.Core
├── Khefest.Graphics
├── Khefest.Graphics.LowLevel
├── Khefest.Graphics.HighLevel
├── Khefest.UI
├── Khefest.Input
├── Khefest.Windowing
├── Khefest.Audio
├── Khefest.Assets
├── Khefest.Debug
├── Khefest.Plugins
├── Khefest.Windows
└── Examples/
```

The exact module structure may be refined during implementation, but responsibilities must remain clearly separated.

---

# 5. Graphics Philosophy

Khefest must expose **two levels of graphics API**.

## 5.1 High-level API

Designed for beginners and rapid development.

Examples of concepts:

```text
DrawSprite(...)
DrawText(...)
DrawShape(...)
DrawMesh(...)
CreateTexture(...)
CreateModel(...)
```

The high-level API should hide unnecessary GPU complexity.

A beginner should be able to create a functional game without understanding:

* GPU synchronization
* command submission
* resource barriers
* descriptor management
* GPU memory allocation
* pipeline internals

---

# 5.2 Low-level API

Advanced users must be able to access the underlying graphics concepts.

Khefest should expose concepts such as:

```text
Device
Buffer
Texture
Shader
Pipeline
CommandList
RenderTarget
Framebuffer
Resource
GPU memory
Synchronization
```

The exact terminology and architecture should be designed consistently rather than copying Vulkan's API.

The low-level API should provide enough control to allow advanced users to optimize rendering and implement specialized rendering systems.

The framework must not force advanced users to use only the high-level API.

---

# 6. Progressive Learning Model

Khefest should naturally support this progression:

```text
Beginner
   ↓
High-level Khefest API
   ↓
Intermediate
   ↓
Graphics resources
   ↓
Advanced
   ↓
Low-level rendering API
   ↓
Expert
   ↓
Direct control over Khefest's rendering architecture
```

A user should be able to learn the framework itself progressively rather than needing to understand a complex graphics API before using it.

---

# 7. Rendering

Version 1 must support:

## 2D

* Sprites
* Textures
* Shapes
* Lines
* Rectangles
* Circles
* Basic transformations
* Sprite batching where appropriate

## 3D

* Meshes
* Models
* Textures
* Materials
* Cameras
* Transformations
* Basic lighting
* Depth testing
* Render targets

## Text

Khefest must provide text rendering and font management.

Fonts should be treated as graphics resources.

---

# 8. Application Loop

Khefest should provide a framework-managed application loop.

Conceptually:

```text
Khefest.Run(...)
```

The framework should manage:

```text
Initialize
    ↓
Input processing
    ↓
Update
    ↓
Render
    ↓
Present
    ↓
Repeat
    ↓
Shutdown
```

Users should not normally need to implement a Win32 message loop manually.

Advanced users may optionally gain access to lower-level loop/event functionality.

---

# 9. Memory Management

Khefest must support two resource-management modes.

## Automatic

Example conceptual configuration:

```text
AutoMemManagement = true
```

Khefest automatically manages the lifetime of GPU resources.

The framework should determine when resources can safely be released.

## Manual

```text
AutoMemManagement = false
```

The user becomes responsible for explicitly managing resources.

This mode should be intended for advanced users who need precise control over resource lifetime and memory usage.

The two modes must be documented clearly.

---

# 10. Resource System

Resources should include at minimum:

```text
Texture
Buffer
Shader
Mesh
Model
Font
Audio
RenderTarget
```

Resources should have predictable lifetimes and identifiers/handles where appropriate.

The system should avoid unnecessary allocations and copies.

---

# 11. Asset Support

Initial built-in asset formats:

### Images

* PNG
* JPEG

### Fonts

Support common desktop font formats where practical.

### Audio

* MP3

The asset system should be modular so additional formats can be added later.

Asset loading should support both:

```text
Load(...)
```

and, where practical:

```text
LoadAsync(...)
```

---

# 12. Shaders

Khefest must support both:

### Built-in shaders

For beginners.

Example:

```text
DrawSprite(...)
```

should not require the user to manually write a shader.

### Custom shaders

Advanced users must be able to create and use their own shaders.

Khefest should provide a shader abstraction rather than exposing platform-specific shader APIs directly.

The shader system should eventually support validation and useful compiler/error messages.

---

# 13. UI

UI is an important subsystem, but Khefest is not exclusively a UI framework.

The UI architecture should be designed as a substantial independent module.

The exact model—immediate mode, retained mode, or a hybrid—should be selected during the architectural phase based on:

* performance
* usability
* flexibility
* animation
* layout
* input handling
* custom rendering
* integration with the Khefest renderer

The UI system should support custom controls and extensions.

---

# 14. Debugging and Development Tools

Khefest must include a dedicated debugging system.

Potential functionality:

* Rendering statistics
* FPS
* frame time
* draw-call statistics
* resource statistics
* memory statistics
* GPU information
* debug logging
* graphics errors
* resource leak detection
* validation/debug mode
* render debugging
* optional debug UI
* profiling hooks

Debug functionality should be disableable or minimized in release builds.

---

# 15. Error System

Khefest should have its own error system.

Do not rely exclusively on generic exceptions.

The system should provide:

* Khefest-specific error types
* error codes where useful
* descriptive messages
* subsystem information
* debug context
* optional stack/context information

Errors should make graphics failures understandable rather than producing cryptic native errors.

---

# 16. Plugin System

Khefest should be extensible.

The architecture should eventually allow third-party modules/plugins to provide:

* custom UI controls
* asset loaders
* rendering features
* tools
* debugging tools
* game systems
* custom resources

The plugin API must be designed so that future extensions do not require modifying Khefest's core.

---

# 17. API Design

The API should prioritize:

### Discoverability

Users should be able to understand what a class/function does from its name and documentation.

### Consistency

Similar operations should use consistent naming and behavior.

### Simplicity

Common operations should require minimal code.

### Escape hatches

Advanced functionality should remain available.

### Strong typing

Use C#'s type system instead of forcing users to work with strings or untyped data unnecessarily.

---

# 18. Performance

Khefest must be designed for game development.

Performance priorities include:

* Low CPU overhead
* Efficient GPU resource management
* Minimal unnecessary allocations
* Batching where appropriate
* Efficient command generation
* Efficient asset management
* Multithreading opportunities
* Predictable frame times

However, optimization must not make the beginner API unnecessarily complicated.

The high-level API should optimize common operations internally.

---

# 19. Native Boundary

C# should be the default implementation language.

Native C/C++ should only be used when justified.

Every native component must have a clearly defined boundary with C#.

Avoid scattering native calls throughout the framework.

Prefer:

```text
C# Khefest API
        ↓
Khefest native abstraction
        ↓
Windows/native graphics facilities
```

rather than allowing individual Khefest classes to independently call native APIs.

---

# 20. Graphics Backend Independence

Although version 1 targets Windows, the public Khefest API must not be designed around a specific external graphics API.

Do not expose:

```text
OpenGL.*
Vulkan.*
Direct3D.*
```

concepts in the public API.

Khefest's concepts should belong to Khefest.

A future implementation should be able to introduce additional platform/graphics backends without requiring applications using the high-level API to be rewritten.

---

# 21. Version 1 Scope

The first usable version should focus on:

### Platform & Research
* Windows Win32 platform layer (Window, Keyboard, Mouse, Resizing, Fullscreen, Borderless, Application loop)
* **GPU layer research and Architectural Decision Record (ADR)** defining the native Windows GPU path

### Graphics
* 2D rendering
* 3D rendering
* Textures
* Sprites
* Shapes
* Text
* Meshes
* Models
* Cameras
* Basic materials
* Shaders
* Render targets

### Resources
* Automatic management
* Manual management
* PNG
* JPEG
* Fonts
* MP3

### Development
* Logging
* Errors
* Debug statistics
* Resource debugging

UI, plugins, and advanced tooling should be architected early but do not need to block the first functional renderer.

---

# 22. Example User Experience

A basic application should eventually look conceptually like:

```text
Khefest.Initialize(...);

Khefest.Run(new Game());
```

with the game implementing high-level operations such as:

```text
Initialize()
Update()
Render()
Shutdown()
```

A beginner should be able to create a window, draw a sprite, render text, and create a basic 3D scene without knowing the internals of the graphics system.

An advanced developer should be able to descend into:

```text
Device
Buffer
Texture
Shader
Pipeline
CommandList
RenderTarget
```

and build custom rendering systems.

---

# 23. Development Strategy

Do not attempt to implement every subsystem simultaneously.

Recommended progression:

## Phase 1 — Foundation

* Repository
* Solution structure
* Core module
* Error system
* Logging
* Configuration
* Resource abstraction

## Phase 2 — Windows

* Win32 interop
* Window
* Message processing
* Keyboard
* Mouse
* Timing
* Application loop

## Phase 3 — GPU Layer Research & Architectural Feasibility (Milestone)

The GPU layer must be treated as an explicit research milestone rather than a predetermined implementation detail.

Because Khefest intentionally rejects wrapping OpenGL, Vulkan, Direct3D 11, or Direct3D 12, the developer must investigate and document the actual Windows GPU path before committing to the renderer architecture. This prevents the project from accidentally becoming a thin D3D12 wrapper while preserving Khefest's original vision of a bespoke, native abstraction.

Key research objectives:
* **Windows GPU Interface Investigation**: Investigate the actual user-mode and kernel interfaces on Windows (WDDM, DXGI, presentation models/flip model, driver dispatch, and OS hardware abstractions) across modern GPU vendors (NVIDIA, AMD, Intel).
* **Hardware & Driver Realities**: Document the boundary between what Windows allows in user mode without standard runtimes versus what requires native OS graphics facilities.
* **Architectural Trade-offs**: Analyze trade-offs regarding command submission, memory management, resource barriers, synchronization, and presentation latency.
* **Anti-Wrapper Verification**: Ensure the proposed low-level Khefest abstraction is designed from first principles around Khefest's own mental model, rather than merely renaming D3D12 or Vulkan structs and functions.

Required Milestone Deliverables:
1. **GPU Architecture & Feasibility Report (ADR)**: A comprehensive architectural document analyzing the investigated Windows GPU communication paths, technical constraints, viability assessments, and the proposed native Khefest GPU abstraction design.
2. **Minimal Presentation SPIKE / Proof of Concept**: An isolated native prototype verifying window presentation and basic GPU execution based on the researched path.
3. **Architecture Sign-Off**: Explicit review and approval of the GPU architectural direction before proceeding to renderer implementation.

## Phase 4 — Minimal Renderer

Goal:

> Open a Khefest window and render the first primitive using the architecture validated in Phase 3.

Progressively implement:

* GPU device abstraction
* Buffers
* Textures
* Shaders
* Render targets
* Command system
* Synchronization
* Presentation

## Phase 5 — 2D

* Textures
* Sprites
* Shapes
* Text
* Cameras
* Batching

## Phase 6 — 3D

* Meshes
* Models
* Materials
* Camera
* Depth
* Lighting

## Phase 7 — Resource Management

* Automatic lifetime management
* Manual lifetime management
* GPU memory management
* Resource cache

## Phase 8 — Debugging

* Statistics
* Validation
* Resource tracking
* Profiling

## Phase 9 — UI

Build the Khefest UI system on top of the graphics infrastructure.

## Phase 10 — Plugins

Introduce the extension system after the core API has stabilized.

---

# 24. Testing

Khefest must include automated tests wherever practical.

Test:

* Resource lifetimes
* Window state
* Input
* Asset loading
* Math
* Resource management
* API behavior
* Error handling

Graphics tests should include small executable examples/validation applications.

Maintain example projects such as:

```text
Examples/
├── HelloWindow
├── Hello2D
├── HelloText
├── Hello3D
├── HelloModel
├── HelloLowLevel
└── GameExample
```

---

# 25. Documentation

Documentation is part of the product.

Provide:

* Getting started guide
* Installation
* First application
* Windows GPU Architecture & Feasibility Report (ADR)
* 2D tutorial
* 3D tutorial
* Resource management
* Shader guide
* Low-level graphics guide
* UI guide
* Debugging guide
* API reference
* Architecture documentation

The documentation should explicitly explain the progression from the high-level API to the low-level API.

---

# 26. Design Principle

The most important requirement of Khefest is:

> **Do not make beginners pay the complexity cost required by advanced users.**

A beginner should be able to make a game using simple Khefest functions.

An experienced user should be able to access the underlying systems and take control when necessary.

Khefest therefore follows:

```text
                    Khefest
                       │
          ┌────────────┴────────────┐
          │                         │
      High-Level                 Low-Level
          │                         │
    Simple API                GPU control
          │                         │
          └────────────┬────────────┘
                       │
                  Same framework
```

---

# 27. Product Goal

Khefest should occupy the space between:

```text
Raylib
  ↓
simple but limited for ambitious projects

Khefest
  ↓
simple + powerful + extensible

Vulkan
  ↓
extremely powerful but highly complex
```

Khefest should **not** attempt to compete with Vulkan by exposing Vulkan's complexity.

Instead, its goal is to provide enough underlying capability that an ambitious game developer does not have to abandon Khefest simply because their project becomes more sophisticated.

---

# 28. Final Product Definition

Khefest is a **C#-first, Windows-first, modular game and graphics framework** with:

* A simple high-level API
* A powerful low-level graphics API
* Windows GPU research milestone and architectural decision record (ADR)
* 2D rendering
* 3D rendering
* Text
* Sprites
* Shapes
* Resource management
* Custom shaders
* UI
* Input
* Win32 windowing
* Debugging/profiling
* Plugin support
* Automatic and manual resource management
* Native C/C++ support where required

Its defining principle is:

> **Forge simple games quickly. Forge complex games without outgrowing the framework.**
