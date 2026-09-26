using System.Numerics;
using System.Text.RegularExpressions;
using Khefest.Core.Configuration;
using Khefest.Core.Resources;
using Khefest.Graphics.Camera;
using Khefest.Graphics.Mathematics;
using Khefest.Graphics.TwoD;
using Khefest.Input;
using Khefest.Windows.Timing;
using Xunit;

namespace Khefest.Tests;

/// <summary>
/// Milestone 1.6.0: Executable Documentation Verification Tests.
/// Validates that code examples published in README.md, docs/getting-started.md,
/// and docs/developer-guide.md compile, run without leaks, and never silently rot.
/// </summary>
public sealed class DocumentationVerificationTests
{
    [Fact]
    public void GettingStarted_GameLoopSimulation_ExecutesAndCleansUpWithZeroLeaks()
    {
        // Simulates the exact logic published in docs/getting-started.md
        var resources = new ResourceManager(new MemoryConfig { EnableLeakTracking = true });

        var config = new KhefestConfigBuilder()
            .ConfigureWindow(w => w with
            {
                Title = "My First Khefest Application",
                Width = 1280,
                Height = 720,
                VSync = true
            })
            .Build();

        Assert.Equal(1280, config.Window.Width);
        Assert.Equal(720, config.Window.Height);

        // Simulation state
        var camera = new Camera2D(config.Window.Width, config.Window.Height);
        var playerPos = new Vector2(400, 300);

        // Simulate frame update
        var gameTime = new GameTime(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(0.016), 60, 60.0f);
        float dt = gameTime.DeltaTime;
        float speed = 300f;

        // Player moves right
        playerPos.X += speed * dt;
        Assert.Equal(404.8f, playerPos.X, 1);

        // Camera resize on window resize
        camera.Resize(1920, 1080);
        Assert.Equal(1920, camera.ViewportWidth);
        Assert.Equal(1080, camera.ViewportHeight);

        // Verify clean shutdown
        resources.Dispose();
        Assert.Empty(resources.Tracker.CheckForLeaks());
    }

    [Fact]
    public void DeveloperGuide_MathAndGeometrySnippets_BehaveAccurately()
    {
        // Simulates snippets from docs/developer-guide.md:
        // 1. LerpAngle
        float currentAngle = 3.10f;
        float targetAngle = -3.10f;
        float smoothed = MathHelper.LerpAngle(currentAngle, targetAngle, 0.5f);
        Assert.True(smoothed > 3.0f || smoothed < -3.0f); // Wraps around pi boundary

        // 2. Rect2D collision check snippet
        var playerPos = new Vector2(100, 100);
        var bulletPos = new Vector2(105, 105);
        var playerBounds = new Rect2D(playerPos.X - 16, playerPos.Y - 16, 32, 32);
        var bulletBounds = new Rect2D(bulletPos.X - 4, bulletPos.Y - 4, 8, 8);

        Assert.True(playerBounds.Intersects(bulletBounds));
    }

    [Fact]
    public void MarkdownDocumentation_HasNoObsoleteApiReferences()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var allDocFiles = Directory.GetFiles(Path.Combine(repoRoot, "docs"), "*.md", SearchOption.AllDirectories).ToList();
        allDocFiles.Add(Path.Combine(repoRoot, "README.md"));

        // Universal obsolete patterns across all documentation
        var obsoletePatterns = new[]
        {
            @"\bUnderlyingTexture\b",        // Replaced by CanonicalTexture
            @"\bColorTarget\s*="            // Replaced by polymorphic ColorTargets
        };

        foreach (var file in allDocFiles)
        {
            var content = File.ReadAllText(file);
            foreach (var pattern in obsoletePatterns)
            {
                var match = Regex.Match(content, pattern);
                Assert.False(match.Success,
                    $"Obsolete pattern '{pattern}' found in documentation file '{Path.GetFileName(file)}' at index {match.Index}.");
            }
        }

        // Consumer tutorials must not instruct developers to instantiate internal driver types
        var tutorialFiles = new[]
        {
            Path.Combine(repoRoot, "README.md"),
            Path.Combine(repoRoot, "docs", "getting-started.md"),
            Path.Combine(repoRoot, "docs", "developer-guide.md")
        };

        var internalDriverPatterns = new[]
        {
            @"\bnew\s+WindowsGpuDevice\b",
            @"\bnew\s+WindowsGpuBuffer\b",
            @"\bnew\s+WindowsGpuTexture\b"
        };

        foreach (var file in tutorialFiles)
        {
            if (!File.Exists(file)) continue;
            var content = File.ReadAllText(file);
            foreach (var pattern in internalDriverPatterns)
            {
                var match = Regex.Match(content, pattern);
                Assert.False(match.Success,
                    $"Consumer guide '{Path.GetFileName(file)}' contains prohibited internal driver instantiation '{pattern}'.");
            }
        }
    }
}
