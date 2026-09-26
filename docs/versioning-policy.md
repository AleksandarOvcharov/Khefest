# Khefest Versioning & API Compatibility Policy

This document defines the versioning scheme, API stability guarantees, deprecation lifecycle, and packaging standards for the **Khefest** engine.

---

## 1. Semantic Versioning (SemVer 2.0.0)

Khefest strictly follows [Semantic Versioning 2.0.0](https://semver.org/):

$$\text{Version} = \text{MAJOR}.\text{MINOR}.\text{PATCH}$$

* **MAJOR (e.g. `2.0.0`)**: Incompatible API changes, removal of deprecated types or methods, or foundational architecture overhauls.
* **MINOR (e.g. `1.5.0`)**: Backwards-compatible feature additions, ergonomics improvements, new hardware/rendering capabilities, and non-breaking API additions.
* **PATCH (e.g. `1.5.1`)**: Backwards-compatible bug fixes, performance optimizations, memory leak corrections, and documentation updates.

---

## 2. API Surface Classification

To provide clarity to game developers and framework contributors, Khefest divides its codebase into three distinct surface categories:

```
┌────────────────────────────────────────────────────────┐
│                   Public Engine API                    │
│   (Khefest.Core, Khefest.Graphics, Khefest.Input, ...) │  ◄── Strict SemVer Guarantees
└──────────────────────────┬─────────────────────────────┘
                           │
┌──────────────────────────▼─────────────────────────────┐
│                  Hardware Abstraction                  │
│       (IGpuDevice, IGpuBuffer, ICommandRecorder)       │  ◄── Strict SemVer Guarantees
└──────────────────────────┬─────────────────────────────┘
                           │
┌──────────────────────────▼─────────────────────────────┐
│             Internal Platform Drivers                  │
│   (Khefest.Graphics.LowLevel.Windows, D3D11Native)     │  ◄── Internal Implementation Detail
└────────────────────────────────────────────────────────┘
```

### A. Public Engine API (Guaranteed Stable)
* All `public` types, interfaces, methods, properties, and events across:
  * `Khefest.Core` (logging, errors, configuration, lifetime tracking, plugins)
  * `Khefest.Windowing` (window contracts, display service)
  * `Khefest.Input` (keyboard, mouse, button, and scroll state)
  * `Khefest.Graphics` (2D/3D engines, `SpriteBatch`, `Camera2D`, `RenderTarget2D`, `PostProcessEffect`, spatial math)
  * `Khefest.UI` (layout, widgets, themes)
  * `Khefest.Windows` (`Game`, `KhefestApp`, `GameTime`, `Win32Window`)
* **Guarantee**: No breaking signature changes or removals will occur within a MAJOR release.

### B. Hardware Abstraction Layer (Guaranteed Stable)
* Low-level interfaces in `Khefest.Graphics.LowLevel`:
  * `IGpuDevice`, `IGpuBuffer`, `IGpuTexture`, `IPipeline`, `ICommandRecorder`, `ISwapChain`.
  * Value structs: `RenderPassDesc`, `PipelineDesc`, `Color4`, `Viewport`, `ScissorRect`, `VertexLayout`.
* **Guarantee**: Hardware-agnostic. No vendor or native driver types are ever exposed.

### C. Internal Platform Drivers (No Compatibility Guarantees)
* Types residing in `*.Windows` namespaces (e.g. `WindowsGpuDevice`, `WindowsCommandRecorder`, `WindowsGpuTexture`, `D3D11Native`).
* While `public` in C# to allow modular composition across internal engine assemblies, these types are strictly internal driver implementations.
* **Rule**: Game consumers must not reference or cast to `*.Windows` types. An automated reflection test in the test suite verifies that the public engine surface does not leak driver types.

---

## 3. Deprecation Lifecycle

When a public API is superseded by a superior design, Khefest adheres to a multi-stage deprecation cycle:

1. **Stage 1 (Deprecation Notice)**:
   * The API is marked with `[Obsolete("Use NewMethod() instead.", false)]`.
   * Compilation issues a compiler warning with clear instructions for migration.
   * The old API remains fully functional and internally redirects to the new implementation.
2. **Stage 2 (Maintenance)**:
   * Deprecated APIs are maintained for at least **one full MINOR release cycle** before being eligible for removal.
3. **Stage 3 (Removal)**:
   * Removal only occurs upon a **MAJOR version increment** (e.g., from `1.x` to `2.0.0`).

---

## 4. Packaging and Distribution Policy

* **Canonical Metapackage**: `Khefest` — References all core engine assemblies for convenient 1-line installation:
  ```bash
  dotnet add package Khefest
  ```
* **Modular Packages**: Granular packages (`Khefest.Core`, `Khefest.Graphics`, etc.) are published simultaneously with matching version numbers.
* **Symbols & Debugging**: Every release publishes accompanying `.snupkg` symbol packages with Source Link support enabled.
* **Package Validation**: Every package generation is verified by CI/CD via `dotnet pack` and consumer integration tests.
