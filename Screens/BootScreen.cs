using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using WonderGame.Core;

namespace WonderGame.Screens;

public class BootScreen : IScreen
{
    // Configuration for line display speed and pauses
    private const float MIN_LINE_SPEED_SECONDS = 0.000f;
    private const float LINE_SPEED_VARIABILITY_SECONDS = 0.005f;
    private const float CHANCE_OF_LONG_PAUSE = 0.01f; // 1% chance
    private const float LONG_PAUSE_MIN_SECONDS = 0.01f;
    private const float LONG_PAUSE_VARIABILITY_SECONDS = 0.3f;
    
    private readonly TextRenderer _textRenderer;
    private readonly List<string> _bootMessages;
    private readonly Random _random = new();
    private float _timer;
    private int _currentLine;
    private float _currentLineTime;
    
    private IScreen? _nextScreen;
    private readonly Func<IScreen> _createNextScreen;

    public BootScreen(GraphicsDevice graphicsDevice, SpriteFont font, Color themeForeground, Func<IScreen> createNextScreen)
    {
        _textRenderer = new TextRenderer(graphicsDevice, font, themeForeground);
        _bootMessages = LoadBootMessages("Data/boot_sequence.txt");
        _textRenderer.SetLines(_bootMessages);

        _timer = 0f;
        _currentLine = 0;
        _createNextScreen = createNextScreen;
        SetNextLineTime();
    }

    private void SetNextLineTime()
    {
        if (_random.NextDouble() < CHANCE_OF_LONG_PAUSE)
        {
            // Introduce a long, dramatic pause
            _currentLineTime = LONG_PAUSE_MIN_SECONDS + (float)_random.NextDouble() * LONG_PAUSE_VARIABILITY_SECONDS;
        }
        else
        {
            // Set a normal, very fast random time
            _currentLineTime = MIN_LINE_SPEED_SECONDS + (float)_random.NextDouble() * LINE_SPEED_VARIABILITY_SECONDS;
        }
    }

    private List<string> LoadBootMessages(string filePath)
    {
        if (File.Exists(filePath))
        {
            return new List<string>(File.ReadAllLines(filePath));
        }
        
        Console.WriteLine($"Error: Boot sequence file not found at {filePath}");
        return new List<string> { "Error: Boot sequence file not found." };
    }

    public void Update(GameTime gameTime)
    {
        if (_nextScreen != null || _currentLine >= _bootMessages.Count) return;

        _timer += (float)gameTime.ElapsedGameTime.TotalSeconds;

        // Process multiple lines in one frame if enough time has passed
        while (_timer >= _currentLineTime)
        {
            _timer -= _currentLineTime;
            _currentLine++;
            SetNextLineTime();

            if (_currentLine >= _bootMessages.Count)
            {
                _nextScreen = _createNextScreen();
                break; // Exit loop, sequence is complete
            }
        }
    }

    public void Draw(GameTime gameTime)
    {
        // The TextRenderer now handles everything: clearing the screen is done
        // in the main Program.cs, and the SpriteBatch is handled globally.
        _textRenderer.Draw(_currentLine);
    }

    public IScreen? GetNextScreen()
    {
        return _nextScreen;
    }
} 