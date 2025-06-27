using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WonderGame.Core
{
    public class TextHandlingUtils
    {
        private readonly SpriteFont _font;
        private readonly Random _typewriterRandom = new();
        private const double _baseTypewriterSpeed = 0.004; // 4ms base speed = 250 chars/sec

        // Typewriter state
        private readonly Queue<string> _pendingLines = new();
        private string _currentTypingLine = "";
        private int _currentCharIndex = 0;
        private double _typewriterTimer = 0;

        // Events for communication with the UI
        public event Action<string>? LineCompleted;
        public event Action? TypingCompleted;

        public TextHandlingUtils(SpriteFont font)
        {
            _font = font;
        }

        #region Typewriter System

        public void QueueOutput(string text)
        {
            _pendingLines.Enqueue(text);
        }

        public bool HasPendingContent => _pendingLines.Count > 0 || !string.IsNullOrEmpty(_currentTypingLine);

        public void UpdateTypewriter(GameTime gameTime)
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

                // Process multiple characters per frame to overcome frame rate limitations
                while (_typewriterTimer > 0 && _currentCharIndex < _currentTypingLine.Length)
                {
                    var currentChar = _currentTypingLine[_currentCharIndex];
                    var charSpeed = GetCharacterSpeed(currentChar);

                    if (_typewriterTimer >= charSpeed)
                    {
                        _typewriterTimer -= charSpeed; // Subtract the time used for this character
                        _currentCharIndex++;

                        if (_currentCharIndex >= _currentTypingLine.Length)
                        {
                            // Finished typing this line
                            LineCompleted?.Invoke(_currentTypingLine);
                            _currentTypingLine = "";
                            _currentCharIndex = 0;
                            _typewriterTimer = 0; // Reset timer for next line

                            // Check if all content is done
                            if (_pendingLines.Count == 0)
                            {
                                TypingCompleted?.Invoke();
                            }
                            break;
                        }
                    }
                    else
                    {
                        // Not enough time accumulated for next character
                        break;
                    }
                }
            }
        }

        public string GetCurrentPartialLine()
        {
            if (string.IsNullOrEmpty(_currentTypingLine) || _currentCharIndex == 0)
                return "";
            
            return _currentTypingLine.Substring(0, _currentCharIndex);
        }

        private double GetCharacterSpeed(char character)
        {
            // Base speed with some random variation (±50%)
            var speed = _baseTypewriterSpeed * (0.5 + _typewriterRandom.NextDouble());

            // Adjust speed based on character type for more natural feel
            switch (character)
            {
                case ' ':
                    // Spaces are slightly faster (like AI processing words in chunks)
                    return speed * 0.3;
                case '.':
                case '!':
                case '?':
                    // Punctuation gets a tiny pause (like AI considering sentence structure)
                    return speed * 2.0;
                case ',':
                case ';':
                case ':':
                    // Minor punctuation gets small pause
                    return speed * 1.5;
                case '\n':
                case '\r':
                    // Line breaks are instant
                    return 0.001;
                default:
                    // Regular characters with variation
                    return speed;
            }
        }

        #endregion

        #region Text Wrapping

        public List<string> WrapText(string text, float maxWidth)
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

        public List<string> RebuildWrappedLines(IEnumerable<string> history, float availableWidth, string? currentPartialLine = null)
        {
            var wrappedLines = new List<string>();
            
            foreach (var line in history)
            {
                var wrapped = WrapText(line, availableWidth);
                wrappedLines.AddRange(wrapped);
            }

            // Add the currently typing line if there is one
            if (!string.IsNullOrEmpty(currentPartialLine))
            {
                var wrapped = WrapText(currentPartialLine, availableWidth);
                wrappedLines.AddRange(wrapped);
            }

            return wrappedLines;
        }

        #endregion

        #region Scrolling

        public struct ScrollResult
        {
            public float NewScrollOffset { get; set; }
            public bool UserScrolled { get; set; }
        }

        public ScrollResult HandleScrolling(
            KeyboardState keyboardState, 
            KeyboardState previousKeyboardState,
            MouseState mouseState, 
            MouseState previousMouseState,
            float currentScrollOffset,
            int totalLines,
            int visibleLineCount)
        {
            var scrollSpeed = 3f;
            var maxScroll = Math.Max(0, totalLines - visibleLineCount);
            var newScrollOffset = currentScrollOffset;
            bool scrolled = false;

            // Keyboard scrolling
            if (keyboardState.IsKeyDown(Keys.PageUp) && !previousKeyboardState.IsKeyDown(Keys.PageUp))
            {
                newScrollOffset = Math.Max(0, newScrollOffset - scrollSpeed);
                scrolled = true;
            }
            else if (keyboardState.IsKeyDown(Keys.PageDown) && !previousKeyboardState.IsKeyDown(Keys.PageDown))
            {
                newScrollOffset = Math.Min(maxScroll, newScrollOffset + scrollSpeed);
                scrolled = true;
            }
            else if (keyboardState.IsKeyDown(Keys.Home) && !previousKeyboardState.IsKeyDown(Keys.Home))
            {
                newScrollOffset = 0;
                scrolled = true;
            }
            else if (keyboardState.IsKeyDown(Keys.End) && !previousKeyboardState.IsKeyDown(Keys.End))
            {
                newScrollOffset = Math.Max(0, totalLines - visibleLineCount);
                scrolled = true;
            }

            // Mouse wheel scrolling
            var scrollWheelDelta = mouseState.ScrollWheelValue - previousMouseState.ScrollWheelValue;
            if (scrollWheelDelta != 0)
            {
                var mouseScrollSpeed = 2f;
                if (scrollWheelDelta > 0) // Scroll up
                {
                    newScrollOffset = Math.Max(0, newScrollOffset - mouseScrollSpeed);
                }
                else // Scroll down
                {
                    newScrollOffset = Math.Min(maxScroll, newScrollOffset + mouseScrollSpeed);
                }
                scrolled = true;
            }

            return new ScrollResult
            {
                NewScrollOffset = newScrollOffset,
                UserScrolled = scrolled
            };
        }

        public float CalculateAutoScrollOffset(int totalLines, int visibleLineCount)
        {
            return Math.Max(0, totalLines - visibleLineCount);
        }

        #endregion

        #region Utility Methods

        public void ClearTypewriter()
        {
            _pendingLines.Clear();
            _currentTypingLine = "";
            _currentCharIndex = 0;
            _typewriterTimer = 0;
        }

        public bool IsTyping => !string.IsNullOrEmpty(_currentTypingLine);

        public int PendingLineCount => _pendingLines.Count;

        #endregion
    }
} 