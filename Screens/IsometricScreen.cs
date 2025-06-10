using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using WonderGame.Core;

namespace WonderGame.Screens
{
    // Represents an object in the isometric world, defined by its name and position.
    public class WorldObject
    {
        public string Name { get; }
        public Vector2 Position { get; }
        public Rectangle BoundingBox { get; }
        public Vector2 Scale { get; }

        public WorldObject(string name, Vector2 position, SpriteFont font, Vector2? scale = null)
        {
            Name = name;
            Position = position;
            Scale = scale ?? Vector2.One;
            var size = font.MeasureString(name) * Scale;
            BoundingBox = new Rectangle((int)position.X, (int)position.Y, (int)size.X, (int)size.Y);
        }
    }

    public class IsometricScreen : IScreen
    {
        private readonly SpriteFont _font;
        private readonly Color _themeForeground;
        private readonly Color _themeBackground;
        
        private Vector2 _playerPosition;
        private readonly List<WorldObject> _worldObjects = new();
        private readonly List<Rectangle> _collisionRects = new();

        private IScreen? _nextScreen;
        private readonly GraphicsDevice _graphicsDevice;

        public IsometricScreen(GraphicsDevice graphicsDevice, SpriteFont font, Color themeBackground, Color themeForeground)
        {
            _graphicsDevice = graphicsDevice;
            _font = font;
            _themeForeground = themeForeground;
            _themeBackground = themeBackground;
            
            _playerPosition = new Vector2(250, 200); // Starting position

            // Define objects, their positions, and scales
            _worldObjects.Add(new WorldObject("T\nA\nB\nL\nE", new Vector2(300, 220), _font, new Vector2(2.5f, 1.5f))); // Stretched
            _worldObjects.Add(new WorldObject("FRIDGE", new Vector2(500, 150), _font));
            _worldObjects.Add(new WorldObject("LIGHTBULB", new Vector2(350, 80), _font, new Vector2(0.8f, 0.8f))); // Scaled down
            _worldObjects.Add(new WorldObject("BANANA", new Vector2(200, 350), _font));
            var door = new WorldObject("D\nO\nO\nR", new Vector2(690, 200), _font);
            _worldObjects.Add(door);

            // Create walls for collision based on the dot layout
            var wallTop = new Rectangle(110, 110, 600, 10);
            var wallBottom = new Rectangle(110, 420, 600, 10);
            var wallLeft = new Rectangle(110, 110, 10, 320);
            var wallRight = new Rectangle(700, 110, 10, 320);
            
            _collisionRects.AddRange(new[] { wallTop, wallBottom, wallLeft, wallRight });
            _collisionRects.AddRange(_worldObjects.Select(o => o.BoundingBox));
        }
        
        public IScreen? GetNextScreen() => _nextScreen;

        public void Update(GameTime gameTime)
        {
            var keyboardState = Keyboard.GetState();

            // Return to main screen on ESC
            if (keyboardState.IsKeyDown(Keys.Escape))
            {
                _nextScreen = new MainScreen(_graphicsDevice, _font, _themeBackground, _themeForeground);
                return;
            }

            var moveDirection = Vector2.Zero;
            const float speed = 200f;

            if (keyboardState.IsKeyDown(Keys.W) || keyboardState.IsKeyDown(Keys.Up)) moveDirection.Y -= 1;
            if (keyboardState.IsKeyDown(Keys.S) || keyboardState.IsKeyDown(Keys.Down)) moveDirection.Y += 1;
            if (keyboardState.IsKeyDown(Keys.A) || keyboardState.IsKeyDown(Keys.Left)) moveDirection.X -= 1;
            if (keyboardState.IsKeyDown(Keys.D) || keyboardState.IsKeyDown(Keys.Right)) moveDirection.X += 1;

            if (moveDirection != Vector2.Zero)
            {
                moveDirection.Normalize();
                var moveAmount = moveDirection * speed * (float)gameTime.ElapsedGameTime.TotalSeconds;
                
                var playerSize = _font.MeasureString("@");
                var originalPosition = _playerPosition;

                // Check X-axis collision
                _playerPosition.X += moveAmount.X;
                var nextPlayerBounds = new Rectangle((int)_playerPosition.X, (int)originalPosition.Y, (int)playerSize.X, (int)playerSize.Y);
                foreach (var rect in _collisionRects)
                {
                    if (rect.Intersects(nextPlayerBounds))
                    {
                        _playerPosition.X = originalPosition.X; // Collision, revert X
                        break;
                    }
                }

                // Check Y-axis collision
                _playerPosition.Y += moveAmount.Y;
                nextPlayerBounds = new Rectangle((int)_playerPosition.X, (int)_playerPosition.Y, (int)playerSize.X, (int)playerSize.Y);
                foreach (var rect in _collisionRects)
                {
                    if (rect.Intersects(nextPlayerBounds))
                    {
                        _playerPosition.Y = originalPosition.Y; // Collision, revert Y
                        break;
                    }
                }
            }
        }

        public void Draw(GameTime gameTime)
        {
            var spriteBatch = Core.Global.SpriteBatch;
            if (spriteBatch == null) return;
            
            string instructions = "WASD/Arrows: Move | ESC: Return to prompt | E: Interact";
            spriteBatch.DrawString(_font, instructions, new Vector2(20, 20), Color.Gray);

            // Draw floor dots
            for (int x = 0; x < 30; x++)
            {
                for (int y = 0; y < 15; y++)
                {
                    spriteBatch.DrawString(_font, ".", new Vector2(110 + x * 20, 110 + y * 20), _themeForeground, 0, Vector2.Zero, 0.8f, SpriteEffects.None, 0);
                }
            }

            // Draw objects and cover floor dots behind them
            foreach (var obj in _worldObjects)
            {
                spriteBatch.Draw(spriteBatch.GraphicsDevice.GetWhitePixel(), obj.BoundingBox, _themeBackground);
                spriteBatch.DrawString(_font, obj.Name, obj.Position, _themeForeground, 0, Vector2.Zero, obj.Scale, SpriteEffects.None, 0);
            }
            
            // Draw player
            spriteBatch.DrawString(_font, "@", _playerPosition, Color.LawnGreen);
        }
    }
} 