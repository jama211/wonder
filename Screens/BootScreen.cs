using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;

namespace WonderGame.Screens;

public class BootScreen : IScreen
{
    private readonly SpriteFont _font;
    private readonly GraphicsDevice _graphicsDevice;
    private double _timer;
    private int _bootMessageIndex;
    public bool IsComplete { get; private set; }

    private readonly string[] _bootMessages;
    private const double TimePerLine = 0.01;

    public BootScreen(SpriteFont font, GraphicsDevice graphicsDevice)
    {
        _font = font;
        _graphicsDevice = graphicsDevice;

        var lines = File.ReadAllLines("Data/boot_sequence.txt");
        for (int i = 0; i < lines.Length; i++)
        {
            lines[i] = lines[i].Replace("\t", "    ");
        }
        _bootMessages = lines;
    }

    public void Update(GameTime gameTime)
    {
        if (IsComplete) return;

        _timer += gameTime.ElapsedGameTime.TotalSeconds;
        if (_timer > TimePerLine)
        {
            _timer = 0;
            if (_bootMessageIndex < _bootMessages.Length)
            {
                _bootMessageIndex++;
            }
            else
            {
                IsComplete = true;
            }
        }
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        var lineHeight = _font.LineSpacing;
        var visibleLines = _graphicsDevice.Viewport.Height / lineHeight;

        var startLine = 0;
        if (_bootMessageIndex >= visibleLines)
        {
            startLine = _bootMessageIndex - visibleLines;
            if (startLine >= _bootMessages.Length)
            {
                startLine = _bootMessages.Length - visibleLines;
            }
        }

        var endLine = Math.Min(_bootMessageIndex, _bootMessages.Length);

        for (int i = startLine; i < endLine; i++)
        {
            var y = 10 + (i - startLine) * lineHeight;
            spriteBatch.DrawString(_font, _bootMessages[i], new Vector2(10, y), Color.Green);
        }
    }
} 