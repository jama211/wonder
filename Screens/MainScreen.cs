using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
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

        // Interaction tracking for "look harder" unlock
        private readonly HashSet<string> _interactedObjects = new();
        private bool _lookHarderUnlocked = false;
        private bool _hasSeenPostItNote = false;
        
        // Text rendering and scrolling
        private float _scrollOffset = 0f;
        private readonly List<string> _wrappedLines = new();
        private bool _userHasScrolled = false;
        
        // Command history
        private readonly List<string> _commandHistory = new();
        private int _commandHistoryIndex = -1;
        
        // Typewriter effect for output
        private readonly Queue<string> _pendingLines = new();
        private string _currentTypingLine = "";
        private int _currentCharIndex = 0;
        private double _typewriterTimer = 0;
        private const double _typewriterSpeed = 0.02; // 20ms per character = very fast but visible

        public MainScreen(GraphicsDevice graphicsDevice, SpriteFont font, Color themeBackground, Color themeForeground)
        {
            _graphicsDevice = graphicsDevice;
            _font = font;
            _themeForeground = themeForeground;
            _themeBackground = themeBackground;

            // SIMSYS boot lines displayed after the main boot sequence - queue for typewriter effect
            QueueOutput("BOOTING SIMSYS v1.7.44b");
            QueueOutput("Initialising subroutine: CORE MODULES... [OK]");
            QueueOutput("Engaging neural shell... [OK]");
            QueueOutput("Welcome, Operator.");
            QueueOutput("");
            
            // New intro text after boot sequence
            QueueOutput("> You awaken to the sterile hum of machinery. There's a slight pressure behind your eyes, like a memory you can't quite access.");
            QueueOutput("> A terminal cursor blinks expectantly.");
            QueueOutput("> Perhaps you should 'look' around to get your bearings.");
            QueueOutput("> (The neural interface helpfully suggests that 'help' might reveal additional operator commands.)");
            
            _previousKeyboardState = Keyboard.GetState();
            _previousMouseState = Mouse.GetState();
        }

        public IScreen? GetNextScreen()
        {
            return _nextScreen;
        }

        public void AddLogEntry(string logEntry)
        {
            QueueOutput(logEntry);
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
                    _currentState = ScreenState.ConfirmingQuit;
                    _selectedQuitOption = 0; // Default to Confirm
                }
                else
                {
                    HandleSpecialKeys();
                    HandleScrolling(keyboardState, mouseState);
                    UpdateTypewriter(gameTime);
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
                ProcessCommand(command);
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
            
            // Rebuild wrapped lines when needed
            RebuildWrappedLines(availableWidth);
            
            var visibleLineCount = (int)((viewport.Height - 60) / lineHeight); // Leave space for input
            var totalLines = _wrappedLines.Count;
            
            // Auto-scroll to bottom if we have more lines than can fit AND user hasn't manually scrolled
            if (totalLines > visibleLineCount && !_userHasScrolled)
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
            
            // Add the currently typing line if there is one
            if (!string.IsNullOrEmpty(_currentTypingLine) && _currentCharIndex > 0)
            {
                var partialText = _currentTypingLine.Substring(0, _currentCharIndex);
                var wrappedLines = WrapText(partialText, availableWidth);
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
        
        private void HandleScrolling(KeyboardState keyboardState, MouseState mouseState)
        {
            var scrollSpeed = 3f;
            var viewport = _graphicsDevice.Viewport;
            var lineHeight = _font.LineSpacing;
            var visibleLineCount = (int)((viewport.Height - 60) / lineHeight);
            var maxScroll = Math.Max(0, _wrappedLines.Count - visibleLineCount);
            
            bool scrolled = false;
            
            // Keyboard scrolling
            if (keyboardState.IsKeyDown(Keys.PageUp) && !_previousKeyboardState.IsKeyDown(Keys.PageUp))
            {
                _scrollOffset = Math.Max(0, _scrollOffset - scrollSpeed);
                scrolled = true;
            }
            else if (keyboardState.IsKeyDown(Keys.PageDown) && !_previousKeyboardState.IsKeyDown(Keys.PageDown))
            {
                _scrollOffset = Math.Min(maxScroll, _scrollOffset + scrollSpeed);
                scrolled = true;
            }
            else if (keyboardState.IsKeyDown(Keys.Home) && !_previousKeyboardState.IsKeyDown(Keys.Home))
            {
                _scrollOffset = 0;
                scrolled = true;
            }
            else if (keyboardState.IsKeyDown(Keys.End) && !_previousKeyboardState.IsKeyDown(Keys.End))
            {
                _scrollOffset = Math.Max(0, _wrappedLines.Count - visibleLineCount);
                scrolled = true;
            }
            
            // Mouse wheel scrolling
            var scrollWheelDelta = mouseState.ScrollWheelValue - _previousMouseState.ScrollWheelValue;
            if (scrollWheelDelta != 0)
            {
                var mouseScrollSpeed = 2f;
                if (scrollWheelDelta > 0) // Scroll up
                {
                    _scrollOffset = Math.Max(0, _scrollOffset - mouseScrollSpeed);
                }
                else // Scroll down
                {
                    _scrollOffset = Math.Min(maxScroll, _scrollOffset + mouseScrollSpeed);
                }
                scrolled = true;
            }
            
            if (scrolled)
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

        private void CheckInteractionUnlock()
        {
            if (_hasSeenPostItNote)
            {
                _lookHarderUnlocked = true;
            }
        }

        private void ProcessCommand(string input)
        {
            var trimmedInput = input.Trim();
            if (string.IsNullOrWhiteSpace(trimmedInput)) return;

            var parts = trimmedInput.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var verb = parts[0];
            var args = parts.Skip(1).ToArray();

            switch (verb)
            {
                case "help":
                    HandleHelp();
                    break;
                    
                case "look":
                    HandleLook(args);
                    break;
                    
                case "examine":
                    HandleExamine(args);
                    break;
                    
                case "touch":
                    HandleTouch(args);
                    break;

                case "inventory":
                    HandleInventory();
                    break;
                    
                case "exit":
                case "quit":
                    HandleExit();
                    break;
                    
                case "clear":
                    HandleClear();
                    break;
                    
                // Future dragon insult commands (placeholder implementations)
                case "insult":
                    HandleInsult(args);
                    break;
                    
                case "tell":
                    HandleTell(args);
                    break;
                    
                case "call":
                    HandleCall(args);
                    break;
                    
                case "say":
                    HandleSay(args);
                    break;
                    
                default:
                    QueueOutput($"> Unknown command: '{verb}'{(args.Length > 0 ? $" {string.Join(" ", args)}" : "")}");
                    break;
            }
        }

        private void HandleHelp()
        {
            QueueOutput("> Available commands:");
            QueueOutput(">   help  - Shows this help message.");
            QueueOutput(">   clear - Clears the screen.");
            QueueOutput(">   exit  - Exits the game.");
            QueueOutput(">   look  - Examines your surroundings.");
            QueueOutput(">   examine [object] - Examines a specific object.");
            QueueOutput(">   touch [object] - Touches a specific object.");
            QueueOutput(">   inventory - Shows your inventory.");
            QueueOutput("");
            QueueOutput("> Navigation controls:");
            QueueOutput(">   Up/Down arrows - Navigate command history");
            QueueOutput(">   PageUp/PageDown - Scroll through text history");
            QueueOutput(">   Home/End - Jump to top/bottom of history");
            QueueOutput(">   Mouse wheel - Scroll through text history");
        }

        private void HandleLook(string[] args)
        {
            if (args.Length == 0)
            {
                // Look around the room
                QueueOutput("> You are in a small square room. The walls are featureless metal, tinted green by");
                QueueOutput("> flickering fluorescent strips above. There is a terminal in front of you, a bunk");
                QueueOutput("> bolted to the wall, and an old sign, partially scratched off.");
            }
            else if (args.Length == 1 && args[0] == "harder")
            {
                // "look harder" command
                if (!_lookHarderUnlocked)
                {
                    QueueOutput("> You try to focus, but the command feels unfamiliar. Maybe you need more context first?");
                }
                else
                {
                    QueueOutput("> You focus harder. The humming intensifies. The walls begin to shimmer...");
                    QueueOutput("> [TRANSITION TO DEEP SYSTEM MODE INITIATED]");
                    _nextScreen = new IsometricScreen(_graphicsDevice, _font, _themeBackground, _themeForeground, "room_1", _previousKeyboardState);
                }
            }
            else
            {
                // Look at specific object - treat same as examine
                var target = string.Join(" ", args);
                ExamineObject(target);
            }
        }

        private void HandleExamine(string[] args)
        {
            if (args.Length == 0)
            {
                QueueOutput("> What would you like to examine?");
                return;
            }

            var target = string.Join(" ", args);
            ExamineObject(target);
        }

        private void HandleTouch(string[] args)
        {
            if (args.Length == 0)
            {
                QueueOutput("> What would you like to touch?");
                return;
            }

            var target = string.Join(" ", args);
            TouchObject(target);
        }

        private void ExamineObject(string target)
        {
            switch (target)
            {
                case "sign":
                    _interactedObjects.Add("sign");
                    CheckInteractionUnlock();
                    QueueOutput("> The sign reads: \"____ YOUR POSTS. THE DRAGON IS ALWAYS LISTENING.\"");
                    break;
                    
                case "terminal":
                    _interactedObjects.Add("terminal");
                    QueueOutput("> It displays a looping warning:");
                    QueueOutput("> \"EMOTIONAL SUPPRESSION FIELD ACTIVE. HOSTILE ENTITY DETECTED IN SECTOR C.\"");
                    QueueOutput("> There's a small yellow post-it note stuck to the bottom corner of the screen.");
                    break;
                    
                case "bunk":
                    _interactedObjects.Add("bunk");
                    CheckInteractionUnlock();
                    QueueOutput("> Cold. Uninviting. There's a sticker on the underside: \"You are not the first, nor");
                    QueueOutput("> the last. Tell the Dragon it smells.\"");
                    break;
                    
                case "walls":
                    _interactedObjects.Add("walls");
                    CheckInteractionUnlock();
                    QueueOutput("> Tiny scratches criss-cross the metal. Messages carved into it:");
                    QueueOutput("> - \"i told him he looked like a crouton. he ran.\"");
                    QueueOutput("> - \"insults = escape?\"");
                    break;

                case "post-it":
                case "postit":
                case "postit note":
                case "note":
                case "post-it note":
                    if (_interactedObjects.Contains("terminal"))
                    {
                        _hasSeenPostItNote = true;
                        CheckInteractionUnlock();
                        QueueOutput("> The post-it note reads in hasty handwriting:");
                        QueueOutput("> \"When the warnings get too much, try to 'look harder' at reality.\"");
                        QueueOutput("> \"Trust me, there's more than meets the eye. -J\"");
                    }
                    else
                    {
                        QueueOutput("> You don't see any post-it note here.");
                    }
                    break;

                default:
                    QueueOutput($"> You don't see any '{target}' here.");
                    break;
            }
        }

        private void TouchObject(string target)
        {
            switch (target)
            {
                case "bunk":
                    _interactedObjects.Add("bunk");
                    CheckInteractionUnlock();
                    QueueOutput("> Cold. Uninviting. There's a sticker on the underside: \"You are not the first, nor");
                    QueueOutput("> the last. Tell the Dragon it smells.\"");
                    break;
                    
                case "walls":
                    _interactedObjects.Add("walls");
                    CheckInteractionUnlock();
                    QueueOutput("> Tiny scratches criss-cross the metal. Messages carved into it:");
                    QueueOutput("> - \"i told him he looked like a crouton. he ran.\"");
                    QueueOutput("> - \"insults = escape?\"");
                    break;

                case "sign":
                    QueueOutput("> The sign feels cold and metallic to the touch.");
                    break;

                case "terminal":
                    QueueOutput("> The terminal screen is slightly warm from the display.");
                    break;

                default:
                    QueueOutput($"> You can't touch the '{target}'.");
                    break;
            }
        }

        private void HandleInventory()
        {
            QueueOutput("> [EMPTY]");
        }

        private void HandleExit()
        {
            _currentState = ScreenState.ConfirmingQuit;
            _selectedQuitOption = 0; // Default to Confirm
        }

        private void HandleClear()
        {
            _history.Clear();
            // Re-add the SIMSYS boot lines and intro text after clearing - but queue them for typewriter effect
            QueueOutput("BOOTING SIMSYS v1.7.44b");
            QueueOutput("Initialising subroutine: CORE MODULES... [OK]");
            QueueOutput("Engaging neural shell... [OK]");
            QueueOutput("Welcome, Operator.");
            QueueOutput("");
            QueueOutput("> You awaken to the sterile hum of machinery. There's a slight pressure behind your eyes, like a memory you can't quite access.");
            QueueOutput("> A terminal cursor blinks expectantly.");
            QueueOutput("> Perhaps you should 'look' around to get your bearings.");
            QueueOutput("> (The neural interface helpfully suggests that 'help' might reveal additional operator commands.)");
        }

        private void HandleInsult(string[] args)
        {
            // Future implementation for: "insult dragon with burnt ram smell"
            if (args.Length == 0)
            {
                QueueOutput("> What would you like to insult?");
                return;
            }
            
            var target = args[0];
            var insultsArgs = args.Skip(1).ToArray();
            
            // Placeholder - will be implemented when dragon room is added
            QueueOutput($"> You attempt to insult the {target}, but it's not here.");
        }

        private void HandleTell(string[] args)
        {
            // Future implementation for: "tell dragon it looks like a crouton"
            if (args.Length < 2)
            {
                QueueOutput("> Tell who what?");
                return;
            }
            
            var target = args[0];
            var message = string.Join(" ", args.Skip(1));
            
            // Placeholder - will be implemented when dragon room is added
            QueueOutput($"> You try to tell the {target} something, but it's not here.");
        }

        private void HandleCall(string[] args)
        {
            // Future implementation for: "call dragon a poo poo bum head"
            if (args.Length < 3 || args[1] != "a")
            {
                QueueOutput("> Usage: call [target] a [insult]");
                return;
            }
            
            var target = args[0];
            var insult = string.Join(" ", args.Skip(2));
            
            // Placeholder - will be implemented when dragon room is added  
            QueueOutput($"> You try to call the {target} something, but it's not here.");
        }

        private void HandleSay(string[] args)
        {
            // Future implementation for: "say to dragon you smell terrible"
            if (args.Length < 3 || args[0] != "to")
            {
                QueueOutput("> Usage: say to [target] [message]");
                return;
            }
            
            var target = args[1];
            var message = string.Join(" ", args.Skip(2));
            
            // Placeholder - will be implemented when dragon room is added
            QueueOutput($"> You try to speak to the {target}, but it's not here.");
        }
        
        private void QueueOutput(string text)
        {
            _pendingLines.Enqueue(text);
        }
        
        private void UpdateTypewriter(GameTime gameTime)
        {
            if (string.IsNullOrEmpty(_currentTypingLine) && _pendingLines.Count > 0)
            {
                // Start typing the next line
                _currentTypingLine = _pendingLines.Dequeue();
                _currentCharIndex = 0;
                _typewriterTimer = 0;
            }
            
            if (!string.IsNullOrEmpty(_currentTypingLine))
            {
                _typewriterTimer += gameTime.ElapsedGameTime.TotalSeconds;
                
                if (_typewriterTimer >= _typewriterSpeed)
                {
                    _typewriterTimer = 0;
                    _currentCharIndex++;
                    
                    if (_currentCharIndex >= _currentTypingLine.Length)
                    {
                        // Finished typing this line
                        _history.Add(_currentTypingLine);
                        _currentTypingLine = "";
                        _currentCharIndex = 0;
                    }
                }
            }
        }
        

    }
} 