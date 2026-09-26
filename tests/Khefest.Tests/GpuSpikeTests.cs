using Khefest.Core.Configuration;
using Khefest.Graphics.LowLevel;
using Khefest.Graphics.LowLevel.Spike;
using Khefest.Input;
using Khefest.Windowing;
using Khefest.Windows.Windowing;
using Xunit;
using Xunit.Abstractions;

namespace Khefest.Tests;

public class GpuSpikeTests
{
    private readonly ITestOutputHelper _output;

    public GpuSpikeTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void NativeGpuSpike_ExecutesPresentationCycleSuccessfully()
    {
        var config = new WindowConfig
        {
            Title = "Khefest Phase 3 GPU Presentation SPIKE",
            Width = 800,
            Height = 600,
            Mode = WindowMode.Windowed
        };

        var input = new InputManager();
        using var window = new Win32Window(config, input);

        var result = WindowsGpuSpike.Run(window, frameCount: 5, clearColor: Color4.CornflowerBlue);
        _output.WriteLine($"SPIKE Result: Success={result.IsSuccess}, Adapter={result.AdapterName}, Details={result.Details}");

        Assert.True(result.IsSuccess, $"GPU SPIKE failed: {result.Details}");
        Assert.Equal(5, result.FramesPresented);
    }
}
