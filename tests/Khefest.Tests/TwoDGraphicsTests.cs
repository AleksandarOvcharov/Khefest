using System.Numerics;
using Khefest.Core.Configuration;
using Khefest.Core.Resources;
using Khefest.Graphics.Camera;
using Khefest.Graphics.LowLevel;
using Khefest.Graphics.LowLevel.Windows;
using Khefest.Graphics.Text;
using Khefest.Graphics.Texturing;
using Khefest.Graphics.TwoD;

namespace Khefest.Tests;

public sealed class TwoDGraphicsTests
{
    [Fact]
    public void Camera2D_InitializesWithCorrectViewportAndMatrices()
    {
        var camera = new Camera2D(1280, 720);
        Assert.Equal(1280, camera.ViewportWidth);
        Assert.Equal(720, camera.ViewportHeight);
        Assert.Equal(new Vector2(640, 360), camera.Origin);

        var view = camera.GetViewMatrix();
        var proj = camera.GetProjectionMatrix();
        var viewProj = camera.GetViewProjectionMatrix();

        Assert.False(Matrix4x4.Identity.Equals(view));
        Assert.False(Matrix4x4.Identity.Equals(proj));
        Assert.False(Matrix4x4.Identity.Equals(viewProj));
    }

    [Fact]
    public void Camera2D_ScreenToWorldAndBack_PreservesCoordinates()
    {
        var camera = new Camera2D(800, 600)
        {
            Position = new Vector2(100, 200),
            Zoom = 1.5f,
            Rotation = 0.25f
        };

        var screenPoint = new Vector2(400, 300);
        var worldPoint = camera.ScreenToWorld(screenPoint);
        var projectedBack = camera.WorldToScreen(worldPoint);

        Assert.Equal(screenPoint.X, projectedBack.X, 2);
        Assert.Equal(screenPoint.Y, projectedBack.Y, 2);
    }

    [Fact]
    public void Camera2D_GetVisibleBounds_EncompassesViewport()
    {
        var camera = new Camera2D(1000, 800)
        {
            Position = new Vector2(0, 0),
            Zoom = 1.0f
        };

        var (min, max) = camera.GetVisibleBounds();

        // With origin at center (500, 400), visible bounds should be (-500, -400) to (500, 400)
        Assert.True(min.X < 0);
        Assert.True(min.Y < 0);
        Assert.True(max.X > 0);
        Assert.True(max.Y > 0);
        Assert.Equal(1000f, max.X - min.X, 1);
        Assert.Equal(800f, max.Y - min.Y, 1);
    }

    [Fact]
    public void TextureRegion_ComputesNormalizedUVCoordinatesAccurately()
    {
        using var resources = new ResourceManager(new MemoryConfig { AutoMemManagement = false, EnableLeakTracking = true });
        using var device = new WindowsGpuDevice(resources);

        using var texture = Texture2D.CreateSolid(device, "TestTex", 100, 200, Color4.Red, resources);
        var region = new TextureRegion(texture, 25, 50, 50, 100);

        Assert.Equal(0.25f, region.U0, 3);
        Assert.Equal(0.25f, region.V0, 3);
        Assert.Equal(0.75f, region.U1, 3);
        Assert.Equal(0.75f, region.V1, 3);
        Assert.Equal(50, region.Width);
        Assert.Equal(100, region.Height);
    }

    [Fact]
    public void Sprite_InitializesWithCorrectDimensions()
    {
        using var resources = new ResourceManager(new MemoryConfig { AutoMemManagement = false, EnableLeakTracking = true });
        using var device = new WindowsGpuDevice(resources);

        using var texture = Texture2D.CreateSolid(device, "SpriteTex", 64, 32, Color4.White, resources);
        var sprite = new Sprite(texture)
        {
            Scale = new Vector2(2.0f, 3.0f)
        };

        Assert.Equal(128.0f, sprite.Width);
        Assert.Equal(96.0f, sprite.Height);

        sprite.CenterOrigin();
        Assert.Equal(new Vector2(32.0f, 16.0f), sprite.Origin);
    }

    [Fact]
    public void BitmapFont_MeasuresStringAndDrawsTextAccurately()
    {
        using var resources = new ResourceManager(new MemoryConfig { AutoMemManagement = false, EnableLeakTracking = true });
        using var device = new WindowsGpuDevice(resources);

        var font = DefaultFont.Create(device, resources);
        Assert.NotNull(font);

        var singleLineSize = font.MeasureString("Khefest");
        Assert.True(singleLineSize.X > 0);
        Assert.True(singleLineSize.Y > 0);

        var multiLineSize = font.MeasureString("Line 1\nLine 2");
        Assert.True(multiLineSize.Y > singleLineSize.Y);

        font.Texture.Dispose();
    }

    [Fact]
    public void SpriteBatch_DrawsAndBatchesQuadsWithoutError()
    {
        using var resources = new ResourceManager(new MemoryConfig { AutoMemManagement = false, EnableLeakTracking = true });
        using var device = new WindowsGpuDevice(resources);

        using var target = device.CreateTexture("RenderTarget", 800, 600, GpuFormat.R8G8B8A8_UNorm, TextureUsage.RenderTarget | TextureUsage.Sampled);
        using var batch = new SpriteBatch(device, resources);
        using var customTex = Texture2D.CreateSolid(device, "CustomTex", 32, 32, Color4.Green, resources);

        var camera = new Camera2D(800, 600);

        // 1. Begin batch
        batch.Begin(target, camera, SpriteSortMode.Deferred);

        // 2. Draw textured sprites
        batch.Draw(customTex, new Vector2(50, 50), Color4.White);
        batch.Draw(customTex, new Vector2(100, 100), new Vector2(64, 64), Color4.Red);

        // 3. Draw vector shapes
        batch.DrawLine(new Vector2(0, 0), new Vector2(800, 600), Color4.Blue, 2.0f);
        batch.DrawRectangle(new Vector2(200, 200), new Vector2(150, 80), Color4.Yellow, 3.0f);
        batch.FillRectangle(new Vector2(400, 300), new Vector2(80, 80), Color4.Cyan);
        batch.DrawCircle(new Vector2(500, 200), 40.0f, Color4.Magenta, 16);
        batch.FillCircle(new Vector2(650, 200), 30.0f, Color4.White, 16);
        batch.DrawTriangle(new Vector2(100, 400), new Vector2(150, 500), new Vector2(50, 500), Color4.Green);

        // 4. Draw string text
        batch.DrawString("Khefest 2D Engine", new Vector2(10, 10), Color4.White, 2.0f);

        // 5. End batch and submit
        batch.End();
        device.WaitForGpu();
    }

    [Fact]
    public void SpriteBatch_StrictMemoryMode_NoLeaks()
    {
        var resources = new ResourceManager(new MemoryConfig { AutoMemManagement = false, EnableLeakTracking = true });
        var device = new WindowsGpuDevice(resources);

        var target = device.CreateTexture("RenderTarget", 400, 300, GpuFormat.R8G8B8A8_UNorm, TextureUsage.RenderTarget | TextureUsage.Sampled);
        var batch = new SpriteBatch(device, resources);
        var tex = Texture2D.CreateSolid(device, "LeakTestTex", 16, 16, Color4.Blue, resources);

        batch.Begin(target);
        batch.Draw(tex, new Vector2(10, 10));
        batch.End();
        device.WaitForGpu();

        // Explicitly dispose all resources
        tex.Dispose();
        batch.Dispose();
        target.Dispose();
        device.Dispose();

        // Verify leak check detects zero leaks
        var leaks = resources.Tracker.CheckForLeaks();
        Assert.Empty(leaks);
    }

    [Fact]
    public void SpriteBatch_BeginWithClearColor_ExecutesWithoutError()
    {
        using var resources = new ResourceManager(new MemoryConfig { AutoMemManagement = false, EnableLeakTracking = true });
        using var device = new WindowsGpuDevice(resources);

        using var target = device.CreateTexture("ClearTarget", 400, 300, GpuFormat.R8G8B8A8_UNorm, TextureUsage.RenderTarget | TextureUsage.Sampled);
        using var batch = new SpriteBatch(device, resources);

        var clearColor = new Color4(0.05f, 0.1f, 0.15f, 1.0f);
        batch.Begin(target, camera: null, SpriteSortMode.Deferred, clearColor);
        batch.FillCircle(new Vector2(200, 150), 40f, Color4.CornflowerBlue);
        batch.End();
        device.WaitForGpu();
    }

    [Fact]
    public void WindowsGpuBuffer_PartialSubresourceUpdate_SucceedsWithBoxBounds()
    {
        using var resources = new ResourceManager(new MemoryConfig { AutoMemManagement = false, EnableLeakTracking = true });
        using var device = new WindowsGpuDevice(resources);

        int totalBytes = 1024;
        using var buffer = device.CreateBuffer("SubresourceTestBuffer", totalBytes, BufferUsage.Vertex);

        // Upload partial data at offset
        var subData = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        buffer.SetData<byte>(subData, offsetInBytes: 64);

        // Upload single value at offset
        int value = 0x12345678;
        buffer.SetData<int>(in value, offsetInBytes: 128);

        device.WaitForGpu();
    }
}
