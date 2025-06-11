using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using WonderGame.Core;

namespace WonderGame.Screens
{
    // Represents an object in the isometric world, defined by its name and position.
    public class WorldObject
    {
        public RoomObject Data { get; }
        public Vector2 Position { get; }
        public Rectangle BoundingBox { get; }
        public Vector2 Scale { get; }

        public WorldObject(RoomObject data, SpriteFont font)
        {
            Data = data;
            Position = new Vector2(data.X, data.Y);
            Scale = new Vector2(data.ScaleX, data.ScaleY);
            var size = font.MeasureString(data.Name) * Scale;
            BoundingBox = new Rectangle((int)Position.X, (int)Position.Y, (int)size.X, (int)size.Y);
        }
    }

    public class IsometricScreen : IScreen
    {
        private readonly SpriteFont _font;
        private readonly Color _themeForeground;
        private readonly Color _themeBackground;
        
        private Vector2 _playerPosition;
        private List<WorldObject> _worldObjects = new();
        private List<Rectangle> _collisionRects = new();

        private IScreen? _nextScreen;
        private readonly GraphicsDevice _graphicsDevice;
        
        private string? _interactionMessage;
        private float _messageTimer;
        private const float MESSAGE_DURATION = 3f; // seconds

        public IsometricScreen(GraphicsDevice graphicsDevice, SpriteFont font, Color themeBackground, Color themeForeground)
        {
            _graphicsDevice = graphicsDevice;
            _font = font;
            _themeForeground = themeForeground;
            _themeBackground = themeBackground;
            
            LoadRoom("room_1");
        }
        
        private void LoadRoom(string roomFileName)
        {
            var path = Path.Combine("Data", "Rooms", $"{roomFileName}.json");
            if (!File.Exists(path))
            {
                // Handle error, maybe go to an error screen or show a message
                Console.WriteLine($"Could not find room file: {path}");
                return;
            }

            var jsonString = File.ReadAllText(path);
            var roomData = JsonSerializer.Deserialize<Room>(jsonString);

            _worldObjects.Clear();
            _collisionRects.Clear();

            if (roomData != null)
            {
                foreach (var objData in roomData.Objects)
                {
                    _worldObjects.Add(new WorldObject(objData, _font));
                }
            }
            
            _playerPosition = new Vector2(250, 200);

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

            if (keyboardState.IsKeyDown(Keys.Escape))
            {
                _nextScreen = new MainScreen(_graphicsDevice, _font, _themeBackground, _themeForeground);
                return;
            }

            HandlePlayerMovement(gameTime, keyboardState);
            HandleInteraction(keyboardState);
            
            // Message timer
            if (_interactionMessage != null)
            {
                _messageTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                if (_messageTimer > MESSAGE_DURATION)
                {
                    _interactionMessage = null;
                    _messageTimer = 0;
                }
            }
        }
        
        private void HandleInteraction(KeyboardState keyboardState)
        {
            if (keyboardState.IsKeyDown(Keys.E))
            {
                var playerBounds = new Rectangle((int)_playerPosition.X, (int)_playerPosition.Y, (int)_font.MeasureString("@").X, (int)_font.MeasureString("@").Y);
                
                // Add a small buffer to the player bounds for easier interaction
                playerBounds.Inflate(10, 10);

                foreach (var obj in _worldObjects)
                {
                    if (obj.BoundingBox.Intersects(playerBounds))
                    {
                        if (obj.Data.DoorTo != null)
                        {
                            LoadRoom(obj.Data.DoorTo);
                            return; // Stop checking after transitioning
                        }
                        if (obj.Data.Description != null)
                        {
                            _interactionMessage = obj.Data.Description;
                            _messageTimer = 0;
                        }
                    }
                }
            }
        }

        private void HandlePlayerMovement(GameTime gameTime, KeyboardState keyboardState)
        {
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

                _playerPosition.X += moveAmount.X;
                var nextPlayerBounds = new Rectangle((int)_playerPosition.X, (int)originalPosition.Y, (int)playerSize.X, (int)playerSize.Y);
                if (_collisionRects.Any(rect => rect.Intersects(nextPlayerBounds)))
                {
                    _playerPosition.X = originalPosition.X;
                }

                _playerPosition.Y += moveAmount.Y;
                nextPlayerBounds = new Rectangle((int)_playerPosition.X, (int)_playerPosition.Y, (int)playerSize.X, (int)playerSize.Y);
                if (_collisionRects.Any(rect => rect.Intersects(nextPlayerBounds)))
                {
                    _playerPosition.Y = originalPosition.Y;
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
                spriteBatch.DrawString(_font, obj.Data.Name, obj.Position, _themeForeground, 0, Vector2.Zero, obj.Scale, SpriteEffects.None, 0);
            }
            
            // Draw player
            spriteBatch.DrawString(_font, "@", _playerPosition, Color.LawnGreen);
            
            if (_interactionMessage != null)
            {
                var messageSize = _font.MeasureString(_interactionMessage);
                var messagePos = new Vector2((_graphicsDevice.Viewport.Width - messageSize.X) / 2, _graphicsDevice.Viewport.Height - 50);
                spriteBatch.DrawString(_font, _interactionMessage, messagePos, Color.White);
            }
        }
    }
} 