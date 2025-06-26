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
        private bool _hasSeenPostItNote = false;
        
        // Text rendering and scrolling
        private float _scrollOffset = 0f;
        private readonly List<string> _wrappedLines = new();

        public MainScreen(GraphicsDevice graphicsDevice, SpriteFont font, Color themeBackground, Color themeForeground)
        {
            _graphicsDevice = graphicsDevice;
            _font = font;
            _themeForeground = themeForeground;
            _themeBackground = themeBackground;

            // SIMSYS boot lines displayed after the main boot sequence
            _history.Add("BOOTING SIMSYS v1.7.44b");
            _history.Add("Initialising subroutine: CORE MODULES... [OK]");
            _history.Add("Engaging neural shell... [OK]");
            _history.Add("Welcome, Operator.");
            _history.Add("");
            
            // New intro text after boot sequence
            _history.Add("> You awaken to the sterile hum of machinery. There's a slight pressure behind your eyes, like a memory you can't quite access.");
            _history.Add("> A terminal cursor blinks expectantly.");
            _history.Add("> Perhaps you should 'look' around to get your bearings.");
            _history.Add("> (The neural interface helpfully suggests that 'help' might reveal additional operator commands.)");
            
            _previousKeyboardState = Keyboard.GetState();
        }

        public IScreen? GetNextScreen()
        {
            return _nextScreen;
        }

        public void AddLogEntry(string logEntry)
        {
            _history.Add(logEntry);
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
                    HandleScrolling(keyboardState);
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
                _history.Add(""); // Add spacing between input and response
                ProcessCommand(command);
                _currentInput.Clear();
            }
        }

        private void DrawMainContent()
        {
            var viewport = _graphicsDevice.Viewport;
            var margin = 10f;
            var availableWidth = viewport.Width - (margin * 2);
            var lineHeight = _font.LineSpacing;
            
            // Rebuild wrapped lines when needed
            RebuildWrappedLines(availableWidth);
            
            var visibleLineCount = (int)((viewport.Height - 60) / lineHeight); // Leave space for input
            var totalLines = _wrappedLines.Count;
            
            // Auto-scroll to bottom if we have more lines than can fit
            if (totalLines > visibleLineCount)
            {
                _scrollOffset = totalLines - visibleLineCount;
            }
            
            // Draw visible lines
            var startLine = Math.Max(0, (int)_scrollOffset);
            var endLine = Math.Min(_wrappedLines.Count, startLine + visibleLineCount);
            
            float yPos = margin;
            for (int i = startLine; i < endLine; i++)
            {
                if (i < _wrappedLines.Count)
                {
                    Global.SpriteBatch?.DrawString(_font, _wrappedLines[i], new Vector2(margin, yPos), _themeForeground);
                }
                yPos += lineHeight;
            }
            
            // Draw input prompt at bottom
            var inputY = viewport.Height - 40;
            
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
            var inputLines = WrapText(inputPrompt, availableWidth);
            
            // Draw input lines
            var inputStartY = inputY;
            foreach (var inputLine in inputLines)
            {
                Global.SpriteBatch?.DrawString(_font, inputLine, new Vector2(margin, inputStartY), _themeForeground);
                inputStartY += lineHeight;
            }

            // Draw cursor
            if (_cursorVisible && _currentState == ScreenState.Normal)
            {
                var lastInputLine = inputLines.LastOrDefault() ?? "";
                var cursorX = margin + _font.MeasureString(lastInputLine).X;
                var cursorY = inputStartY - lineHeight;
                Global.SpriteBatch?.DrawString(_font, "_", new Vector2(cursorX, cursorY), _themeForeground);
            }
        }
        
        private void RebuildWrappedLines(float availableWidth)
        {
            _wrappedLines.Clear();
            foreach (var line in _history)
            {
                var wrappedLines = WrapText(line, availableWidth);
                _wrappedLines.AddRange(wrappedLines);
            }
        }
        
        private List<string> WrapText(string text, float maxWidth)
        {
            var lines = new List<string>();
            if (string.IsNullOrEmpty(text))
            {
                lines.Add("");
                return lines;
            }
            
            var words = text.Split(' ');
            var currentLine = new StringBuilder();
            
            foreach (var word in words)
            {
                var testLine = currentLine.Length == 0 ? word : $"{currentLine} {word}";
                var testSize = _font.MeasureString(testLine);
                
                if (testSize.X <= maxWidth)
                {
                    if (currentLine.Length > 0)
                        currentLine.Append(" ");
                    currentLine.Append(word);
                }
                else
                {
                    if (currentLine.Length > 0)
                    {
                        lines.Add(currentLine.ToString());
                        currentLine.Clear();
                    }
                    
                    // Handle very long words that don't fit on a single line
                    if (_font.MeasureString(word).X > maxWidth)
                    {
                        // Break the word into chunks
                        var chars = word.ToCharArray();
                        var chunk = new StringBuilder();
                        
                        foreach (var ch in chars)
                        {
                            var testChunk = $"{chunk}{ch}";
                            if (_font.MeasureString(testChunk).X <= maxWidth)
                            {
                                chunk.Append(ch);
                            }
                            else
                            {
                                if (chunk.Length > 0)
                                {
                                    lines.Add(chunk.ToString());
                                    chunk.Clear();
                                }
                                chunk.Append(ch);
                            }
                        }
                        
                        if (chunk.Length > 0)
                        {
                            currentLine.Append(chunk.ToString());
                        }
                    }
                    else
                    {
                        currentLine.Append(word);
                    }
                }
            }
            
            if (currentLine.Length > 0)
            {
                lines.Add(currentLine.ToString());
            }
            
            return lines.Count > 0 ? lines : new List<string> { "" };
        }
        
        private void HandleScrolling(KeyboardState keyboardState)
        {
            var scrollSpeed = 3f;
            
            if (keyboardState.IsKeyDown(Keys.PageUp) && !_previousKeyboardState.IsKeyDown(Keys.PageUp))
            {
                _scrollOffset = Math.Max(0, _scrollOffset - scrollSpeed);
            }
            else if (keyboardState.IsKeyDown(Keys.PageDown) && !_previousKeyboardState.IsKeyDown(Keys.PageDown))
            {
                var viewport = _graphicsDevice.Viewport;
                var lineHeight = _font.LineSpacing;
                var visibleLineCount = (int)((viewport.Height - 60) / lineHeight);
                var maxScroll = Math.Max(0, _wrappedLines.Count - visibleLineCount);
                _scrollOffset = Math.Min(maxScroll, _scrollOffset + scrollSpeed);
            }
            else if (keyboardState.IsKeyDown(Keys.Home) && !_previousKeyboardState.IsKeyDown(Keys.Home))
            {
                _scrollOffset = 0;
            }
            else if (keyboardState.IsKeyDown(Keys.End) && !_previousKeyboardState.IsKeyDown(Keys.End))
            {
                var viewport = _graphicsDevice.Viewport;
                var lineHeight = _font.LineSpacing;
                var visibleLineCount = (int)((viewport.Height - 60) / lineHeight);
                _scrollOffset = Math.Max(0, _wrappedLines.Count - visibleLineCount);
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
            if (_hasSeenPostItNote)
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
                    _history.Add("");
                    _history.Add("> Scroll controls:");
                    _history.Add(">   PageUp/PageDown - Scroll through text history");
                    _history.Add(">   Home/End - Jump to top/bottom of history");
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
                    _history.Add("> It displays a looping warning:");
                    _history.Add("> \"EMOTIONAL SUPPRESSION FIELD ACTIVE. HOSTILE ENTITY DETECTED IN SECTOR C.\"");
                    _history.Add("> There's a small yellow post-it note stuck to the bottom corner of the screen.");
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

                case "examine post-it":
                case "examine postit":
                case "examine postit note":
                case "examine note":
                case "examine post-it note":
                    if (_interactedObjects.Contains("terminal"))
                    {
                        _hasSeenPostItNote = true;
                        CheckInteractionUnlock();
                        _history.Add("> The post-it note reads in hasty handwriting:");
                        _history.Add("> \"When the warnings get too much, try to 'look harder' at reality.\"");
                        _history.Add("> \"Trust me, there's more than meets the eye. -J\"");
                    }
                    else
                    {
                        _history.Add("> You don't see any post-it note here.");
                    }
                    break;

                case "inventory":
                    _history.Add("> [EMPTY]");
                    break;
                    
                case "look harder":
                    if (!_lookHarderUnlocked)
                    {
                        _history.Add("> You try to focus, but the command feels unfamiliar. Maybe you need more context first?");
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
                    // Re-add the SIMSYS boot lines and intro text after clearing
                    _history.Add("BOOTING SIMSYS v1.7.44b");
                    _history.Add("Initialising subroutine: CORE MODULES... [OK]");
                    _history.Add("Engaging neural shell... [OK]");
                    _history.Add("Welcome, Operator.");
                    _history.Add("");
                    _history.Add("> You awaken to the sterile hum of machinery. There's a slight pressure behind your eyes, like a memory you can't quite access.");
                    _history.Add("> A terminal cursor blinks expectantly.");
                    _history.Add("> Perhaps you should 'look' around to get your bearings.");
                    _history.Add("> (The neural interface helpfully suggests that 'help' might reveal additional operator commands.)");
                    break;
                    
                default:
                    _history.Add($"> Unknown command: '{command}'");
                    break;
            }
        }
    }
} 