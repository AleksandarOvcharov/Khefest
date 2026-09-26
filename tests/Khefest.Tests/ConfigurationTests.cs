using Khefest.Core.Configuration;
using Xunit;

namespace Khefest.Tests;

public class ConfigurationTests
{
    [Fact]
    public void KhefestConfigBuilder_BuildsValidConfiguration()
    {
        var config = new KhefestConfigBuilder()
            .ConfigureWindow(w => w with
            {
                Title = "Custom Game Title",
                Width = 1920,
                Height = 1080,
                Mode = WindowMode.BorderlessFullscreen
            })
            .ConfigureMemory(m => m with
            {
                AutoMemManagement = false,
                EnableLeakTracking = true
            })
            .Set("Engine.Profile", "Development")
            .Build();

        Assert.Equal("Custom Game Title", config.Window.Title);
        Assert.Equal(1920, config.Window.Width);
        Assert.Equal(1080, config.Window.Height);
        Assert.Equal(WindowMode.BorderlessFullscreen, config.Window.Mode);
        Assert.False(config.Memory.AutoMemManagement);
        Assert.True(config.Memory.EnableLeakTracking);
        Assert.Equal("Development", config.GetSetting("Engine.Profile"));
    }

    [Fact]
    public void KhefestConfigBuilder_ThrowsOnInvalidWindowDimensions()
    {
        var builder = new KhefestConfigBuilder()
            .ConfigureWindow(w => w with { Width = -10, Height = 600 });

        Assert.Throws<ArgumentException>(() => builder.Build());
    }
}
