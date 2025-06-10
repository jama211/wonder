using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Collections.Generic;
using System.Text;
using WonderGame.Core;

namespace WonderGame.Screens
{
    // A new interface for screens that can receive text input.
    public interface ITextInputReceiver
    {
        void OnTextInput(char character);
        void OnBackspace();
        void OnEnter();
    }

    public class MainScreen : IScreen, ITextInputReceiver
    {
        private readonly SpriteFont _font;
        private readonly Color _themeForeground;
        private readonly List<string> _history = new();
        private readonly StringBuilder _currentInput = new();
        private KeyboardState _previousKeyboardState;

        private double _cursorTimer;
        private bool _cursorVisible;
        
        private IScreen? _nextScreen;
        private readonly GraphicsDevice _graphicsDevice;
        private readonly Color _themeBackground;

        public MainScreen(GraphicsDevice graphicsDevice, SpriteFont font, Color themeBackground, Color themeForeground)
        {
            _graphicsDevice = graphicsDevice;
            _font = font;
            _themeForeground = themeForeground;
            _themeBackground = themeBackground;

            _history.Add("Your eyes slowly adjust to the gloom. You find yourself in a room.");
            _history.Add("A single bare bulb hanging from the ceiling casts long, dancing shadows.");
            _history.Add("Would you like to 'look' around?");
            
            _previousKeyboardState = Keyboard.GetState();
        }

        public IScreen? GetNextScreen()
        {
            return _nextScreen;
        }

        public void OnTextInput(char character)
        {
            _currentInput.Append(character);
        }

        public void OnBackspace()
        {
            if (_currentInput.Length > 0)
            {
                _currentInput.Remove(_currentInput.Length - 1, 1);
            }
        }

        public void OnEnter()
        {
            var command = _currentInput.ToString();
            _history.Add($"> {command}");
            ProcessCommand(command);
            _currentInput.Clear();
        }

        public void Update(GameTime gameTime)
        {
            // Handle non-text input like Tab, which isn't captured by the TextInput event
            HandleSpecialKeys();

            _cursorTimer += gameTime.ElapsedGameTime.TotalSeconds;
            if (_cursorTimer > 0.5)
            {
                _cursorTimer = 0;
                _cursorVisible = !_cursorVisible;
            }
        }

        public void Draw(GameTime gameTime)
        {
            float yPos = 10;
            // Use the lineHeight from the font for consistent spacing
            float lineHeight = _font.LineSpacing;

            foreach (var line in _history)
            {
                Global.SpriteBatch?.DrawString(_font, line, new Vector2(10, yPos), _themeForeground);
                yPos += lineHeight;
            }

            var inputPrompt = $"> {_currentInput}";
            Global.SpriteBatch?.DrawString(_font, inputPrompt, new Vector2(10, yPos), _themeForeground);

            if (_cursorVisible)
            {
                var cursorX = _font.MeasureString(inputPrompt).X;
                Global.SpriteBatch?.DrawString(_font, "_", new Vector2(10 + cursorX, yPos), _themeForeground);
            }
        }

        private void HandleSpecialKeys()
        {
            var keyboardState = Keyboard.GetState();
            
            // Tab is not a character, so handle it separately
            if (keyboardState.IsKeyDown(Keys.Tab) && !_previousKeyboardState.IsKeyDown(Keys.Tab))
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
                    _history.Add("  look  - Examines your surroundings.");
                    break;
                case "look":
                    _history.Add("The room is sparse, coated in a fine layer of dust. Against one wall stands");
                    _history.Add("a humming, ancient REFRIGERATOR. A sturdy wooden TABLE sits in the center,");
                    _history.Add("and a heavy iron DOOR is set in the opposite wall. It feels like you");
                    _history.Add("could 'look harder' to get a better sense of the space.");
                    break;
                case "look harder":
                    _history.Add("You focus, concentrating on the space around you...");
                    _nextScreen = new IsometricScreen(_graphicsDevice, _font, _themeBackground, _themeForeground);
                    break;
                case "exit":
                    // A proper event system would be better, but this works for now.
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
} 