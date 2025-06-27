using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Linq;
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
        private MouseState _previousMouseState;

        private double _cursorTimer;
        private bool _cursorVisible;
        
        private IScreen? _nextScreen;
        private readonly GraphicsDevice _graphicsDevice;
        private readonly Color _themeBackground;

        private ScreenState _currentState = ScreenState.Normal;
        private int _selectedQuitOption = 0; // 0 for Confirm, 1 for Cancel

        // Command processor
        private readonly CommandProcessor _commandProcessor;
        
        // Text handling utilities
        private readonly TextHandlingUtils _textUtils;
        private float _scrollOffset = 0f;
        private readonly List<string> _wrappedLines = new();
        private bool _userHasScrolled = false;
        
        // Command history
        private readonly List<string> _commandHistory = new();
        private int _commandHistoryIndex = -1;

        // Flag to ignore the first ESC key press (for transitions from other screens)
        private bool _ignoreNextEscape = false;

        public MainScreen(GraphicsDevice graphicsDevice, SpriteFont font, Color themeBackground, Color themeForeground, bool ignoreFirstEscape = false)
        {
            _graphicsDevice = graphicsDevice;
            _font = font;
            _themeForeground = themeForeground;
            _themeBackground = themeBackground;
            _ignoreNextEscape = ignoreFirstEscape;

            // Initialize text handling utilities
            _textUtils = new TextHandlingUtils(font);
            _textUtils.LineCompleted += OnLineCompleted;

            // Initialize command processor
            _commandProcessor = new CommandProcessor(
                _textUtils.QueueOutput,
                () => new IsometricScreen(_graphicsDevice, _font, _themeBackground, _themeForeground, "room_1", this, _previousKeyboardState)
            );

            // SIMSYS boot lines displayed after the main boot sequence - queue for typewriter effect
            _textUtils.QueueOutput("BOOTING SIMSYS v1.7.44b");
            _textUtils.QueueOutput("Initialising subroutine: CORE MODULES... [OK]");
            _textUtils.QueueOutput("Engaging neural shell... [OK]");
            _textUtils.QueueOutput("Welcome, Operator.");
            _textUtils.QueueOutput("");
            
            // New intro text after boot sequence
            _textUtils.QueueOutput("> You awaken to the sterile hum of machinery. There's a slight pressure behind your eyes, like a memory you can't quite access.");
            _textUtils.QueueOutput("> A terminal cursor blinks expectantly.");
            _textUtils.QueueOutput("> Perhaps you should 'look' around to get your bearings.");
            _textUtils.QueueOutput("> (The neural interface helpfully suggests that 'help' might reveal additional operator commands.)");
            
            _previousKeyboardState = Keyboard.GetState();
            _previousMouseState = Mouse.GetState();
        }

        // Constructor for preserving state when returning from other screens
        public MainScreen(GraphicsDevice graphicsDevice, SpriteFont font, Color themeBackground, Color themeForeground, 
                         List<string> existingHistory, List<string> existingCommandHistory, bool ignoreFirstEscape = false)
        {
            _graphicsDevice = graphicsDevice;
            _font = font;
            _themeForeground = themeForeground;
            _themeBackground = themeBackground;
            _ignoreNextEscape = ignoreFirstEscape;

            // Initialize text handling utilities
            _textUtils = new TextHandlingUtils(font);
            _textUtils.LineCompleted += OnLineCompleted;

            // Initialize command processor
            _commandProcessor = new CommandProcessor(
                _textUtils.QueueOutput,
                () => new IsometricScreen(_graphicsDevice, _font, _themeBackground, _themeForeground, "room_1", this, _previousKeyboardState)
            );

            // Restore existing history
            _history.AddRange(existingHistory);
            _commandHistory.AddRange(existingCommandHistory);
            
            _previousKeyboardState = Keyboard.GetState();
            _previousMouseState = Mouse.GetState();
        }

        public IScreen? GetNextScreen()
        {
            return _nextScreen;
        }

        public void AddLogEntry(string logEntry)
        {
            _textUtils.QueueOutput(logEntry);
        }

        // Methods to access history for state preservation
        public List<string> GetHistory()
        {
            return new List<string>(_history);
        }

        public List<string> GetCommandHistory()
        {
            return new List<string>(_commandHistory);
        }

        private void OnLineCompleted(string line)
        {
            _history.Add(line);
        }

        public void Update(GameTime gameTime)
        {
            var keyboardState = Keyboard.GetState();
            var mouseState = Mouse.GetState();

            if (_currentState == ScreenState.ConfirmingQuit)
            {
                HandleQuitConfirmationInput(keyboardState);
            }
            else // Normal state
            {
                if (keyboardState.IsKeyDown(Keys.Escape) && !_previousKeyboardState.IsKeyDown(Keys.Escape))
                {
                    if (_ignoreNextEscape)
                    {
                        _ignoreNextEscape = false; // Reset the flag
                    }
                    else
                    {
                        _currentState = ScreenState.ConfirmingQuit;
                        _selectedQuitOption = 0; // Default to Confirm
                    }
                }
                else
                {
                    HandleSpecialKeys();
                    HandleScrolling(keyboardState, mouseState);
                    _textUtils.UpdateTypewriter(gameTime);
                    _cursorTimer += gameTime.ElapsedGameTime.TotalSeconds;
                    if (_cursorTimer > 0.5)
                    {
                        _cursorTimer = 0;
                        _cursorVisible = !_cursorVisible;
                    }
                }
            }
            _previousKeyboardState = keyboardState;
            _previousMouseState = mouseState;
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
                
                // Add to command history if not empty and not the same as last command
                if (!string.IsNullOrWhiteSpace(command) && 
                    (_commandHistory.Count == 0 || _commandHistory[_commandHistory.Count - 1] != command))
                {
                    _commandHistory.Add(command);
                }
                _commandHistoryIndex = -1; // Reset history navigation
                
                // User input should appear immediately (no typewriter effect)
                _history.Add(""); // Add spacing above the user prompt
                _history.Add($">> {command}");
                _history.Add(""); // Add spacing between input and response
                
                // System responses use the typewriter effect
                var nextScreen = _commandProcessor.ProcessCommand(command);
                if (nextScreen != null)
                {
                    _nextScreen = nextScreen;
                }
                
                // Handle special commands that need MainScreen-specific logic
                if (command.Trim().ToLowerInvariant() == "clear")
                {
                    _history.Clear();
                    _wrappedLines.Clear();
                    _scrollOffset = 0f;
                    _userHasScrolled = false;
                    _textUtils.ClearTypewriter();
                }
                
                _currentInput.Clear();
                _userHasScrolled = false; // Reset scroll flag so new content auto-scrolls
            }
        }

        private void DrawMainContent()
        {
            var viewport = _graphicsDevice.Viewport;
            var margin = 10f;
            var availableWidth = viewport.Width - (margin * 2);
            var lineHeight = _font.LineSpacing;
            
            // Rebuild wrapped lines using text utils
            var currentPartialLine = _textUtils.GetCurrentPartialLine();
            _wrappedLines.Clear();
            _wrappedLines.AddRange(_textUtils.RebuildWrappedLines(_history, availableWidth, currentPartialLine));
            
            var visibleLineCount = (int)((viewport.Height - 60) / lineHeight); // Leave space for input
            var totalLines = _wrappedLines.Count;
            
            // Auto-scroll to bottom if we have more lines than can fit AND user hasn't manually scrolled
            if (totalLines > visibleLineCount && !_userHasScrolled)
            {
                _scrollOffset = _textUtils.CalculateAutoScrollOffset(totalLines, visibleLineCount);
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
            var inputLines = _textUtils.WrapText(inputPrompt, availableWidth);
            
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
        
        private void HandleScrolling(KeyboardState keyboardState, MouseState mouseState)
        {
            var viewport = _graphicsDevice.Viewport;
            var lineHeight = _font.LineSpacing;
            var visibleLineCount = (int)((viewport.Height - 60) / lineHeight);
            
            var scrollResult = _textUtils.HandleScrolling(
                keyboardState, 
                _previousKeyboardState,
                mouseState, 
                _previousMouseState,
                _scrollOffset,
                _wrappedLines.Count,
                visibleLineCount
            );
            
            _scrollOffset = scrollResult.NewScrollOffset;
            if (scrollResult.UserScrolled)
            {
                _userHasScrolled = true;
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
            
            // Command history navigation with Up/Down arrows
            if (keyboardState.IsKeyDown(Keys.Up) && !_previousKeyboardState.IsKeyDown(Keys.Up))
            {
                if (_commandHistory.Count > 0)
                {
                    if (_commandHistoryIndex == -1)
                    {
                        _commandHistoryIndex = _commandHistory.Count - 1;
                    }
                    else if (_commandHistoryIndex > 0)
                    {
                        _commandHistoryIndex--;
                    }
                    
                    _currentInput.Clear();
                    _currentInput.Append(_commandHistory[_commandHistoryIndex]);
                }
            }
            else if (keyboardState.IsKeyDown(Keys.Down) && !_previousKeyboardState.IsKeyDown(Keys.Down))
            {
                if (_commandHistory.Count > 0 && _commandHistoryIndex != -1)
                {
                    if (_commandHistoryIndex < _commandHistory.Count - 1)
                    {
                        _commandHistoryIndex++;
                        _currentInput.Clear();
                        _currentInput.Append(_commandHistory[_commandHistoryIndex]);
                    }
                    else
                    {
                        _commandHistoryIndex = -1;
                        _currentInput.Clear();
                    }
                }
            }
            
            _previousKeyboardState = keyboardState;
        }
    }
} 