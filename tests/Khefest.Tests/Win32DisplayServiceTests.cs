using Khefest.Windows.Display;
using Xunit;

namespace Khefest.Tests;

public class Win32DisplayServiceTests
{
    [Fact]
    public void DisplayService_EnumeratesMonitorsSuccessfully()
    {
        var service = new Win32DisplayService();
        var monitors = service.GetMonitors();

        Assert.NotEmpty(monitors);

        var primary = service.GetPrimaryMonitor();
        Assert.NotNull(primary);
        Assert.True(primary.IsPrimary);
        Assert.True(primary.Width > 0);
        Assert.True(primary.Height > 0);
        Assert.True(primary.WorkAreaWidth > 0);
        Assert.True(primary.WorkAreaHeight > 0);
    }
}
