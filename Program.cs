using SadConsole;
using SadConsole.Configuration;
using WonderGame.Core;
using WonderGame.Screens;

namespace WonderGame;

internal static class Program
{
    private static void Main(string[] args)
    {
        Settings.WindowTitle = "WonderGame";

        Builder gameStartup = new Builder()
            .SetScreenSize(GameSettings.GAME_WIDTH, GameSettings.GAME_HEIGHT)
            .SetStartingScreen<BootScreen>()
            .IsStartingScreenFocused(true)
            .ConfigureFonts(true);

        Game.Create(gameStartup);
        Game.Instance.Run();
        Game.Instance.Dispose();
    }
}