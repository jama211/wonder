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

        // Interaction tracking for "look harder" unlock
        private readonly HashSet<string> _interactedObjects = new();
        private bool _lookHarderUnlocked = false;

        public MainScreen(GraphicsDevice graphicsDevice, SpriteFont font, Color themeBackground, Color themeForeground)
        {
            _graphicsDevice = graphicsDevice;
            _font = font;
            _themeForeground = themeForeground;
            _themeBackground = themeBackground;

            // New intro text after boot sequence
            _history.Add("> You awaken to the sterile hum of machinery. There's a slight pressure behind your eyes, like a memory you can't quite access.");
            _history.Add("> A terminal cursor blinks expectantly.");
            
            _previousKeyboardState = Keyboard.GetState();
        }

        public IScreen? GetNextScreen()
        {
            return _nextScreen;
        }

        public void Update(GameTime gameTime)
        {
            var keyboardState = Keyboard.GetState();

            if (_currentState == ScreenState.ConfirmingQuit)
            {
                HandleQuitConfirmationInput(keyboardState);
            }
            else // Normal state
            {
                if (keyboardState.IsKeyDown(Keys.Escape) && !_previousKeyboardState.IsKeyDown(Keys.Escape))
                {
                    _currentState = ScreenState.ConfirmingQuit;
                    _selectedQuitOption = 0; // Default to Confirm
                }
                else
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
                _history.Add($">> {command}");
                ProcessCommand(command);
                _currentInput.Clear();
            }
        }

        private void DrawMainContent()
        {
            float yPos = 10;
            float lineHeight = _font.LineSpacing;

            foreach (var line in _history)
            {
                Global.SpriteBatch?.DrawString(_font, line, new Vector2(10, yPos), _themeForeground);
                yPos += lineHeight;
            }

            // Filter the input to prevent drawing unsupported characters
            var filteredInput = new StringBuilder();
            foreach(char c in _currentInput.ToString())
            {
                if (_font.Characters.Contains(c))
                {
                    filteredInput.Append(c);
                }
            }

            var inputPrompt = $">> {filteredInput}";
            Global.SpriteBatch?.DrawString(_font, inputPrompt, new Vector2(10, yPos), _themeForeground);

            if (_cursorVisible && _currentState == ScreenState.Normal)
            {
                var cursorX = _font.MeasureString(inputPrompt).X;
                Global.SpriteBatch?.DrawString(_font, "_", new Vector2(10 + cursorX, yPos), _themeForeground);
            }
        }

        private void DrawQuitConfirmationDialog()
        {
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

        private void CheckInteractionUnlock()
        {
            if (_interactedObjects.Count >= 2)
            {
                _lookHarderUnlocked = true;
            }
        }

        private void ProcessCommand(string input)
        {
            var command = input.ToLowerInvariant().Trim();

            if (string.IsNullOrWhiteSpace(command)) return;

            switch (command)
            {
                case "help":
                    _history.Add("> Available commands:");
                    _history.Add(">   help  - Shows this help message.");
                    _history.Add(">   clear - Clears the screen.");
                    _history.Add(">   exit  - Exits the game.");
                    _history.Add(">   look  - Examines your surroundings.");
                    _history.Add(">   examine [object] - Examines a specific object.");
                    _history.Add(">   touch [object] - Touches a specific object.");
                    _history.Add(">   inventory - Shows your inventory.");
                    break;
                    
                case "look":
                    _history.Add("> You are in a small square room. The walls are featureless metal, tinted green by");
                    _history.Add("> flickering fluorescent strips above. There is a terminal in front of you, a bunk");
                    _history.Add("> bolted to the wall, and an old sign, partially scratched off.");
                    break;
                    
                case "examine sign":
                    _interactedObjects.Add("sign");
                    CheckInteractionUnlock();
                    _history.Add("> The sign reads: \"____ YOUR POSTS. THE DRAGON IS ALWAYS LISTENING.\"");
                    break;
                    
                case "examine terminal":
                    _interactedObjects.Add("terminal");
                    CheckInteractionUnlock();
                    _history.Add("> It displays a looping warning:");
                    _history.Add("> \"EMOTIONAL SUPPRESSION FIELD ACTIVE. HOSTILE ENTITY DETECTED IN SECTOR C.\"");
                    break;
                    
                case "touch bunk":
                    _interactedObjects.Add("bunk");
                    CheckInteractionUnlock();
                    _history.Add("> Cold. Uninviting. There's a sticker on the underside: \"You are not the first, nor");
                    _history.Add("> the last. Tell the Dragon it smells.\"");
                    break;
                    
                case "examine bunk":
                    _interactedObjects.Add("bunk");
                    CheckInteractionUnlock();
                    _history.Add("> Cold. Uninviting. There's a sticker on the underside: \"You are not the first, nor");
                    _history.Add("> the last. Tell the Dragon it smells.\"");
                    break;
                    
                case "examine walls":
                    _interactedObjects.Add("walls");
                    CheckInteractionUnlock();
                    _history.Add("> Tiny scratches criss-cross the metal. Messages carved into it:");
                    _history.Add("> - \"i told him he looked like a crouton. he ran.\"");
                    _history.Add("> - \"insults = escape?\"");
                    break;
                    
                case "touch walls":
                    _interactedObjects.Add("walls");
                    CheckInteractionUnlock();
                    _history.Add("> Tiny scratches criss-cross the metal. Messages carved into it:");
                    _history.Add("> - \"i told him he looked like a crouton. he ran.\"");
                    _history.Add("> - \"insults = escape?\"");
                    break;

                case "inventory":
                    _history.Add("> [EMPTY]");
                    break;
                    
                case "look harder":
                    if (!_lookHarderUnlocked)
                    {
                        _history.Add("> You squint into the shadows... but you're not ready yet. Something's missing.");
                    }
                    else
                    {
                        _history.Add("> You focus harder. The humming intensifies. The walls begin to shimmer...");
                        _history.Add("> [TRANSITION TO DEEP SYSTEM MODE INITIATED]");
                        _nextScreen = new IsometricScreen(_graphicsDevice, _font, _themeBackground, _themeForeground, "room_1", _previousKeyboardState);
                    }
                    break;
                    
                case "exit":
                    // Instead of exiting directly, open the confirmation dialog.
                    _currentState = ScreenState.ConfirmingQuit;
                    _selectedQuitOption = 0; // Default to Confirm
                    break;
                    
                case "clear":
                    _history.Clear();
                    // Re-add the intro text after clearing
                    _history.Add("> You awaken to the sterile hum of machinery. There's a slight pressure behind your eyes, like a memory you can't quite access.");
                    _history.Add("> A terminal cursor blinks expectantly.");
                    break;
                    
                default:
                    _history.Add($"> Unknown command: '{command}'");
                    break;
            }
        }
    }
} 