using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace WonderGame.Screens;

public class BootScreen : IScreen
{
    private readonly SpriteFont _font;
    private double _timer;
    private int _bootMessageIndex;
    public bool IsComplete { get; private set; }

    private static readonly string[] BootMessages =
    {
        "INITIALIZING...",
        "LOADING KERNEL...",
        "VERIFYING SYSTEM INTEGRITY...",
        "DECRYPTING DATA STREAMS...",
        "ESTABLISHING SECURE CONNECTION...",
        "LOADING INTERFACE...",
        "BOOT SEQUENCE COMPLETE.",
        ""
    };

    public BootScreen(SpriteFont font)
    {
        _font = font;
    }

    public void Update(GameTime gameTime)
    {
        if (IsComplete) return;

        _timer += gameTime.ElapsedGameTime.TotalSeconds;
        if (_timer > 0.25)
        {
            _timer = 0;
            if (_bootMessageIndex < BootMessages.Length - 1)
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
        for (int i = 0; i <= _bootMessageIndex; i++)
        {
            spriteBatch.DrawString(_font, BootMessages[i], new Vector2(10, 10 + i * 20), Color.Green);
        }
    }
} 