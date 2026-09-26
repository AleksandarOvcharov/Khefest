using Khefest.Core.Configuration;
using Khefest.Windows.Application;

namespace KhefestTemplate;

public static class Program
{
    [STAThread]
    public static void Main()
    {
        var config = new KhefestConfigBuilder()
            .ConfigureWindow(w => w with
            {
                Title = "My Khefest Game",
                Width = 1280,
                Height = 720,
                VSync = true
            })
            .Build();

        var game = new MyGame();
        KhefestApp.Run(game, config);
    }
}
