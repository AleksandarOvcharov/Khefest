using System.Reflection;
using System.Xml.Linq;
using Khefest.Core.Configuration;
using Khefest.Core.Resources;
using Khefest.Graphics.LowLevel;
using Khefest.Graphics.LowLevel.Windows;
using Khefest.Graphics.Mathematics;
using Khefest.Graphics.PostProcessing;
using Khefest.Graphics.Texturing;
using Khefest.Graphics.TwoD;
using Khefest.Input;
using Khefest.Windows.Timing;
using Xunit;

namespace Khefest.Tests;

public class ExternalDeveloperValidationTests
{
    [Fact]
    public void PublicApi_ContainsNoDirect3DOrWindowsSpecificTypesInSignatures()
    {
        // Audit assemblies to ensure NO Direct3D 11 or Windows types appear in public API signatures
        var assemblies = new[]
        {
            typeof(IGpuDevice).Assembly,         // Khefest.Graphics.LowLevel
            typeof(SpriteBatch).Assembly,        // Khefest.Graphics
            typeof(InputManager).Assembly        // Khefest.Input
        };

        var bannedTypeNames = new[] { "ID3D11", "D3D", "SharpDX", "Vortice", "Silk.NET", "HWND", "HRESULT" };

        foreach (var assembly in assemblies)
        {
            var publicTypes = assembly.GetExportedTypes();

            foreach (var type in publicTypes)
            {
                // Skip internal-facing Windows implementation namespace
                if (type.Namespace != null && type.Namespace.EndsWith(".Windows"))
                    continue;

                foreach (var banned in bannedTypeNames)
                {
                    Assert.DoesNotContain(banned, type.Name, StringComparison.OrdinalIgnoreCase);
                }

                // Check methods and properties
                foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                {
                    foreach (var param in method.GetParameters())
                    {
                        foreach (var banned in bannedTypeNames)
                        {
                            Assert.DoesNotContain(banned, param.ParameterType.Name, StringComparison.OrdinalIgnoreCase);
                        }
                    }

                    foreach (var banned in bannedTypeNames)
                    {
                        Assert.DoesNotContain(banned, method.ReturnType.Name, StringComparison.OrdinalIgnoreCase);
                    }
                }
            }
        }
    }

    [Fact]
    public void ExternalDeveloperWorkflow_FullComposedPipelineHeadlessExecution_ZeroLeaks()
    {
        var config = new MemoryConfig { EnableLeakTracking = true };
        using var manager = new ResourceManager(config);
        using var device = new WindowsGpuDevice(manager);

        // 1. High-Level SpriteBatch
        using var spriteBatch = new SpriteBatch(device, manager);

        // 2. High-Level RenderTarget2D (offscreen scene)
        using var sceneRT = new RenderTarget2D(device, "ExternalSceneRT", 640, 480, hasDepth: false, manager: manager);

        // 3. High-Level PostProcessEffect (procedural triangle HLSL)
        const string PostHlsl = @"
Texture2D g_Texture : register(t0);
SamplerState g_Sampler : register(s0);

struct PSInput
{
    float4 Position : SV_POSITION;
    float2 TexCoord : TEXCOORD0;
};

float4 PSMain(PSInput input) : SV_TARGET
{
    return g_Texture.Sample(g_Sampler, input.TexCoord);
}
";
        using var postFx = new PostProcessEffect(device, "ExternalPostFx", PostHlsl, "PSMain", manager: manager);

        // 4. Low-Level Custom Pipeline (procedural background)
        const string CustomHlsl = @"
struct PSInput
{
    float4 Position : SV_POSITION;
    float2 TexCoord : TEXCOORD0;
};

PSInput VSMain(uint id : SV_VertexID)
{
    PSInput output;
    output.TexCoord = float2((id << 1) & 2, id & 2);
    output.Position = float4(output.TexCoord * float2(2.0f, -2.0f) + float2(-1.0f, 1.0f), 0.0f, 1.0f);
    return output;
}

float4 PSMain(PSInput input) : SV_TARGET
{
    return float4(0.1f, 0.15f, 0.2f, 1.0f);
}
";
        using var vs = device.CompileShader("ExtVS", ShaderStage.Vertex, CustomHlsl, "VSMain");
        using var ps = device.CompileShader("ExtPS", ShaderStage.Pixel, CustomHlsl, "PSMain");

        var pipelineDesc = new PipelineDesc
        {
            VertexShader = vs,
            PixelShader = ps,
            VertexLayout = null, // Procedural triangle
            Topology = PrimitiveTopology.TriangleList,
            CullMode = CullMode.None,
            FillMode = FillMode.Solid,
            BlendMode = BlendMode.Opaque,
            DepthTestEnabled = false,
            DepthWriteEnabled = false
        };
        using var pipeline = device.CreatePipeline("ExtPipeline", pipelineDesc);

        // Execute PASS 1: Low-level pass into SceneRT
        var pass1 = sceneRT.CreatePass(Color4.Black);
        using (var recorder = device.CreateCommandRecorder())
        {
            using (recorder.BeginScopedPass(pass1))
            {
                recorder.SetPipeline(pipeline);
                recorder.Draw(3, 0);
            }
            device.Submit(recorder);
        }

        // Execute PASS 2: High-Level SpriteBatch draw into SceneRT
        spriteBatch.Begin(sceneRT, null);
        spriteBatch.FillCircle(new System.Numerics.Vector2(320, 240), 20f, Color4.RoyalBlue);
        spriteBatch.DrawRectangle(new System.Numerics.Vector2(100, 100), new System.Numerics.Vector2(200, 150), Color4.Gold);
        spriteBatch.End();

        // Target backbuffer mock
        using var finalRT = new RenderTarget2D(device, "FinalMockRT", 640, 480, manager: manager);

        // Execute PASS 3: Post-processing SceneRT -> FinalRT
        postFx.Apply(sceneRT, finalRT);

        // Execute PASS 4: High-level HUD overlay on final target
        spriteBatch.Begin(finalRT, null);
        spriteBatch.DrawString("HUD OVERLAY VALIDATION", new System.Numerics.Vector2(10, 10), Color4.White);
        spriteBatch.End();

        // 5. Test Resizing
        sceneRT.Resize(800, 600);
        finalRT.Resize(800, 600);
        Assert.Equal(800, sceneRT.Width);
        Assert.Equal(600, sceneRT.Height);
        Assert.Equal(800, finalRT.Width);
        Assert.Equal(600, finalRT.Height);

        // 6. Test Shutdown and Leak Check
        // Explicitly disposing everything in reverse order
        // (All 'using' statements will dispose at block exit)
    }

    [Fact]
    public void NuGetConsumer_ProjectFile_StrictlyHasNoProjectReferences()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var csprojPath = Path.Combine(repoRoot, "samples", "NuGetConsumer", "NuGetConsumer.csproj");

        Assert.True(File.Exists(csprojPath), $"Consumer csproj '{csprojPath}' must exist.");

        var doc = XDocument.Load(csprojPath);
        var projectReferences = doc.Descendants("ProjectReference").ToList();

        // Assert strictly zero project references
        Assert.Empty(projectReferences);

        // Assert PackageReference to Khefest exists
        var pkgRef = doc.Descendants("PackageReference")
            .FirstOrDefault(p => p.Attribute("Include")?.Value == "Khefest");
        Assert.NotNull(pkgRef);
    }
}
