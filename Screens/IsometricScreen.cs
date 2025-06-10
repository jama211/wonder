using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;

namespace WonderGame.Screens
{
    // Represents an object in the isometric world, defined by its name and position.
    public class WorldObject
    {
        public string Name { get; }
        public Vector2 Position { get; }
        public Rectangle BoundingBox { get; }

        public WorldObject(string name, Vector2 position, SpriteFont font)
        {
            Name = name;
            Position = position;
            var size = font.MeasureString(name);
            BoundingBox = new Rectangle((int)position.X, (int)position.Y, (int)size.X, (int)size.Y);
        }
    }

    public class IsometricScreen : IScreen
    {
        private readonly SpriteFont _font;
        private readonly Color _themeForeground;
        private readonly Color _themeBackground;
        
        private Vector2 _playerPosition;
        private readonly List<WorldObject> _worldObjects;

        private readonly List<string> _roomLayout;
        private readonly WorldObject _door;

        public IsometricScreen(GraphicsDevice graphicsDevice, SpriteFont font, Color themeBackground, Color themeForeground)
        {
            _font = font;
            _themeForeground = themeForeground;
            _themeBackground = themeBackground;
            
            _playerPosition = new Vector2(250, 200); // Starting position

            // Define objects and their positions
            _worldObjects = new List<WorldObject>
            {
                new WorldObject("TABLE", new Vector2(300, 250), _font),
                new WorldObject("FRIDGE", new Vector2(500, 150), _font)
            };

            // Define the visual layout of the room
            _roomLayout = new List<string>
            {
                "------------------------------------------------------------",
                "|..........................................................|",
                "|..........................................................|",
                "|..........................................................|",
                "|..........................................................|",
                "|..........................................................|",
                "|..........................................................|",
                "|..........................................................|",
                "|..........................................................|",
                "------------------------------------------------------------"
            };
            
            _door = new WorldObject("D\nO\nO\nR", new Vector2(600, 200), _font);
            _worldObjects.Add(_door);
        }
        
        public IScreen? GetNextScreen() => null; // No transitions from this screen yet

        public void Update(GameTime gameTime)
        {
            var keyboardState = Keyboard.GetState();
            var moveDirection = Vector2.Zero;
            const float speed = 200f; // Pixels per second

            if (keyboardState.IsKeyDown(Keys.W) || keyboardState.IsKeyDown(Keys.Up)) moveDirection.Y -= 1;
            if (keyboardState.IsKeyDown(Keys.S) || keyboardState.IsKeyDown(Keys.Down)) moveDirection.Y += 1;
            if (keyboardState.IsKeyDown(Keys.A) || keyboardState.IsKeyDown(Keys.Left)) moveDirection.X -= 1;
            if (keyboardState.IsKeyDown(Keys.D) || keyboardState.IsKeyDown(Keys.Right)) moveDirection.X += 1;

            if (moveDirection != Vector2.Zero)
            {
                moveDirection.Normalize();
                var nextPosition = _playerPosition + moveDirection * speed * (float)gameTime.ElapsedGameTime.TotalSeconds;
                
                // Player's bounding box at the next position
                var playerSize = _font.MeasureString("@");
                var nextPlayerBounds = new Rectangle((int)nextPosition.X, (int)nextPosition.Y, (int)playerSize.X, (int)playerSize.Y);

                // Check for collision with any world object
                bool collisionDetected = false;
                foreach (var obj in _worldObjects)
                {
                    if (obj.BoundingBox.Intersects(nextPlayerBounds))
                    {
                        collisionDetected = true;
                        break;
                    }
                }
                
                // Only update position if no collision is detected
                if (!collisionDetected)
                {
                    _playerPosition = nextPosition;
                }
            }
        }

        public void Draw(GameTime gameTime)
        {
            var spriteBatch = Core.Global.SpriteBatch;
            if (spriteBatch == null) return;

            // 1. Draw the room layout
            var y = 100;
            foreach (var line in _roomLayout)
            {
                spriteBatch.DrawString(_font, line, new Vector2(100, y), _themeForeground);
                y += _font.LineSpacing;
            }

            // 2. Draw all the world objects (including the door)
            foreach (var obj in _worldObjects)
            {
                spriteBatch.DrawString(_font, obj.Name, obj.Position, _themeForeground);
            }

            // 3. Draw the player
            spriteBatch.DrawString(_font, "@", _playerPosition, Color.LawnGreen); // Make player stand out
        }
    }
} 