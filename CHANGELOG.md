# Changelog

All notable changes to the **Khefest** engine will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [1.6.3] - 2026-09-27

> **External Consumer Validation, Documentation Synchronization & Hardening Milestone**  
> Validated real-world consumer setups across samples/MyGame and pure NuGet package consumer samples/NuGetConsumer. Synchronized documentation snippets across README.md, docs/getting-started.md, and docs/developer-guide.md with responsive player movement and background grid rendering. Added automated test verifying MyGame only references the canonical metapackage Khefest.csproj. Updated template and consumer configurations to v1.6.3 with 113 passing tests and 0 warnings.

### Fixed & Validated
- **Documentation Example Synchronization**:
  - Synchronized starter game snippets in docs/getting-started.md and docs/developer-guide.md to reflect origin-centered entities, background reference arena grids, and decoupled screen-space HUD overlay rendering.
  - Aligned published PackageReference examples and 	emplates/Khefest.Template/KhefestTemplate.csproj to version 1.6.3.
- **Consumer Metapackage Dependency Verification**:
  - Added MyGame_Sample_ConsumesCanonicalMetapackage_AndCompilesCleanly test to ExternalDeveloperValidationTests.cs verifying external project structure adheres to single-reference metapackage consumption.
- **Pure NuGet Consumer Validation (samples/NuGetConsumer)**:
  - Re-verified pure package restoration from local feed with zero project references under TreatWarningsAsErrors=true.

---

## [1.6.2] - 2026-09-27

> **Rendering Correctness, Buffer Bounds & Stability Fixes Milestone**  
> Addressed rendering artifacts in 2D sprite and shape rendering, eliminated backbuffer shutdown leak reports, protected native window callback procedures from GC collection, and resolved player movement and centering in template and quickstart samples.

### Fixed
- **Direct3D 11 Buffer Over-Read in `WindowsGpuBuffer.SetData`**:
  - Added `D3D11_BOX` struct in `D3D11Native.cs` and passed accurate byte-range destination boxes to `ID3D11DeviceContext::UpdateSubresource`.
  - Prevented D3D11 from over-reading past the bounds of active vertex/index spans into uninitialized heap memory when updating dynamic GPU buffers.
- **SwapChainBackBuffer Shutdown Leak Report**:
  - Ensured `ISwapChain` is deterministically disposed in the `finally` block of `KhefestApp.Run` prior to evaluating `ResourceManager.Tracker.HasLeaks`.
  - Backbuffer render target texture is cleanly dereferenced and untracked at shutdown, eliminating false-positive leak warnings.
- **Win32 Window GC Callback Protection**:
  - Replaced transient managed delegate window procedure with native `DefWindowProcW` export address obtained via `NativeLibrary.GetExport(..., "DefWindowProcW")`.
  - Eliminates potential garbage collection and access violation crashes during native window destruction on application exit.
- **RetroTankArena Offscreen Target Accumulation**:
  - Added `clearColor` parameter to `SpriteBatch.Begin(renderTarget, camera, sortMode, Color4? clearColor)` and `SpriteBatch.Begin(renderTarget, transformMatrix, sortMode, Color4? clearColor)`.
  - Explicitly cleared `_offscreenTarget` on Pass 1 in `RetroTankArena`, eliminating smeared tank trails and accumulating explosion artifacts across frames.
- **ShapeRenderer2D `FillCircle` Rasterization Artifacts**:
  - Replaced radiating overlapping lines with non-overlapping horizontal scanline slices (`FillRectangle`), removing jagged perimeter spikes and alpha double-blending artifacts.
- **Template and QuickStart Circle Centering & Movement**:
  - Decoupled camera position from player position in `samples/MyGame/Game.cs`, `templates/Khefest.Template/Game.cs`, and `README.md`, allowing the circle to freely move across the viewport.
  - Added background arena reference grid lines (`DrawLine`) and arena boundary box (`DrawRectangle`) in world space.
  - Decoupled world-space entity rendering from screen-space HUD overlay rendering (`camera: null`), and displayed live player coordinates in the HUD (`Pos: ({_position.X:F0}, {_position.Y:F0})`).

---

## [1.6.1] - 2026-09-26

> **Fixed metadata**  

---

## [1.6.0] - 2026-09-26

> **Consumer Experience & Release Engineering Milestone**  
> Validated the complete external developer journey from initial package discovery and scaffolding to deployment. Introduced a pure NuGet package consumer project (`samples/NuGetConsumer`) with strictly zero project references, restored from the local feed. Formally audited the metapackage dependency graph and prevented duplicate/circular dependencies. Replaced heuristic member-count compatibility tests with a deterministic, signature-based API baseline comparison system (`tests/Khefest.Tests/ApiBaselines/Khefest.Api.Baseline.1.5.0.txt`) that flags removed types, methods, parameters, return types, or accessibility shifts. Established executable documentation tests (`tests/Khefest.Tests/DocumentationVerificationTests.cs`) ensuring all published code snippets compile, run headlessly, and never silently rot. Created the official `dotnet new khefest` project template (`templates/Khefest.Template` and `Khefest.Templates.1.6.0.nupkg`) enabling rapid one-command game scaffolding. Expanded automated test suite from 104 to 110 passing tests with 0 warnings and 0 errors across the solution.

### Added
- **Pure NuGet Consumer Verification Project (`samples/NuGetConsumer`)**:
  - Independent sample application configured with strictly `<PackageReference Include="Khefest" Version="1.6.0" />` and zero `<ProjectReference>` elements.
  - Validates end-to-end NuGet restoration, transitive dependency resolution, and runtime execution from an external developer environment.
  - Added automated CI test (`NuGetConsumer_ProjectFile_StrictlyHasNoProjectReferences`) ensuring no internal source references ever leak into consumer projects.
- **Official `dotnet new khefest` Template & Package (`Khefest.Templates.1.6.0.nupkg`)**:
  - Designed standard template structure under `templates/Khefest.Template/` including `KhefestTemplate.csproj`, `Program.cs`, `Game.cs`, and `Assets/`.
  - Packaged template into `Khefest.Templates.1.6.0.nupkg` with `<PackageType>Template</PackageType>`.
  - Verified local installation via `dotnet new install ./artifacts/packages/Khefest.Templates.1.6.0.nupkg` and instant generation via `dotnet new khefest -n MyGame`.
  - Added automated test (`TemplatePackage_ContainsExpectedStructureAndMetadata`) checking package manifest and content integrity.
- **Signature-Based API Compatibility System (`ApiSignatureExtractor`)**:
  - Created deterministic API signature extraction engine (`tests/Khefest.Tests/ApiSignatureExtractor.cs`) parsing type declarations, constructors, methods with generic arguments and modifiers, properties, events, and fields.
  - Locked canonical baseline `tests/Khefest.Tests/ApiBaselines/Khefest.Api.Baseline.1.5.0.txt`.
  - Upgraded compatibility test (`PublicApiSignatures_MatchOrExtendBaseline_WithoutBreakingChanges`) to detect any removed or mutated public API member rather than relying on raw member counts.
- **Executable Documentation & Rot Prevention Suite (`tests/Khefest.Tests/DocumentationVerificationTests.cs`)**:
  - `GettingStarted_GameLoopSimulation_ExecutesAndCleansUpWithZeroLeaks`: Simulates published starter game loop headlessly, verifying frame updates, camera viewport resizing, and leak-free resource shutdown.
  - `DeveloperGuide_MathAndGeometrySnippets_BehaveAccurately`: Executes published snippets for `MathHelper.LerpAngle` and `Rect2D` collision detection.
  - `MarkdownDocumentation_HasNoObsoleteApiReferences`: Automated markdown scanner across all documentation files prohibiting obsolete patterns (`ColorTarget =`, `UnderlyingTexture`) or direct instantiation of internal driver types (`WindowsGpuDevice`).
- **Metapackage Dependency Graph Audit**:
  - Added `Metapackage_DependencyGraphAudit_ReferencesAllSubsystems` verifying that `Khefest.nupkg` accurately bundles all 7 modular subsystems (`Core`, `Windowing`, `Input`, `Graphics.LowLevel`, `Graphics`, `UI`, `Windows`) with matching version semantics.

### Refined & Stabilized
- **Documentation Synchronization**:
  - Synchronized `docs/getting-started.md` and `docs/developer-guide.md` to use standard NuGet package commands (`dotnet add package Khefest`) and updated `ColorTargets` array syntax.
- **Solution Integration**:
  - Added `samples/NuGetConsumer` and `templates/Khefest.Templates.csproj` to `Khefest.sln`.
- **Zero-Warning Rigor**:
  - Maintained 0 warnings and 0 errors across entire solution under `-warnaserror`.
  - Automated test suite expanded to 110 passing tests.

---

## [1.5.0] - 2026-09-26

> **Public API Stabilization & Packaging Milestone**  
> Stabilized and unified the Khefest public API surface for production distribution. Refactored wrapper texture access to the semantically pure `IGpuTexture.CanonicalTexture` accessor ("give me the GPU texture represented by this resource"). Centralized build configuration, versioning (1.5.0), and symbol packaging via root `Directory.Build.props`. Enabled XML documentation generation (`GenerateDocumentationFile=true`) across all library projects. Established canonical metapackage `Khefest` (`src/Khefest/Khefest.csproj`) enabling single-line NuGet installation (`dotnet add package Khefest`). Created `docs/versioning-policy.md` outlining SemVer 2.0.0 rules, API stability guarantees, surface boundaries, and multi-stage deprecation cycles. Added public API compatibility test suite (`tests/Khefest.Tests/PublicApiCompatibilityTests.cs`) locking down the public API baseline against accidental breaking changes, verifying package structure integrity, and validating headless compilation of documentation starter examples. Expanded automated test suite to 104 passing tests with 0 warnings and 0 errors across the solution.

### Added
- **Unified Canonical Metapackage (`src/Khefest/Khefest.csproj`)**:
  - Provides a single entry point NuGet package (`Khefest`) referencing all engine modules (`Core`, `Windowing`, `Input`, `Graphics.LowLevel`, `Graphics`, `UI`, `Windows`).
  - Supports 1-line package installation for game developers: `dotnet add package Khefest`.
- **Packaging & Distribution Infrastructure**:
  - Centralized solution build settings, package metadata, authors, license, repository URLs, and tags in root `Directory.Build.props`.
  - Configured Source Link support, embedding untracked sources, and automated symbol package generation (`.snupkg`).
  - Created root `nuget.config` registering `./artifacts/packages` as a local feed alongside `nuget.org`.
  - Automated `dotnet pack` pipeline producing 8 distinct packages (`Khefest`, `Khefest.Core`, `Khefest.Windowing`, `Khefest.Input`, `Khefest.Graphics.LowLevel`, `Khefest.Graphics`, `Khefest.UI`, `Khefest.Windows`).
- **Versioning & API Compatibility Policy (`docs/versioning-policy.md`)**:
  - Formally documents SemVer 2.0.0 compliance (Major.Minor.Patch).
  - Classifies API surfaces into Public Engine API, Hardware Abstraction Layer, and Internal Platform Drivers (`*.Windows`), clarifying stability commitments.
  - Outlines the 3-stage deprecation lifecycle (`[Obsolete]` notice with migration guidance, maintenance for at least one minor release, removal only on major version increments).
- **Public API Compatibility & Regression Test Suite (`tests/Khefest.Tests/PublicApiCompatibilityTests.cs`)**:
  - `CanonicalTexture_IsProperlyExposedOnGpuTextureAndRenderTarget2D`: Asserts proper contract exposure of canonical texture accessors.
  - `CorePublicTypes_ExistAndAreConsistent`: Verifies presence and visibility of key public types across all subsystems.
  - `LowLevelInterfaceSignatures_HaveNotBroken`: Locks down method signatures for `IGpuBuffer.SetData`, `ICommandRecorder.BeginScopedPass`, and `RenderTarget2D.Resize`.
  - `PublicApiSurface_TotalMemberCountMeetsOrExceedsBaseline`: Reflection-based guard asserting that total public types (>= 60) and members (>= 350) do not silently regress.
  - `DocumentationExampleSnippet_CompilesAndExecutesHeadlessly`: Validates that documentation starter examples compile and execute headlessly with zero resource leaks.
  - `GeneratedPackages_ContainAssembliesAndXmlDocumentation`: Validates that generated `.nupkg` packages contain compiled binaries and corresponding `.xml` IntelliSense files.

### Refined & Stabilized
- **Canonical Texture Resource Accessor**:
  - Refactored `IGpuTexture.UnderlyingTexture` to `IGpuTexture.CanonicalTexture => this;`.
  - Replaced implementation-leak terminology with pure semantic resource intent: "give me the GPU texture represented by this resource", avoiding any suggestion that callers should unwrap backend implementations.
  - Updated `RenderTarget2D.CanonicalTexture` and `WindowsCommandRecorder.ResolveNativeTexture` to seamlessly operate on the canonical texture abstraction.
- **XML Documentation & IntelliSense**:
  - Enabled `<GenerateDocumentationFile>true</GenerateDocumentationFile>` across all projects in `src/`, outputting `.xml` documentation for all libraries to support IDE IntelliSense and package distribution.
- **Zero-Warning Rigor**:
  - Maintained 0 warnings and 0 errors across entire solution under `-warnaserror`.
  - Automated test suite expanded from 98 to 104 passing tests.

---

## [1.4.0] - 2026-09-26

> **External Developer Validation & Abstraction Boundary Assurance Milestone**  
> Validated Khefest from the perspective of an external game developer consuming the engine purely via documentation and public API contracts without inspecting engine source code or importing internal backend types. Built an independent standalone consumer game (`samples/MyGame`) referencing solely public Khefest abstractions, identified and eliminated all developer experience (DX) friction points, audited and sealed the public API surface against DirectX/backend type leaks, and authored a comprehensive developer guide (`docs/developer-guide.md`). Reached 98 passing headless automated tests with 0 warnings and 0 errors across the entire solution.

### Added
- **Standalone External Consumer Game (`samples/MyGame`)**:
  - Independent project created outside the engine core tree (`samples/MyGame/MyGame.csproj` + `Program.cs`) configured with `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`.
  - Exercises full modern game workflow: custom window initialization, `SpriteBatch` multi-layer rendering, `Camera2D` tracking with zoom and offset, input handling via `Input.MousePosition` / `Input.MouseDelta` tuple deconstruction, resize-aware `RenderTarget2D`, custom HLSL CRT/vignette `PostProcessEffect`, custom low-level GPU vertex buffer and pipeline backdrop quad, `Rect2D` arena bounds, and deterministic shutdown.
  - Zero internal references: strictly compiles against public contracts without referencing `Khefest.Graphics.LowLevel.Windows` or any native D3D11 types.
- **Comprehensive Developer Guide (`docs/developer-guide.md`)**:
  - Layer Selection guide: when to use high-level abstractions (`SpriteBatch`, `RenderTarget2D`, `PostProcessEffect`) vs. low-level hardware control (`IGpuDevice`, `IPipeline`, `ICommandRecorder`).
  - Resource Ownership rules: strict enforcement of the *"Whoever Creates, Disposes"* model, leak detection with `ResourceManager.Tracker`, and safe disposal patterns.
  - Custom HLSL Shader Authoring conventions: register bindings (`t0`, `s0`, `b0`), vertex structures, and procedural fullscreen triangles (`SV_VertexID`).
  - Common Diagnostics: clear error explanations for `GraphicsCommandOutsideRenderPass`, `GraphicsPassAlreadyActive`, and `GraphicsResourceBindingFailed`.
  - Complete 50-line starter template for new games.
- **Automated Public API Leak & Consumer Pipeline Tests (`tests/Khefest.Tests/ExternalDeveloperValidationTests.cs`)**:
  - `PublicApi_DoesNotLeakDirectXOrNativeBackendTypes`: Reflection-based architectural guard inspecting every public type, method, property, and field in `Khefest.Core`, `Khefest.Graphics`, and `Khefest.Graphics.LowLevel` to verify no DirectX or internal backend namespaces leak into public contracts.
  - `StandaloneConsumer_HeadlessPipelineExecution_SucceedsWithoutInternalTypes`: Validates that consumer game workflows run headlessly, correctly recording commands and releasing resources with 0 leaks.

### Improved & Friction Polished
- **Color Palette Ergonomics**:
  - Expanded `Color4` with standard game colors: `LimeGreen`, `Gold`, `SlateGray`, `LightSkyBlue`, `RoyalBlue`, `Crimson`, `DeepSkyBlue`, and `Teal`.
- **Low-Level Vertex Layout Ergonomics**:
  - Added `VertexElement(string semanticName, GpuFormat format, int offset, int semanticIndex = 0)` overload, eliminating the mandatory second `0` index argument for non-array semantics.
  - Added `VertexLayout(params VertexElement[] elements)` constructor with automatic stride computation, eliminating manual byte-stride arithmetic.
- **Seamless `RenderTarget2D` Backend Interoperability**:
  - Added `IGpuTexture.UnderlyingTexture => this;` interface default implementation.
  - `RenderTarget2D` implements `UnderlyingTexture` unwrapping the inner `_colorTexture`.
  - `WindowsCommandRecorder` resolves native texture bindings via `ResolveNativeTexture`, allowing developers to pass `RenderTarget2D` directly into `SetTexture` and low-level render passes without manual type unwrapping.
- **Zero-Warning Rigor**:
  - Added `samples/MyGame` to `Khefest.sln` ensuring standalone consumer games are validated on every build.
  - Full solution and test suite compile with 0 warnings and 0 errors under `-warnaserror`.

---

## [1.3.0] - 2026-09-26

> **API Ergonomics & Framework Usability Milestone**  
> Streamlined core engine developer ergonomics based on insights from genuine game development in Milestone 1.2.0. Introduced dynamic resize-aware RenderTarget2D, procedural fullscreen triangle PostProcessEffect (zero-vertex buffer HLSL post-processing), zero-allocation single-value constant buffer uploads via IGpuBuffer.SetData<T>(in T value), core spatial geometry primitive Rect2D, shortest-arc angle interpolation MathHelper.LerpAngle, deterministic RAII ScopedRenderPass, canonical Vector2 input queries, and GameTime.DeltaTime. Expanded test suite from 88 to 96 passing automated tests (0 errors, 0 warnings).

### Added
- **RenderTarget2D (Khefest.Graphics.Texturing.RenderTarget2D)**:
  - High-level offscreen render target wrapping an underlying IGpuTexture with dynamic resize support (Resize(width, height)).
  - Handles automatic texture reallocation and safe disposal of prior resources during window/resolution changes.
  - Implements IGpuTexture directly for seamless drop-in compatibility anywhere textures or render targets are expected.
  - Built-in CreatePass(Color4? clearColor, bool clearDepth) factory method for configuring render passes with optional depth-stencil attachments without manual boilerplate.
- **PostProcessEffect (Khefest.Graphics.PostProcessing.PostProcessEffect)**:
  - Encapsulates custom HLSL screen-space post-processing shaders over a procedural fullscreen triangle (SV_VertexID in the vertex shader).
  - Completely eliminates the need for manual vertex buffer creation, index buffer binding, and vertex input layouts for post-processing passes.
  - High-level Apply(IGpuTexture source, IGpuTexture destination, Color4? clearColor) automatically manages command recording and render pass execution.
  - Inline Draw(ICommandRecorder recorder, IGpuTexture source) allows composing post-processing within existing active render passes.
  - SetUniforms<T>(in T value) where T : unmanaged: Updates effect parameters into a GPU constant buffer without per-frame heap allocations.
- **Zero-Allocation Single-Value Buffer Updates**:
  - Added IGpuBuffer.SetData<T>(in T value, int offsetInBytes = 0) where T : unmanaged across abstractions and native Windows backend (WindowsGpuBuffer).
  - Allows direct updates of uniform structs (e.g. matrices, light parameters, post-fx settings) via in parameter without allocating temporary arrays or spans.
- **ScopedRenderPass (Khefest.Graphics.LowLevel.ScopedRenderPass)**:
  - Disposable RAII struct returned by recorder.BeginScopedPass(in RenderPassDesc pass).
  - Guarantees EndPass() is invoked deterministically upon scope exit, even if unhandled exceptions occur during pass recording.
  - Supported by ICommandRecorder.IsInPass state tracking.
- **Core Math & Spatial Primitives**:
  - **Rect2D (Khefest.Graphics.Mathematics.Rect2D)**: Readonly struct representing axis-aligned 2D rectangles with boundary accessors (Left, Top, Right, Bottom, Center, TopLeft, BottomRight), containment checks (Contains(Vector2), Contains(Rect2D)), intersection testing (Intersects, Intersect), union computation (Union), padding (Inflate), and translation (Offset).
  - **MathHelper.LerpAngle**: Shortest-arc angular interpolation wrapping smoothly across the -pi / +pi discontinuity and returning normalized angles in (-pi, pi].
- **Canonical Input & Timing Ergonomics**:
  - Input.MousePosition and Input.MouseDelta now return System.Numerics.Vector2 directly for effortless vector math in game code.
  - Added Input.MousePositionPixels and Input.MouseDeltaPixels returning (int X, int Y) tuples for pixel-exact operations.
  - Added VectorExtensions.Deconstruct enabling tuple deconstruction: var (x, y) = Input.MousePosition;.
  - Added GameTime.DeltaTime returning frame delta in fractional seconds as float, standardizing frame-rate independent physics and animations across all games.
- **Lifecycle Integration**:
  - Added Game.OnResize(int width, int height) callback in base Game class, invoked automatically by KhefestApp when window resize events occur.
  - Added Camera2D.Resize(int width, int height) to synchronize camera viewports and origins on window resize.
  - Refactored *Retro Tank Arena* (examples/RetroTankArena) to leverage Milestone 1.3.0 APIs (RenderTarget2D, PostProcessEffect, SetData(in T), DeltaTime, OnResize), drastically reducing boilerplate and removing manual vertex buffer post-processing code.
- **Automated API Ergonomics Suite (tests/Khefest.Tests/ApiErgonomicsTests.cs)**:
  - 8 new automated tests (bringing total suite to 96 passing tests):
    - Rect2D_GeometryOperations_BehaveAccurately: Tests containment, intersection, union, inflation, and offsetting.
    - MathHelper_LerpAngle_InterpolatesShortestArcAcrossBoundary: Validates shortest-arc angle interpolation across boundary wrap-around.
    - InputErgonomics_Vector2AndPixelQueries_AndVectorDeconstruction: Validates Vector2 mouse queries, pixel tuples, and deconstruction.
    - GameTime_DeltaTime_ReturnsAccurateSeconds: Validates DeltaTime precision.
    - ScopedRenderPass_GuaranteesEndPass_EvenOnException: Validates RAII render pass cleanup and exception resilience.
    - GpuBuffer_SetDataSingleStruct_ZeroAllocationUpload: Validates single-struct unmanaged uploads and bounds checks.
    - RenderTarget2D_LifecyclePassGenerationAndResizing_ZeroLeaks: Tests offscreen render target resizing, pass generation, and zero-leak disposal.
    - PostProcessEffect_ProceduralTrianglePipelineAndApply: Tests zero-VB procedural triangle shader compilation and rendering.

### Improved & Refined
- **Procedural Pipeline Support**:
  - Updated WindowsGpuDevice.CreatePipeline to permit desc.VertexLayout == null, supporting vertex-shader-generated procedural geometry (e.g. fullscreen triangles using SV_VertexID) without binding input layouts or vertex buffers.
- **Command Recorder Safety**:
  - Hoisted stackalloc allocations out of loops in WindowsCommandRecorder to adhere to CA2014 and eliminate stack exhaustion risks.
- **Resource Ownership Transparency**:
  - Maintained IGpuResource : IResource inheritance hierarchy across all low-level GPU abstractions to provide consistent resource tracking, leak detection, and diagnostics across the entire framework.

---

## [1.2.0] - 2026-09-26

> **API Stress, Renderer Correctness & Genuine Game Milestone**  
> Validated engine robustness under extreme API misuse, repeated lifecycle stress, and advanced multi-pass rendering scenarios. Expanded headless automated testing from 76 to 88 tests covering zero-leak window resizing, massive resource churn, multiple render targets (MRT), depth-only passes, dynamic scissors/viewports, and invalid command state handling. Built *Retro Tank Arena*—a complete playable combat game showcasing high-level 2D sprite rendering naturally dropping down into low-level custom HLSL post-processing.

### Added
- **Genuine Playable Game: Retro Tank Arena (`examples/RetroTankArena`)**:
  - Full top-down arena tank combat game demonstrating natural, friction-free movement between high-level game abstractions and low-level GPU control.
  - Game mechanics: physics-driven tank hull steering with rotational inertia, realistic tread mark deposition on the arena floor, mouse-aimed turret cannon with shell ricochet off concrete walls, deployable proximity landmines, destructible brick barriers with crumble physics, pickups (Health Repair, Ammo Supply, Landmine Depot, Overdrive Shield), and intelligent multi-class enemy AI (Scouts, Battle Tanks, Heavy Tanks) with patrol paths and line-of-sight targeting.
  - Multi-pass rendering pipeline:
    - **Pass 1 (High-Level)**: Complete arena world rendered offscreen to a custom render target texture (`ArenaOffscreenRT`) using `SpriteBatch`, `Camera2D`, and 2D vector primitives.
    - **Pass 2 (Low-Level GPU Dropdown)**: Binds offscreen texture into a custom fullscreen pipeline executing a bespoke HLSL post-processing shader with chromatic aberration, radial vignette, CRT scanlines, and dynamic red damage flash driven by uniform constant buffers.
    - **Pass 3 (High-Level BackBuffer Overlay)**: Renders screen-space HUD, wave status, weapon gauges, and a real-time minimap radar directly onto the swapchain backbuffer.
- **Automated API Abuse & Rendering Correctness Suite (`tests/Khefest.Tests/ApiAbuseAndCorrectnessTests.cs`)**:
  - 12 comprehensive automated headless tests (expanding total test suite to 88/88 passing tests):
  - `ResizeStress_RepeatedResizeAndZeroSizeHandling_ZeroLeaks`: 9 rapid successive window resizes across variable resolutions (including 1x1 edge case) verifying zero GPU resource leaks.
  - `ResourceStress_MassiveTextureAndBufferChurn_CacheEvictionAndZeroLeaks`: Churn of 300 vertex buffers and 100 textures with data updates, verifying exact zero leaked resources upon tracker checkpointing.
  - `RenderingCorrectness_DepthOnlyPass_Succeeds`: Validates shadow-map style depth-only pass with zero color render targets.
  - `RenderingCorrectness_MultipleRenderTargets_Succeeds`: Validates simultaneous binding and clearing of multiple color render targets (MRT).
  - `RenderingCorrectness_RenderToTexture_CanSampleInSubsequentPass`: Validates rendering to an offscreen texture in pass 1 and sampling it as an SRV texture in pass 2.
  - `RenderingCorrectness_DynamicViewportAndScissor_ChangesMidPass`: Validates dynamic mid-pass viewport and scissor rectangle updates.
  - `ApiAbuse_CommandOutsideRenderPass_ThrowsInvalidOperation`: Ensures draw and state commands outside active render passes throw `KhefestGraphicsException(InvalidOperation)`.
  - `ApiAbuse_NestedBeginPass_ThrowsInvalidOperation`: Prohibits nesting `BeginPass` calls without ending previous passes.
  - `ApiAbuse_DrawWithDisposedResource_ThrowsException`: Ensures binding disposed buffers, textures, or pipelines throws `GraphicsResourceBindingFailed`.
  - `ApiAbuse_MismatchedBufferSizes_ThrowsArgumentOutOfRangeException`: Enforces strict data buffer boundary and overflow checks.
  - `ApiAbuse_DuplicateResourceDisposal_IsSafeAndIdempotent`: Verifies double disposal of buffers and textures is completely safe and idempotent.
  - `ApiAbuse_InvalidPipelineConfiguration_ThrowsException`: Verifies pipeline creation rejects null/disposed shaders and empty vertex layouts.

### Fixed & Hardened
- **Render Pass Lifecycle State Machine**:
  - Added strict `_isInPass` guard inside `WindowsCommandRecorder` preventing commands (`Draw`, `DrawIndexed`, `SetVertexBuffer`, `SetIndexBuffer`, `SetUniformBuffer`, `SetTexture`, `SetViewport`, `SetScissor`) from running outside `BeginPass`/`EndPass`.
  - Nested `BeginPass` calls now throw `KhefestGraphicsException` with clear diagnostic context.
- **Multiple Render Targets (MRT) & Depth-Only Passes**:
  - `WindowsCommandRecorder.BeginPass` now supports up to 8 simultaneous render targets via `pass.ColorTargets` and zero color targets when performing depth-only passes (`NumViews = 0, ppRenderTargetViews = null`).
- **GpuResource Safety Validation**:
  - `WindowsCommandRecorder` and `WindowsGpuDevice` validate `IsDisposed` on all bound buffers, textures, and pipeline objects before issuing native driver calls, converting potential driver access violations into clear managed exceptions.

---

## [1.1.0] - 2026-09-26

> **Stress Testing, Graphics Hardening & Benchmark Games Milestone**  
> Stress-tested the engine to its maximum capacity across 2D batching, 3D depth-stencil pipelines, spatial math culling, and concurrent resource lifecycles. Identified and patched low-level driver and swapchain edge cases, and implemented two full benchmark games.

### Added
- **Interactive Benchmark Game: StarSwarm 2D (`examples/StressTest2D`)**:
  - Arcade asteroid shooter and particle swarm stress-testing `SpriteBatch` quad buffering, batch overflows, and font rendering.
  - Features rotational inertia flight controls, rapid-fire dual lasers, asteroid fragmentation physics, dynamic camera shake, and explosive particle bursts supporting 20,000+ simultaneous particles.
  - Interactive stress controls: `[F]` / `[+]` dynamic swarm spawner, `[-]` entity reducer, `[1]`–`[4]` layer toggles, and screen-space HUD with live FPS and quad counters.
- **Interactive Benchmark Game: Cosmic Vanguard 3D (`examples/CosmicVanguard3D`)**:
  - Space combat flight simulator pushing the 3D pipeline with 500–3,000 dynamic orbiting celestial bodies (spheres and cubes).
  - Validates hardware depth-stencil buffering, Blinn-Phong lighting with moving directional light source, and 6-plane `BoundingFrustum` culling.
  - Hybrid 3D scene rendering overlaid with 2D cockpit HUD, reticle, shield gauge, and live GPU telemetry overlay (`RenderStatistics`).
  - Stress controls: `[C]` toggle frustum culling to benchmark performance deltas, `[F]` / `[+]` spawn +500 asteroids, `[L]` toggle rotating light, and 6-DOF flight controls (`W`/`S`/`A`/`D`/`Q`/`E` and `Shift` throttle).
- **Automated Headless Engine Stress Tests (`tests/Khefest.Tests/StressTests.cs`)**:
  - `SpriteBatch_StressTest_Processes10000QuadsWithoutCrashing`: verifies dynamic quad buffer auto-flushing and state recycling across 10,000 submitted quads.
  - `SpatialMath_FrustumCulling_StressTest_10000Objects`: benchmarks 6-plane frustum intersection against 10,000 random 3D bounding boxes.
  - `ResourceLifetimeTracker_StressTest_MassiveChurnNoLeaks`: allocates and destroys 300 GPU buffers and 100 textures in rapid succession, verifying 0 leaks via `ResourceLifetimeTracker`.
  - `ConcurrentResourceCache_StressTest_MultiThreadedAccess`: exercises `ResourceCache` across 50 concurrent threads and 5,000 lookups with thread safety guarantees.
  - `MeshRenderer_StressTest_MultipleMeshesWithDepthBuffer`: renders 100 procedural meshes (cubes and spheres) against hardware depth target.
- **Engine-Wide Real-Time Telemetry**:
  - Added `RenderStatistics.Current` frame-level telemetry singleton recording draw calls, vertex counts, index counts, texture binds, and pipeline switches.
  - Connected `SpriteBatch.Flush()` and `MeshRenderer.Draw()` to feed live metrics to the diagnostics subsystem.

### Fixed
- **Native Direct3D 11 Depth Buffer Binding & Clearing**:
  - Resolved issue in `WindowsCommandRecorder.BeginPass` where `DepthStencilView` was previously unbound (`nint.Zero`) during pass initialization.
  - Corrected native `ID3D11DeviceContext` vtable calls: slot 33 (`OMSetRenderTargets`) now receives the native `pDepthStencilView`, and slot 53 (`ClearDepthStencilView`) is invoked with `D3D11_CLEAR_DEPTH | D3D11_CLEAR_STENCIL` when clearing is enabled.
- **SwapChain Resize Resource Tracking**:
  - Resolved issue in `WindowsSwapChain.Resize` where newly recreated backbuffer textures lost their `ResourceManager` registration, restoring accurate leak tracking across window resize events.
- **Example Compilation & Solution Integration**:
  - Added both `StressTest2D` and `CosmicVanguard3D` to `Khefest.sln`.
  - Removed erroneous `using` block on `Game` in `StressTest2D/Program.cs` and cleaned up unused fields.
  - Corrected parameter ordering for `DirectionalLight` constructor.

---

## [1.0.0] - 2026-09-26

> **First Public Release Baseline**  
> Version 1.0.0 marks the consolidation of all foundational development milestones (Phases 1 through 10) into a production-ready baseline. Khefest owns its graphics abstraction (`IGpuDevice`, `IGpuBuffer`, `IPipeline`, `ICommandRecorder`, `ISwapChain`); Direct3D 11 serves strictly as the initial internal Windows implementation detail without exposing any vendor types.

### Added
- **Repository Restructure**:
  - Moved all core engine modules into standard [`src/`](src/) directory structure (`src/Khefest.Core`, `src/Khefest.Windowing`, `src/Khefest.Input`, `src/Khefest.Graphics.LowLevel`, `src/Khefest.Graphics`, `src/Khefest.Windows`, `src/Khefest.UI`).
  - Added solution folder hierarchy mapping in `Khefest.sln`.
- **Post-Phase Codebase Audit & Improvements**:
  - Native `WM_CHAR` character input routing in `Win32Window` connected to `IInputService.CharTyped` and `UISystem` for real keyboard typing into `TextBox`.
  - Pinned native `WndProc` delegate via `GCHandle` and registered class-level `DefWindowProcW` to eliminate native callback garbage collection faults.
  - Implemented 3D spatial math primitives in `Khefest.Graphics.Mathematics`: `BoundingBox` (AABB), `BoundingSphere`, `BoundingFrustum` (6-plane View-Projection culling), `Plane`, and `MathHelper`.
  - Upgraded resource leak diagnostics in `KhefestApp` shutdown routine with formatted allocation stack traces via `ResourceLifetimeTracker.GenerateLeakReport()`.
  - Created interactive `examples/HelloUI` showcase demonstrating buttons, click counters, text boxes, sliders, progress bars, check boxes, and dark/light theme switching.
- **Phase 10 — Plugins & Extension System**:
  - `PluginManager` with topological sort dependency resolution and cycle detection.
  - `PluginLoadContext` isolating assemblies via collectible .NET `AssemblyLoadContext`.
  - Strongly typed `IExtensionRegistry` and `ExtensionRegistry` for decoupled system hooks.
  - Full plugin lifecycle management (`Discovered`, `Loaded`, `Initialized`, `Active`, `Stopped`, `Unloaded`, `Faulted`).
  - Unit tests covering dependency orders, circular dependencies, extension points, and dynamic unloading.
- **Phase 9 — UI Subsystem**:
  - Two-pass layout engine with recursive `Measure` and `Arrange` passes.
  - Core widgets: `Label`, `Button`, `TextBox` (with blinking cursor, selection, and keyboard editing), `Slider`, `ProgressBar`, and `CheckBox`.
  - Layout containers: `Canvas` (absolute layout) and `StackPanel` (vertical and horizontal stack layout).
  - `UIRenderer` bridging UI elements over high-performance `SpriteBatch`.
  - `UITheme` with built-in `Dark` and `Light` color palettes.
- **Phase 8 — Debugging & Diagnostics**:
  - Hierarchical `Profiler` capturing microsecond CPU scopes.
  - Runtime `FrameStatistics` (FPS counter, frame times) and `RenderStatistics` (draw calls, vertex counts, batch flushes).
  - Configurable `GpuValidator` intercepting unbound buffers, unended passes, or invalid viewport configurations at debug time.
  - `ResourceLifetimeTracker` capturing allocation stack traces for every live resource and validating leak-free shutdowns.
  - On-screen `DebugOverlay` with customizable telemetry cards.
- **Phase 7 — Resource & Memory Management**:
  - Reference-counted `SharedResource<T>` automating shared GPU/asset lifetimes.
  - Thread-safe `ResourceCache<T>` with deterministic cache invalidation.
  - Reusable `GpuBufferPool` and `RenderTargetPool` eliminating runtime heap allocations for transient GPU resources.
  - Unified `AssetManager` with extensible `IAssetLoader` pipeline.
- **Phase 6 — 3D Graphics Engine**:
  - `Mesh` and `MeshPrimitives` generating procedural Cubes, Spheres, and Quads with vertex normals and UV coordinates.
  - `PerspectiveCamera` with view/projection matrices, aspect ratio tracking, and camera movement.
  - Phong/Blinn-Phong lighting system supporting `AmbientLight` and `DirectionalLight`.
  - `Material` representation binding diffuse colors and textures.
  - `MeshRenderer` managing 3D pipeline state, constant buffer updates, and depth-stencil buffer binding.
  - Interactive `examples/Hello3D` showcasing an orbiting camera and lit textured cube.
- **Phase 5 — 2D Graphics Engine**:
  - High-performance `SpriteBatch` supporting textured quad batching with `SpriteSortMode.Deferred` and `SpriteSortMode.Immediate`.
  - `Camera2D` featuring origin-based positioning, panning, rotation, and continuous zoom.
  - Vector shape rendering: lines, circles, filled circles, stroked rectangles, and filled rectangles.
  - Procedural and textured `BitmapFont` text rendering.
  - Interactive `examples/Hello2D` demo with animated multi-sprite simulation and HUD.
- **Phase 4 — Minimal Renderer & Presentation**:
  - Complete graphics presentation pipeline delivering the first colored triangle on screen via clean Khefest API.
  - Direct3D 11 native presentation backend in `Khefest.Graphics.LowLevel`.
  - `examples/HelloTriangle` sample demonstrating hardware vertex processing and backbuffer presentation.
- **Phase 3 — GPU Layer Research Milestone**:
  - Authored Architectural Decision Record [`ADR-001: Windows GPU Path and Renderer Architecture`](docs/architecture/ADR-001-Windows-GPU-Path-and-Renderer-Architecture.md).
  - Designed pure Khefest hardware abstractions: `IGpuDevice`, `IGpuBuffer`, `IGpuTexture`, `IPipeline`, `ICommandRecorder`, `ISwapChain`.
  - Verified native DXGI/D3D presentation via isolated standalone spike.
- **Phase 2 — Win32 Platform Layer**:
  - Raw Win32 window creation, destruction, resizing, and fullscreen toggles using `CreateWindowExW`.
  - Message pump loop driven by `PeekMessageW` / `TranslateMessage` / `DispatchMessageW`.
  - Direct Win32 keyboard and mouse state tracking via `InputManager` and `Win32KeyMapper`.
  - High-precision timing driven by `QueryPerformanceCounter` via `Win32Timer` and `GameTime`.
  - Master `Game` base class and `KhefestApp.Run()` entry point.
  - `examples/HelloWindow` sample demonstrating application lifecycle.
- **Phase 1 — Core Architecture**:
  - Configured solution targeting strictly **.NET 10** (`net10.0` / `net10.0-windows`).
  - Structured error system with `KhefestResult<T>`, `KhefestErrorCode`, and subsystem exceptions.
  - Diagnostic logging architecture with `LogManager`, `ILogger`, log levels, and pluggable sinks.
  - Fluent `KhefestConfigBuilder` and immutable configuration records.
  - Master `ResourceManager` and abstract `ResourceBase` lifecycle tracker.
