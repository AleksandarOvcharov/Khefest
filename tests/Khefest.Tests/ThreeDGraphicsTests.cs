using System.Numerics;
using Khefest.Core.Configuration;
using Khefest.Core.Resources;
using Khefest.Graphics.Camera;
using Khefest.Graphics.LowLevel;
using Khefest.Graphics.LowLevel.Windows;
using Khefest.Graphics.Texturing;
using Khefest.Graphics.ThreeD;
using Khefest.Graphics.ThreeD.Lighting;
using Xunit;

namespace Khefest.Tests;

public class ThreeDGraphicsTests
{
    [Fact]
    public void PerspectiveCamera_CalculatesValidMatricesAndOrbits()
    {
        var camera = new PerspectiveCamera(
            new Vector3(0, 5, -10),
            Vector3.Zero,
            16f / 9f,
            fovDegrees: 60f,
            nearPlane: 0.1f,
            farPlane: 1000f);

        var view = camera.ViewMatrix;
        var proj = camera.ProjectionMatrix;
        var viewProj = camera.ViewProjectionMatrix;

        Assert.True(Matrix4x4.Invert(view, out _), "View matrix should be invertible.");
        Assert.True(Matrix4x4.Invert(proj, out _), "Projection matrix should be invertible.");
        Assert.True(Matrix4x4.Invert(viewProj, out _), "ViewProjection matrix should be invertible.");

        var initialPos = camera.Position;
        float initialDist = Vector3.Distance(initialPos, camera.Target);

        camera.Orbit(0.5f, 0.2f, 0.0f);

        float newDist = Vector3.Distance(camera.Position, camera.Target);
        Assert.Equal(initialDist, newDist, 0.01f);
        Assert.NotEqual(initialPos, camera.Position);
    }

    [Fact]
    public void MeshPrimitives_GeneratesAccurateTopology()
    {
        using var device = new WindowsGpuDevice();

        // 1. Cube
        using var cube = MeshPrimitives.CreateCube(device, 2.0f);
        Assert.Equal(24, cube.VertexCount);
        Assert.Equal(36, cube.IndexCount);
        Assert.Equal(new Vector3(-1.0f), cube.Bounds.Min);
        Assert.Equal(new Vector3(1.0f), cube.Bounds.Max);

        // 2. Sphere
        using var sphere = MeshPrimitives.CreateSphere(device, 1.0f, segments: 16, rings: 8);
        Assert.True(sphere.VertexCount > 0);
        Assert.True(sphere.IndexCount > 0);
        Assert.Equal(new Vector3(-1.0f), sphere.Bounds.Min);
        Assert.Equal(new Vector3(1.0f), sphere.Bounds.Max);

        // 3. Plane
        using var plane = MeshPrimitives.CreatePlane(device, 5.0f, 5.0f);
        Assert.Equal(4, plane.VertexCount);
        Assert.Equal(6, plane.IndexCount);
    }

    [Fact]
    public void MeshRenderer_RendersWithDepthAndLighting_ZeroLeaks()
    {
        var resources = new ResourceManager(new MemoryConfig { AutoMemManagement = false, EnableLeakTracking = true });
        var device = new WindowsGpuDevice(resources);

        var colorTarget = device.CreateTexture(
            "ColorTarget",
            256,
            256,
            GpuFormat.R8G8B8A8_UNorm,
            TextureUsage.RenderTarget | TextureUsage.Sampled);

        var depthTarget = device.CreateTexture(
            "DepthTarget",
            256,
            256,
            GpuFormat.D24_UNorm_S8_UInt,
            TextureUsage.DepthStencil);

        var renderer = new MeshRenderer(device, resources);
        var cubeMesh = MeshPrimitives.CreateCube(device, 1.0f, resources);
        var sphereMesh = MeshPrimitives.CreateSphere(device, 0.5f, 16, 8, resources);

        var camera = new PerspectiveCamera(
            new Vector3(0, 2, -4),
            Vector3.Zero,
            1.0f,
            fovDegrees: 60f);

        var dirLight = new DirectionalLight(new Vector3(-1, -2, 1), new Vector3(1f, 0.95f, 0.8f), 1.2f);
        var ambientLight = new AmbientLight(new Vector3(0.2f, 0.25f, 0.3f), 0.3f);

        renderer.Begin(
            colorTarget,
            depthTarget,
            camera,
            dirLight,
            ambientLight,
            clearColor: true,
            clearDepth: true);

        // Draw cube
        renderer.Draw(cubeMesh, null, Matrix4x4.CreateRotationY(0.5f));

        // Draw sphere slightly behind/above
        renderer.Draw(sphereMesh, null, Matrix4x4.CreateTranslation(0f, 1f, 2f));

        renderer.End();
        device.WaitForGpu();

        // Dispose all tracked resources explicitly
        cubeMesh.Dispose();
        sphereMesh.Dispose();
        renderer.Dispose();
        colorTarget.Dispose();
        depthTarget.Dispose();
        device.Dispose();

        var leaks = resources.Tracker.CheckForLeaks();
        Assert.Empty(leaks);
    }

    [Fact]
    public void Model_CompositePartRendering_WorksCorrectly()
    {
        using var device = new WindowsGpuDevice();
        using var cube = MeshPrimitives.CreateCube(device, 1.0f);
        using var sphere = MeshPrimitives.CreateSphere(device, 0.5f, 12, 6);

        var mat1 = new Material("Mat1", color: new Vector4(1, 0, 0, 1));
        var mat2 = new Material("Mat2", color: new Vector4(0, 1, 0, 1));

        var model = new Model(
            "CompositeModel",
            new[]
            {
                new ModelPart(cube, mat1, Matrix4x4.Identity),
                new ModelPart(sphere, mat2, Matrix4x4.CreateTranslation(0, 1.5f, 0))
            },
            ownsResources: false);

        Assert.Equal(2, model.Parts.Count);
        Assert.Equal(cube, model.Parts[0].Mesh);
        Assert.Equal(sphere, model.Parts[1].Mesh);
    }
}
