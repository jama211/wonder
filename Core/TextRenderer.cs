using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Text;

namespace WonderGame.Core
{
    /// <summary>
    /// Handles rendering a list of text lines with automatic scrolling and character validation.
    /// </summary>
    public class TextRenderer
    {
        private readonly GraphicsDevice _graphicsDevice;
        private readonly SpriteFont _font;
        private readonly Color _foregroundColor;
        private List<string> _lines = new List<string>();
        private int _maxVisibleLines;

        public TextRenderer(GraphicsDevice graphicsDevice, SpriteFont font, Color foregroundColor)
        {
            _graphicsDevice = graphicsDevice;
            _font = font;
            _foregroundColor = foregroundColor;
            
            // Calculate how many lines can fit in the viewport
            _maxVisibleLines = _graphicsDevice.Viewport.Height / _font.LineSpacing;
        }

        /// <summary>
        /// Sets the complete list of lines to be managed by the renderer.
        /// </summary>
        public void SetLines(List<string> lines)
        {
            _lines = lines ?? new List<string>();
        }

        /// <summary>
        /// Draws the visible portion of the text lines.
        /// </summary>
        /// <param name="line_to_focus_on">The line number that should be brought into view.</param>
        public void Draw(int line_to_focus_on)
        {
            if (_lines.Count == 0) return;

            // Determine the starting line to display to ensure the latest lines are visible
            int startLine = Math.Max(0, line_to_focus_on - _maxVisibleLines + 1);

            for (int i = 0; i < _maxVisibleLines; i++)
            {
                int currentLineIndex = startLine + i;
                if (currentLineIndex >= _lines.Count) continue;

                string lineToDraw = _lines[currentLineIndex];
                Vector2 position = new Vector2(0, i * _font.LineSpacing);

                StringBuilder renderableLine = new StringBuilder();
                for (int charIndex = 0; charIndex < lineToDraw.Length; charIndex++)
                {
                    char c = lineToDraw[charIndex];
                    // Only append characters that the font can render
                    if (_font.Characters.Contains(c))
                    {
                        renderableLine.Append(c);
                    }
                    else
                    {
                        Console.WriteLine($"[WARNING] Unsupported character '{c}' (ASCII: {(int)c}) found on line {currentLineIndex + 1}: \"{lineToDraw}\"");
                    }
                }

                // We pass the SpriteBatch in the Draw call in MonoGame 3.8+
                // but for now this example assumes it's handled outside.
                // This will be called between a _spriteBatch.Begin() and .End()
                Global.SpriteBatch?.DrawString(_font, renderableLine.ToString(), position, _foregroundColor);
            }
        }
    }

    // A static class to hold global references, like the SpriteBatch.
    // This simplifies passing the SpriteBatch to every single draw call.
    public static class Global
    {
        public static SpriteBatch? SpriteBatch { get; set; }
    }
} 