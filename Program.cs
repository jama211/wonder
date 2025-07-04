using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using WonderGame.Screens;
using WonderGame.Core;
using WonderGame.Tests;

namespace WonderGame;

public class Program : Game
{
    private GraphicsDeviceManager _graphics;
    private SpriteBatch? _spriteBatch;
    private SpriteFont? _font;
    private IScreen? _currentScreen;

    // Define the theme colors
    private static readonly Color THEME_BACKGROUND = new Color(20, 20, 20); // Dark gray
    private static readonly Color THEME_FOREGROUND = new Color(0, 255, 128); // Bright green

    public Program()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        
        // Make window resizable
        Window.AllowUserResizing = true;
    }

    protected override void Initialize()
    {
        base.Initialize();
        Window.TextInput += HandleTextInput;
        Global.Initialize(GraphicsDevice);
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        Global.SpriteBatch = _spriteBatch;

        _font = Content.Load<SpriteFont>("DefaultFont");

        _currentScreen = new BootScreen(GraphicsDevice, _font, THEME_FOREGROUND, 
            () => new MainScreen(GraphicsDevice, _font, THEME_BACKGROUND, THEME_FOREGROUND));
    }

    private void HandleTextInput(object? sender, TextInputEventArgs e)
    {
        if (_currentScreen is ITextInputReceiver inputReceiver)
        {
            switch (e.Character)
            {
                case '\b': // Backspace
                    inputReceiver.OnBackspace();
                    break;
                case '\r': // Enter
                    inputReceiver.OnEnter();
                    break;
                default:
                    inputReceiver.OnTextInput(e.Character);
                    break;
            }
        }
    }

    protected override void Update(GameTime gameTime)
    {
        // if ((Keyboard.GetState().IsKeyDown(Keys.Escape) || GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed)
        //     Exit();

        _currentScreen?.Update(gameTime);

        var nextScreen = _currentScreen?.GetNextScreen();
        if (nextScreen != null)
        {
            _currentScreen = nextScreen;
        }

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(THEME_BACKGROUND);

        if (_spriteBatch != null)
        {
            _spriteBatch.Begin();
            _currentScreen?.Draw(gameTime);
            _spriteBatch.End();
        }

        base.Draw(gameTime);
    }
}

public static class Launcher
{
    [System.STAThread]
    static void Main(string[] args)
    {
        // Check if we're in test mode
        if (args.Length > 0 && args[0] == "run-tests")
        {
            var testRunner = new CommandTestRunner();
            testRunner.RunAllTests();
            return;
        }

        // Normal game mode
        using (var game = new Program())
            game.Run();
    }
} 