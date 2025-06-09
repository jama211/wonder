using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using WonderGame.Screens;
using WonderGame.Core;

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
    }

    protected override void Initialize()
    {
        base.Initialize();
        Window.TextInput += HandleTextInput;
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        Global.SpriteBatch = _spriteBatch;

        _font = Content.Load<SpriteFont>("DefaultFont");

        _currentScreen = new BootScreen(GraphicsDevice, _font, THEME_FOREGROUND);
        // Subscribe to the boot sequence completion event
        ((BootScreen)_currentScreen).OnBootSequenceComplete += HandleBootSequenceComplete;
    }

    private void HandleBootSequenceComplete()
    {
        if (_font == null) return; // Null check for safety
        // Switch to the main screen
        _currentScreen = new MainScreen(GraphicsDevice, _font, THEME_BACKGROUND, THEME_FOREGROUND);
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
        if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
            Exit();

        _currentScreen?.Update(gameTime);

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
    static void Main()
    {
        using (var game = new Program())
            game.Run();
    }
} 