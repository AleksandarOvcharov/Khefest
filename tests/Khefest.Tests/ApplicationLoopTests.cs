using Khefest.Core.Configuration;
using Khefest.Windows.Application;
using Khefest.Windows.Timing;
using Xunit;

namespace Khefest.Tests;

public class ApplicationLoopTests
{
    private sealed class LifecycleTestGame : Game
    {
        public bool Initialized { get; private set; }
        public int UpdateCount { get; private set; }
        public int RenderCount { get; private set; }
        public bool ShutDownCompleted { get; private set; }

        public override void Initialize()
        {
            Initialized = true;
        }

        public override void Update(GameTime time)
        {
            UpdateCount++;
            if (UpdateCount >= 3)
            {
                Exit();
            }
        }

        public override void Render(GameTime time)
        {
            RenderCount++;
        }

        public override void Shutdown()
        {
            ShutDownCompleted = true;
        }
    }

    [Fact]
    public void KhefestApp_ExecutesFullLifecycleLoop()
    {
        var game = new LifecycleTestGame();
        var config = new KhefestConfigBuilder()
            .ConfigureWindow(w => w with
            {
                Title = "Lifecycle Test Window",
                Width = 640,
                Height = 480
            })
            .ConfigureLogging(l => l with
            {
                EnableConsoleSink = false
            })
            .Build();

        KhefestApp.Run(game, config);

        Assert.True(game.Initialized);
        Assert.True(game.UpdateCount >= 3);
        Assert.True(game.RenderCount >= 3);
        Assert.True(game.ShutDownCompleted);
    }
}
