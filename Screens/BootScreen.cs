using SadConsole;
using SadConsole.Components;
using WonderGame.Core;
using SadRogue.Primitives;

namespace WonderGame.Screens;

internal class BootScreen : SadConsole.ScreenObject
{
    private readonly SadConsole.Console _console;
    private readonly SadConsole.Components.Timer _bootTimer;
    private int _bootMessageIndex;

    private readonly string[] _bootMessages =
    {
        "INITIALIZING...",
        "LOADING KERNEL...",
        "VERIFYING SYSTEM INTEGRITY...",
        "DECRYPTING DATA STREAMS...",
        "ESTABLISHING SECURE CONNECTION...",
        "LOADING INTERFACE...",
        "BOOT SEQUENCE COMPLETE.",
        ""
    };

    public BootScreen()
    {
        _console = new SadConsole.Console(GameSettings.GAME_WIDTH, GameSettings.GAME_HEIGHT);
        _console.Surface.Fill(GameSettings.THEME_BACKGROUND, GameSettings.THEME_BACKGROUND, 0);
        _console.Surface.DefaultForeground = GameSettings.THEME_FOREGROUND;
        Children.Add(_console);

        _bootTimer = new SadConsole.Components.Timer(TimeSpan.FromSeconds(0.5));
        _bootTimer.TimerElapsed += (timer, e) => PrintNextBootMessage();
        _console.SadComponents.Add(_bootTimer);
    }

    private void PrintNextBootMessage()
    {
        if (_bootMessageIndex < _bootMessages.Length)
        {
            _console.Cursor.Position = new Point(0, _bootMessageIndex);
            _console.Cursor.Print(_bootMessages[_bootMessageIndex]);
            _bootMessageIndex++;
        }
        else
        {
            _bootTimer.Stop();
            SadConsole.Game.Instance.Screen = new MainScreen();
            SadConsole.Game.Instance.DestroyDefaultStartingConsole();
        }
    }
} 