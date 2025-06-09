using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Collections.Generic;
using System.Text;

namespace WonderGame.Screens;

public class MainScreen : IScreen
{
    private readonly SpriteFont _font;
    private readonly List<string> _history = new();
    private readonly StringBuilder _currentInput = new();
    private KeyboardState _previousKeyboardState;

    private double _cursorTimer;
    private bool _cursorVisible;

    public MainScreen(SpriteFont font, GameWindow window)
    {
        _font = font;
        _history.Add("Welcome to WonderGame. Type 'help' for a list of commands.");
        _previousKeyboardState = Keyboard.GetState();
        window.TextInput += TextInputHandler;
    }

    private void TextInputHandler(object? sender, TextInputEventArgs e)
    {
        if (e.Character != '\r' && e.Character != '\b')
        {
            _currentInput.Append(e.Character);
        }
    }
    
    public void Update(GameTime gameTime)
    {
        HandleInput();

        _cursorTimer += gameTime.ElapsedGameTime.TotalSeconds;
        if (_cursorTimer > 0.5)
        {
            _cursorTimer = 0;
            _cursorVisible = !_cursorVisible;
        }
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        float yPos = 10;
        foreach (var line in _history)
        {
            spriteBatch.DrawString(_font, line, new Vector2(10, yPos), Color.Green);
            yPos += 20;
        }

        var inputPrompt = $"> {_currentInput}";
        spriteBatch.DrawString(_font, inputPrompt, new Vector2(10, yPos), Color.Green);

        if (_cursorVisible)
        {
            var cursorPosition = _font.MeasureString(inputPrompt);
            spriteBatch.DrawString(_font, "_", new Vector2(10 + cursorPosition.X, yPos), Color.Green);
        }
    }

    private void HandleInput()
    {
        var keyboardState = Keyboard.GetState();

        if (keyboardState.IsKeyDown(Keys.Enter) && !_previousKeyboardState.IsKeyDown(Keys.Enter))
        {
            _history.Add($"> {_currentInput}");
            ProcessCommand(_currentInput.ToString());
            _currentInput.Clear();
        }
        else if (keyboardState.IsKeyDown(Keys.Back) && !_previousKeyboardState.IsKeyDown(Keys.Back))
        {
            if (_currentInput.Length > 0)
            {
                _currentInput.Remove(_currentInput.Length - 1, 1);
            }
        }
        else if (keyboardState.IsKeyDown(Keys.Tab) && !_previousKeyboardState.IsKeyDown(Keys.Tab))
        {
            _currentInput.Append("    ");
        }

        _previousKeyboardState = keyboardState;
    }
    
    private void ProcessCommand(string input)
    {
        var command = input.ToLowerInvariant().Trim();

        if (string.IsNullOrWhiteSpace(command)) return;

        switch (command)
        {
            case "help":
                _history.Add("Available commands:");
                _history.Add("  help  - Shows this help message.");
                _history.Add("  clear - Clears the screen.");
                _history.Add("  exit  - Exits the game.");
                break;
            case "exit":
                // This is a bit of a hack for now. A proper event system would be better.
                System.Environment.Exit(0);
                break;
            case "clear":
                _history.Clear();
                break;
            default:
                _history.Add($"Unknown command: '{command}'");
                break;
        }
    }
} 