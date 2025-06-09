using SadConsole;
using SadConsole.UI;
using SadConsole.UI.Controls;
using WonderGame.Core;
using SadRogue.Primitives;

namespace WonderGame.Screens;

internal class MainScreen : ScreenObject
{
    private readonly ControlsConsole _controlsConsole;

    public MainScreen()
    {
        // Create a console for the controls
        _controlsConsole = new ControlsConsole(GameSettings.GAME_WIDTH, GameSettings.GAME_HEIGHT);
        _controlsConsole.Surface.DefaultForeground = GameSettings.THEME_FOREGROUND;
        _controlsConsole.Surface.DefaultBackground = GameSettings.THEME_BACKGROUND;
        _controlsConsole.Clear();
        
        // Print the welcome message
        _controlsConsole.Print(0, 0, "Welcome to WonderGame. Type 'help' for a list of commands.");
        
        // Create the input box
        var inputBox = new TextBox(GameSettings.GAME_WIDTH - 2)
        {
            Position = new Point(1, GameSettings.GAME_HEIGHT - 2),
        };
        
        inputBox.KeyPressed += (sender, args) =>
        {
            if (sender is TextBox textBox && args.Key.Key == SadConsole.Input.Keys.Enter)
            {
                string input = textBox.Text;

                // Process input here
                _controlsConsole.Print(0, 10, $"> {input}");

                textBox.Text = "";
            }
        };
        
        _controlsConsole.Controls.Add(inputBox);
        _controlsConsole.IsFocused = true;
        inputBox.IsFocused = true;

        Children.Add(_controlsConsole);
    }
} 