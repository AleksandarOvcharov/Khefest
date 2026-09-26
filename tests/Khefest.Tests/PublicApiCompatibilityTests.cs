using System.IO.Compression;
using System.Reflection;
using System.Xml.Linq;
using Khefest.Core.Configuration;
using Khefest.Core.Logging;
using Khefest.Core.Resources;
using Khefest.Graphics.Camera;
using Khefest.Graphics.LowLevel;
using Khefest.Graphics.PostProcessing;
using Khefest.Graphics.Texturing;
using Khefest.Graphics.TwoD;
using Khefest.Input;
using Khefest.Windowing;
using Khefest.Windows.Application;
using Khefest.Windows.Timing;
using Xunit;

namespace Khefest.Tests;

/// <summary>
/// Milestone 1.5.0 / 1.6.0: Public API Stabilization, Compatibility, and Packaging Tests.
/// Prevents accidental breaking changes, ensures naming consistency,
/// performs signature-based baseline verification, and audits packaging invariants.
/// </summary>
public sealed class PublicApiCompatibilityTests
{
    private static readonly Assembly[] PublicAssemblies =
    [
        typeof(KhefestApp).Assembly,                     // Khefest.Windows
        typeof(SpriteBatch).Assembly,                     // Khefest.Graphics
        typeof(IGpuDevice).Assembly,                      // Khefest.Graphics.LowLevel
        typeof(InputManager).Assembly,                    // Khefest.Input
        typeof(IWindow).Assembly,                         // Khefest.Windowing
        typeof(ResourceManager).Assembly                  // Khefest.Core
    ];

    [Fact]
    public void CanonicalTexture_IsProperlyExposedOnGpuTextureAndRenderTarget2D()
    {
        // Assert IGpuTexture defines CanonicalTexture
        var gpuTexProp = typeof(IGpuTexture).GetProperty(nameof(IGpuTexture.CanonicalTexture));
        Assert.NotNull(gpuTexProp);
        Assert.Equal(typeof(IGpuTexture), gpuTexProp.PropertyType);

        // Assert RenderTarget2D implements CanonicalTexture
        var rtProp = typeof(RenderTarget2D).GetProperty(nameof(RenderTarget2D.CanonicalTexture));
        Assert.NotNull(rtProp);
        Assert.Equal(typeof(IGpuTexture), rtProp.PropertyType);
    }

    [Fact]
    public void CorePublicTypes_ExistAndAreConsistent()
    {
        // Verify key public types are all public and in expected namespaces
        var requiredTypes = new[]
        {
            typeof(KhefestApp),
            typeof(Game),
            typeof(GameTime),
            typeof(SpriteBatch),
            typeof(Camera2D),
            typeof(RenderTarget2D),
            typeof(PostProcessEffect),
            typeof(IGpuDevice),
            typeof(IGpuBuffer),
            typeof(IGpuTexture),
            typeof(IPipeline),
            typeof(ICommandRecorder),
            typeof(ISwapChain),
            typeof(RenderPassDesc),
            typeof(ScopedRenderPass),
            typeof(PipelineDesc),
            typeof(Color4),
            typeof(IInputService),
            typeof(Key),
            typeof(MouseButton),
            typeof(IWindow),
            typeof(WindowConfig),
            typeof(ResourceManager),
            typeof(ResourceLifetimeTracker),
            typeof(ILogger)
        };

        foreach (var type in requiredTypes)
        {
            Assert.True(type.IsPublic || type.IsNestedPublic, $"Type {type.FullName} must be public.");
        }
    }

    [Fact]
    public void LowLevelInterfaceSignatures_HaveNotBroken()
    {
        // IGpuBuffer SetData overloads
        var bufferType = typeof(IGpuBuffer);
        var spanSetData = bufferType.GetMethods().FirstOrDefault(m => m.Name == "SetData" && m.GetParameters().Length == 2 && m.GetParameters()[0].ParameterType.Name.StartsWith("ReadOnlySpan"));
        Assert.NotNull(spanSetData);
        var inSetData = bufferType.GetMethods().FirstOrDefault(m => m.Name == "SetData" && m.GetParameters().Length == 2 && m.GetParameters()[0].ParameterType.IsByRef);
        Assert.NotNull(inSetData);

        // ICommandRecorder ScopedPass and Pass methods
        var recorderType = typeof(ICommandRecorder);
        Assert.NotNull(recorderType.GetMethod("BeginPass"));
        Assert.NotNull(recorderType.GetMethod("BeginScopedPass"));
        Assert.NotNull(recorderType.GetMethod("EndPass"));
        Assert.NotNull(recorderType.GetProperty("IsInPass"));

        // RenderTarget2D Resize and CreatePass methods
        var rtType = typeof(RenderTarget2D);
        Assert.NotNull(rtType.GetMethod("Resize"));
        Assert.NotNull(rtType.GetMethod("CreatePass"));
    }

    [Fact]
    public void PublicApiSignatures_MatchOrExtendBaseline_WithoutBreakingChanges()
    {
        var currentSignatures = ApiSignatureExtractor.ExtractPublicSignatures(PublicAssemblies);
        Assert.NotEmpty(currentSignatures);

        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var baselinePath = Path.Combine(repoRoot, "tests", "Khefest.Tests", "ApiBaselines", "Khefest.Api.Baseline.1.5.0.txt");

        // If baseline file does not exist, create it
        if (!File.Exists(baselinePath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(baselinePath)!);
            File.WriteAllLines(baselinePath, currentSignatures);
        }

        var baselineSignatures = File.ReadAllLines(baselinePath)
            .Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith("#"))
            .ToHashSet();

        var currentSet = currentSignatures.ToHashSet();

        // Detect any removed or modified signatures (breaking changes)
        var removedSignatures = baselineSignatures.Except(currentSet).ToList();

        Assert.True(removedSignatures.Count == 0,
            $"Detected {removedSignatures.Count} breaking public API signature changes:\n" +
            string.Join("\n", removedSignatures));
    }

    [Fact]
    public void DocumentationExampleSnippet_CompilesAndExecutesHeadlessly()
    {
        // Emulates the exact starter game loop published in README.md and docs/developer-guide.md
        var resources = new ResourceManager(new MemoryConfig { EnableLeakTracking = true });

        var config = new KhefestConfigBuilder()
            .ConfigureWindow(w => w with { Title = "DocVerificationGame", Width = 800, Height = 600, VSync = false })
            .Build();

        // 1. Initialize
        var gameTime = new GameTime(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(0.016), 60, 60.0f);
        var camera = new Camera2D(config.Window.Width, config.Window.Height);

        // 2. Update
        camera.Move(new System.Numerics.Vector2(10, 0));

        Assert.Equal(10f, camera.Position.X);
        Assert.Equal(0.016f, gameTime.DeltaTime, 3);
        Assert.NotEqual(System.Numerics.Matrix4x4.Identity, camera.GetViewMatrix());

        // 3. Cleanup & verify zero leaks
        resources.Dispose();
        Assert.Empty(resources.Tracker.CheckForLeaks());
    }

    [Fact]
    public void GeneratedPackages_ContainAssembliesAndXmlDocumentation()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var packageDir = Path.Combine(repoRoot, "artifacts", "packages");

        if (Directory.Exists(packageDir))
        {
            var nupkgs = Directory.GetFiles(packageDir, "*.nupkg").Where(f => !f.EndsWith(".symbols.nupkg")).ToList();
            if (nupkgs.Count > 0)
            {
                // Verify Khefest.Core package
                var corePkg = nupkgs.FirstOrDefault(p => Path.GetFileName(p).StartsWith("Khefest.Core."));
                if (corePkg != null)
                {
                    using var archive = ZipFile.OpenRead(corePkg);
                    Assert.Contains(archive.Entries, e => e.FullName.EndsWith("Khefest.Core.dll", StringComparison.OrdinalIgnoreCase));
                    Assert.Contains(archive.Entries, e => e.FullName.EndsWith("Khefest.Core.xml", StringComparison.OrdinalIgnoreCase));
                }

                // Verify metapackage Khefest
                var metaPkg = nupkgs.FirstOrDefault(p => Path.GetFileName(p).StartsWith("Khefest.1.") || Path.GetFileName(p).StartsWith("Khefest.2."));
                Assert.NotNull(metaPkg);
            }
        }
    }

    [Fact]
    public void Metapackage_DependencyGraphAudit_ReferencesAllSubsystems()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var packageDir = Path.Combine(repoRoot, "artifacts", "packages");

        if (Directory.Exists(packageDir))
        {
            var metaPkg = Directory.GetFiles(packageDir, "Khefest.1.*.nupkg").FirstOrDefault(f => !f.EndsWith(".symbols.nupkg") && !Path.GetFileName(f).StartsWith("Khefest.Templates"));
            if (metaPkg != null)
            {
                using var archive = ZipFile.OpenRead(metaPkg);
                var nuspecEntry = archive.Entries.FirstOrDefault(e => e.FullName.EndsWith(".nuspec"));
                Assert.NotNull(nuspecEntry);

                using var stream = nuspecEntry.Open();
                var doc = XDocument.Load(stream);
                XNamespace ns = doc.Root?.GetDefaultNamespace() ?? XNamespace.None;

                var dependencies = doc.Descendants(ns + "dependency")
                    .Select(d => d.Attribute("id")?.Value)
                    .Where(id => id != null)
                    .ToList();

                var expectedSubsystems = new[]
                {
                    "Khefest.Core",
                    "Khefest.Windowing",
                    "Khefest.Input",
                    "Khefest.Graphics.LowLevel",
                    "Khefest.Graphics",
                    "Khefest.UI",
                    "Khefest.Windows"
                };

                foreach (var expected in expectedSubsystems)
                {
                    Assert.Contains(expected, dependencies);
                }
            }
        }
    }

    [Fact]
    public void TemplatePackage_ContainsExpectedStructureAndMetadata()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var packageDir = Path.Combine(repoRoot, "artifacts", "packages");

        if (Directory.Exists(packageDir))
        {
            var templatePkg = Directory.GetFiles(packageDir, "Khefest.Templates.*.nupkg").FirstOrDefault();
            if (templatePkg != null)
            {
                using var archive = ZipFile.OpenRead(templatePkg);

                // Assert template manifest and core project files exist in package content
                Assert.Contains(archive.Entries, e => e.FullName.Replace('\\', '/').Contains(".template.config/template.json", StringComparison.OrdinalIgnoreCase));
                Assert.Contains(archive.Entries, e => e.FullName.EndsWith("KhefestTemplate.csproj", StringComparison.OrdinalIgnoreCase));
                Assert.Contains(archive.Entries, e => e.FullName.EndsWith("Program.cs", StringComparison.OrdinalIgnoreCase));
                Assert.Contains(archive.Entries, e => e.FullName.EndsWith("Game.cs", StringComparison.OrdinalIgnoreCase));
            }
        }
    }
}
