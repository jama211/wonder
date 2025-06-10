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
        private enum ScreenState { Normal, ConfirmingQuit }
        
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

        private ScreenState _currentState = ScreenState.Normal;
        private int _selectedQuitOption = 0; // 0 for Confirm, 1 for Cancel

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

        public void Update(GameTime gameTime)
        {
            var keyboardState = Keyboard.GetState();
            
            // DIAGNOSTIC: Just print a message on escape instead of changing state.
            if (keyboardState.IsKeyDown(Keys.Escape) && !_previousKeyboardState.IsKeyDown(Keys.Escape))
            {
                System.Console.WriteLine("DEBUG: Escape key press detected and handled safely.");
            }
            else
            {
                // This is all the original "Normal" state logic.
                if (_currentState == ScreenState.ConfirmingQuit)
                {
                    HandleQuitConfirmationInput(keyboardState);
                }
                else // Normal state
                {
                    HandleSpecialKeys();
                    _cursorTimer += gameTime.ElapsedGameTime.TotalSeconds;
                    if (_cursorTimer > 0.5)
                    {
                        _cursorTimer = 0;
                        _cursorVisible = !_cursorVisible;
                    }
                }
            }
            
            _previousKeyboardState = keyboardState;
        }

        public void Draw(GameTime gameTime)
        {
            // Draw the main screen content first
            DrawMainContent();

            // Overlay the quit confirmation dialog if needed
            if (_currentState == ScreenState.ConfirmingQuit)
            {
                DrawQuitConfirmationDialog();
            }
        }

        public void OnTextInput(char character)
        {
            if (_currentState == ScreenState.Normal) _currentInput.Append(character);
        }

        public void OnBackspace()
        {
            if (_currentState == ScreenState.Normal && _currentInput.Length > 0)
            {
                _currentInput.Remove(_currentInput.Length - 1, 1);
            }
        }

        public void OnEnter()
        {
            if (_currentState == ScreenState.Normal)
            {
                var command = _currentInput.ToString();
                _history.Add($"> {command}");
                ProcessCommand(command);
                _currentInput.Clear();
            }
        }

        private void DrawMainContent()
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

            if (_cursorVisible && _currentState == ScreenState.Normal)
            {
                var cursorX = _font.MeasureString(inputPrompt).X;
                Global.SpriteBatch?.DrawString(_font, "_", new Vector2(10 + cursorX, yPos), _themeForeground);
            }
        }

        private void DrawQuitConfirmationDialog()
        {
            // System.Console.WriteLine("DEBUG: Attempting to draw quit dialog."); // NEW DEBUG MESSAGE
            var viewport = _graphicsDevice.Viewport;
            var dialogWidth = 400;
            var dialogHeight = 120;
            var dialogX = (viewport.Width - dialogWidth) / 2;
            var dialogY = (viewport.Height - dialogHeight) / 2;
            var dialogRect = new Rectangle(dialogX, dialogY, dialogWidth, dialogHeight);
            
            // Draw dialog box background and border
            Global.SpriteBatch?.Draw(_graphicsDevice.GetWhitePixel(), dialogRect, _themeBackground);
            // Simple border
            var borderRect = new Rectangle(dialogRect.X, dialogRect.Y, dialogRect.Width, 2);
            Global.SpriteBatch?.Draw(_graphicsDevice.GetWhitePixel(), borderRect, _themeForeground);
            borderRect.Y = dialogRect.Bottom - 2;
            Global.SpriteBatch?.Draw(_graphicsDevice.GetWhitePixel(), borderRect, _themeForeground);
            borderRect = new Rectangle(dialogRect.X, dialogRect.Y, 2, dialogRect.Height);
            Global.SpriteBatch?.Draw(_graphicsDevice.GetWhitePixel(), borderRect, _themeForeground);
            borderRect.X = dialogRect.Right - 2;
            Global.SpriteBatch?.Draw(_graphicsDevice.GetWhitePixel(), borderRect, _themeForeground);

            // Draw text
            var question = "Are you sure you want to quit?";
            var questionSize = _font.MeasureString(question);
            var questionPos = new Vector2(dialogX + (dialogWidth - questionSize.X) / 2, dialogY + 20);
            Global.SpriteBatch?.DrawString(_font, question, questionPos, _themeForeground);

            // Draw buttons
            string confirmText = "[ Confirm ]";
            string cancelText = "[ Cancel ]";
            var confirmColor = _selectedQuitOption == 0 ? Color.LawnGreen : _themeForeground;
            var cancelColor = _selectedQuitOption == 1 ? Color.LawnGreen : _themeForeground;

            var confirmPos = new Vector2(dialogX + 50, dialogY + 70);
            var cancelPos = new Vector2(dialogX + dialogWidth - 150, dialogY + 70);

            Global.SpriteBatch?.DrawString(_font, confirmText, confirmPos, confirmColor);
            Global.SpriteBatch?.DrawString(_font, cancelText, cancelPos, cancelColor);
        }

        private void HandleQuitConfirmationInput(KeyboardState keyboardState)
        {
            // System.Console.WriteLine("DEBUG: Handling quit confirmation input."); // DEBUG MESSAGE
            if (keyboardState.IsKeyDown(Keys.Right) && !_previousKeyboardState.IsKeyDown(Keys.Right))
            {
                _selectedQuitOption = 1; // Move to Cancel
            }
            else if (keyboardState.IsKeyDown(Keys.Left) && !_previousKeyboardState.IsKeyDown(Keys.Left))
            {
                _selectedQuitOption = 0; // Move to Confirm
            }
            else if (keyboardState.IsKeyDown(Keys.Enter) && !_previousKeyboardState.IsKeyDown(Keys.Enter))
            {
                if (_selectedQuitOption == 0) // Confirm
                {
                    System.Environment.Exit(0);
                }
                else // Cancel
                {
                    _currentState = ScreenState.Normal;
                }
            }
             else if (keyboardState.IsKeyDown(Keys.Escape) && !_previousKeyboardState.IsKeyDown(Keys.Escape))
            {
                _currentState = ScreenState.Normal; // Also cancel on escape
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
                    // Instead of exiting directly, open the confirmation dialog.
                    _currentState = ScreenState.ConfirmingQuit;
                    _selectedQuitOption = 0; // Default to Confirm
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