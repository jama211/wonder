using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using WonderGame.Core;
using WonderGame.Data;

namespace WonderGame.Screens
{
    // Represents an object in the isometric world, defined by its name and position.
    public class WorldObject
    {
        public Data.RoomObject Data { get; }
        public Vector2 Position { get; private set; }
        public Rectangle BoundingBox { get; private set; }
        public Vector2 Scale { get; private set; }
        private readonly SpriteFont _font;
        
        // this is a magic number to compensate for the fact that the default bounds of text 
        // has a lot of space underneath it for the tails of letters like 'y'. we can tweak 
        // this to get the bounding box to be more snug
        private const float TEXT_BOUNDS_VERTICAL_ADJUSTMENT = 0.35f;

        public WorldObject(Data.RoomObject data, SpriteFont font)
        {
            Data = data;
            _font = font;
            UpdateBoundingBox();
        }

        public void UpdateBoundingBox()
        {
            Position = new Vector2(Data.X, Data.Y);
            Scale = new Vector2(Data.ScaleX, Data.ScaleY);
            var size = _font.MeasureString(Data.Name) * Scale;

            // we have to fudge the Y value of the bounding box because the default MeasureString
            // leaves a lot of space for the tails of letters like 'y'.
            // but we only do this if there are no newlines in the text
            if (!Data.Name.Contains('\n'))
            {
                var fudgedHeight = size.Y - (size.Y * TEXT_BOUNDS_VERTICAL_ADJUSTMENT);
                BoundingBox = new Rectangle((int)Position.X, (int)Position.Y, (int)size.X, (int)fudgedHeight);
            }
            else
            {
                BoundingBox = new Rectangle((int)Position.X, (int)Position.Y, (int)size.X, (int)size.Y);
            }
        }

        public void Draw(SpriteBatch spriteBatch, Color color)
        {
            spriteBatch.DrawString(_font, Data.Name, Position, color, 0, Vector2.Zero, Scale, SpriteEffects.None, 0);
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

        private string _currentRoomName = "";
        private KeyboardState _previousKeyboardState;

        // Random generator for insults
        private readonly Random _random = new();
        
        // List of insults for the INSULT GENERATOR
        private readonly string[] _insults = {
            "You look like a spreadsheet with no formulas.",
            "Your code has more bugs than a picnic.",
            "I've seen better arguments in YouTube comments.",
            "You smell like burnt RAM.",
            "poo poo bum head"
        };

        public IsometricScreen(GraphicsDevice graphicsDevice, SpriteFont font, Color themeBackground, Color themeForeground, string startingRoomName, KeyboardState? previousKeyboardState = null)
        {
            _graphicsDevice = graphicsDevice;
            _font = font;
            _themeForeground = themeForeground;
            _themeBackground = themeBackground;
            _previousKeyboardState = previousKeyboardState ?? Keyboard.GetState();
            
            LoadRoom(startingRoomName, null);
        }
        
        private void PositionPlayerAtEntrance(string? comingFromRoomFileName)
        {
            if (comingFromRoomFileName == null)
            {
                _playerPosition = new Vector2(250, 200); // Initial game start
                return;
            }

            var entranceDoor = _worldObjects.FirstOrDefault(o => o.Data.DoorTo == comingFromRoomFileName);
            if (entranceDoor != null)
            {
                // Position player based on which wall the door is on
                if (entranceDoor.Position.X < 200) // Left wall
                {
                    _playerPosition = new Vector2(entranceDoor.Position.X + entranceDoor.BoundingBox.Width + 20, entranceDoor.Position.Y);
                }
                else if (entranceDoor.Position.X > 650) // Right wall
                {
                    _playerPosition = new Vector2(entranceDoor.Position.X - 40, entranceDoor.Position.Y);
                }
                else if (entranceDoor.Position.Y < 150) // Top wall
                {
                    _playerPosition = new Vector2(entranceDoor.Position.X, entranceDoor.Position.Y + entranceDoor.BoundingBox.Height + 20);
                }
                else // Bottom wall
                {
                    _playerPosition = new Vector2(entranceDoor.Position.X, entranceDoor.Position.Y - 40);
                }
            }
            else
            {
                _playerPosition = new Vector2(250, 200); // Fallback
            }
        }
        
        private void LoadRoom(string roomFileName, string? comingFromRoomFileName)
        {
            var path = Path.Combine("Data", "Rooms", $"{roomFileName}.json");
            if (!File.Exists(path))
            {
                Console.WriteLine($"Could not find room file: {path}");
                _nextScreen = new MainScreen(_graphicsDevice, _font, _themeBackground, _themeForeground); // Go back if room not found
                return;
            }

            var jsonString = File.ReadAllText(path);
            var roomData = JsonSerializer.Deserialize<Room>(jsonString);

            _worldObjects.Clear();
            _collisionRects.Clear();
            _currentRoomName = roomFileName;

            if (roomData != null)
            {
                foreach (var objData in roomData.Objects)
                {
                    _worldObjects.Add(new WorldObject(objData, _font));
                }
            }
            
            PositionPlayerAtEntrance(comingFromRoomFileName);

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

            if (keyboardState.IsKeyDown(Keys.Escape) && _previousKeyboardState.IsKeyUp(Keys.Escape))
            {
                _nextScreen = new MainScreen(_graphicsDevice, _font, _themeBackground, _themeForeground);
                return;
            }

            if (keyboardState.IsKeyDown(Keys.P) && _previousKeyboardState.IsKeyUp(Keys.P))
            {
                _nextScreen = new RoomEditorScreen(_graphicsDevice, _font, _themeBackground, _themeForeground, _currentRoomName, keyboardState);
                return;
            }

            HandlePlayerMovement(gameTime, keyboardState);
            HandleInteraction(keyboardState);
            
            if (_interactionMessage != null)
            {
                _messageTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                if (_messageTimer > MESSAGE_DURATION)
                {
                    _interactionMessage = null;
                    _messageTimer = 0;
                }
            }
            _previousKeyboardState = keyboardState;
        }
        
        private void HandleInteraction(KeyboardState keyboardState)
        {
            if (keyboardState.IsKeyDown(Keys.E) && !_previousKeyboardState.IsKeyDown(Keys.E))
            {
                var playerBounds = new Rectangle((int)_playerPosition.X, (int)_playerPosition.Y, (int)_font.MeasureString("@").X, (int)_font.MeasureString("@").Y);
                playerBounds.Inflate(10, 10);

                foreach (var obj in _worldObjects)
                {
                    if (obj.BoundingBox.Intersects(playerBounds))
                    {
                        // Handle special interactions first
                        if (obj.Data.Description == "INSULT_GENERATOR")
                        {
                            var randomInsult = _insults[_random.Next(_insults.Length)];
                            _interactionMessage = randomInsult;
                            _messageTimer = 0;
                            return;
                        }
                        
                        if (obj.Data.Description == "TERMINAL_ROOM2")
                        {
                            // Create a temporary MainScreen with the log entry
                            var tempMainScreen = new MainScreen(_graphicsDevice, _font, _themeBackground, _themeForeground);
                            tempMainScreen.AddLogEntry("> LOG ENTRY 442A:");
                            tempMainScreen.AddLogEntry("> Subject 442A attempted use of 'smelly pants' insult. Insufficient offense. Subject devoured.");
                            _nextScreen = tempMainScreen;
                            return;
                        }

                        // Handle door interactions
                        if (obj.Data.DoorTo != null)
                        {
                            LoadRoom(obj.Data.DoorTo, _currentRoomName);
                            return;
                        }
                        
                        // Handle regular description interactions
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
            
            string instructions = "WASD/Arrows: Move | ESC: Return to prompt | E: Interact | P: Edit Room";
            spriteBatch.DrawString(_font, instructions, new Vector2(20, 20), Color.Gray);

            // Draw current room name
            var roomNameText = $"Location: {_currentRoomName}";
            var roomNameSize = _font.MeasureString(roomNameText);
            spriteBatch.DrawString(_font, roomNameText, new Vector2(_graphicsDevice.Viewport.Width - roomNameSize.X - 20, 20 + _font.LineSpacing), Color.Gray);

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
                obj.Draw(spriteBatch, _themeForeground);
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