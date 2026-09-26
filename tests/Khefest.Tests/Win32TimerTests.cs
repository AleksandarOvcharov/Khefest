using Khefest.Windows.Timing;
using Xunit;

namespace Khefest.Tests;

public class Win32TimerTests
{
    [Fact]
    public void Timer_AdvancesTimeAndCalculatesDeltas()
    {
        var timer = new Win32Timer();

        Thread.Sleep(15);
        var time1 = timer.Tick();

        Assert.True(time1.DeltaTimeSeconds > 0.005f);
        Assert.True(time1.TotalSeconds > 0.005);
        Assert.Equal(1, time1.FrameCount);

        Thread.Sleep(15);
        var time2 = timer.Tick();

        Assert.True(time2.DeltaTimeSeconds > 0.005f);
        Assert.True(time2.TotalSeconds > time1.TotalSeconds);
        Assert.Equal(2, time2.FrameCount);
    }
}
