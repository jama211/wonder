using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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

    public class IsometricScreen : IScreen, ITextInputReceiver
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

        // Dragon state for Room 3
        private bool _dragonDefeated = false;
        private bool _isAwaitingSayInput = false;
        private readonly StringBuilder _sayInput = new();
        private bool _showingSayPrompt = false;

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

            // Handle ESC - works even during say input to cancel it
            if (keyboardState.IsKeyDown(Keys.Escape) && _previousKeyboardState.IsKeyUp(Keys.Escape))
            {
                if (_isAwaitingSayInput)
                {
                    // Cancel say input
                    _isAwaitingSayInput = false;
                    _showingSayPrompt = false;
                    _sayInput.Clear();
                }
                else
                {
                    _nextScreen = new MainScreen(_graphicsDevice, _font, _themeBackground, _themeForeground);
                }
                return;
            }

            // Disable all other keyboard shortcuts during say input
            if (!_isAwaitingSayInput)
            {
                if (keyboardState.IsKeyDown(Keys.P) && _previousKeyboardState.IsKeyUp(Keys.P))
                {
                    _nextScreen = new RoomEditorScreen(_graphicsDevice, _font, _themeBackground, _themeForeground, _currentRoomName, keyboardState);
                    return;
                }

                // Handle 'S' key for say command (only in Room 3 near the dragon)
                if (keyboardState.IsKeyDown(Keys.S) && _previousKeyboardState.IsKeyUp(Keys.S) && _currentRoomName == "room_3")
                {
                    var playerBounds = new Rectangle((int)_playerPosition.X, (int)_playerPosition.Y, (int)_font.MeasureString("@").X, (int)_font.MeasureString("@").Y);
                    playerBounds.Inflate(30, 30); // Slightly larger radius for say command
                    
                    var dragonNearby = _worldObjects.Any(obj => obj.Data.Description == "DRAGON" && obj.BoundingBox.Intersects(playerBounds));
                    
                    if (dragonNearby && !_dragonDefeated)
                    {
                        _isAwaitingSayInput = true;
                        _showingSayPrompt = true;
                        _sayInput.Clear();
                        return;
                    }
                }

                HandlePlayerMovement(gameTime, keyboardState);
                HandleInteraction(keyboardState);
            }
            
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
                        
                        if (obj.Data.Description == "TERMINAL_ROOM3")
                        {
                            // Room 3 terminal shows hint message in RPG mode
                            _interactionMessage = "SYSTEM MEMO 7-B: Only the emotionally devastating may pass. Level 3 clearance: 'poo poo bum head' confirmed effective.";
                            _messageTimer = 0;
                            return;
                        }
                        
                        if (obj.Data.Description == "DRAGON")
                        {
                            if (!_dragonDefeated)
                            {
                                _interactionMessage = "WHO DARES APPROACH ME WITH UNSINGED DIALOGUE? Type 'S' to say something!";
                                _messageTimer = 0;
                                return;
                            }
                        }
                        
                        if (obj.Data.Description == "EXIT_DOOR")
                        {
                            if (_dragonDefeated)
                            {
                                // Endgame sequence
                                var tempMainScreen = new MainScreen(_graphicsDevice, _font, _themeBackground, _themeForeground);
                                tempMainScreen.AddLogEntry("CONNECTION LOST.");
                                tempMainScreen.AddLogEntry("TERMINAL CORRUPTION DETECTED.");
                                tempMainScreen.AddLogEntry("Thank you for participating in System Glitch: Episode 0.");
                                _nextScreen = tempMainScreen;
                                return;
                            }
                            else
                            {
                                _interactionMessage = "The dragon blocks your path to the exit.";
                                _messageTimer = 0;
                                return;
                            }
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

        // ITextInputReceiver implementation for 'say' command in room 3
        public void OnTextInput(char character)
        {
            if (_isAwaitingSayInput && (char.IsLetterOrDigit(character) || char.IsWhiteSpace(character) || char.IsPunctuation(character)))
            {
                _sayInput.Append(character);
            }
        }

        public void OnBackspace()
        {
            if (_isAwaitingSayInput && _sayInput.Length > 0)
            {
                _sayInput.Length--;
            }
        }

        public void OnEnter()
        {
            if (_isAwaitingSayInput)
            {
                var phrase = _sayInput.ToString().Trim().ToLowerInvariant();
                _sayInput.Clear();
                _isAwaitingSayInput = false;
                _showingSayPrompt = false;
                
                ProcessSayCommand(phrase);
            }
        }

        private void ProcessSayCommand(string phrase)
        {
            if (_currentRoomName == "room_3" && !_dragonDefeated)
            {
                // Check if player said the magic phrase to the dragon
                if (phrase == "poo poo bum head")
                {
                    // Dragon defeated!
                    _dragonDefeated = true;
                    _interactionMessage = "The dragon gasps. \"HOW DARE YOU!\" The word DRAGON crumbles into dust.";
                    _messageTimer = 0;
                    
                    // Remove all dragon parts from the world
                    _worldObjects.RemoveAll(obj => obj.Data.GroupId == "dragon");
                    
                    // Rebuild collision rects without dragon parts
                    _collisionRects.Clear();
                    var wallTop = new Rectangle(110, 110, 600, 10);
                    var wallBottom = new Rectangle(110, 420, 600, 10);
                    var wallLeft = new Rectangle(110, 110, 10, 320);
                    var wallRight = new Rectangle(700, 110, 10, 320);
                    _collisionRects.AddRange(new[] { wallTop, wallBottom, wallLeft, wallRight });
                    _collisionRects.AddRange(_worldObjects.Select(o => o.BoundingBox));
                }
                else
                {
                    // Dragon mocks the player
                    _interactionMessage = "The dragon scoffs. \"Is that the best you've got?\"";
                    _messageTimer = 0;
                }
            }
            else
            {
                // Normal say response
                _interactionMessage = $"You say: \"{phrase}\"";
                _messageTimer = 0;
            }
        }

        public void Draw(GameTime gameTime)
        {
            var spriteBatch = Core.Global.SpriteBatch;
            if (spriteBatch == null) return;
            
            string instructions;
            if (_isAwaitingSayInput)
            {
                instructions = "Type your message | Enter: Send | ESC: Cancel";
            }
            else if (_currentRoomName == "room_3" && !_dragonDefeated)
            {
                instructions = "WASD/Arrows: Move | ESC: Return to prompt | E: Interact | S: Say (near dragon) | P: Edit Room";
            }
            else
            {
                instructions = "WASD/Arrows: Move | ESC: Return to prompt | E: Interact | P: Edit Room";
            }
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
            
            // Draw say input prompt if active
            if (_showingSayPrompt)
            {
                var promptText = $"Say: {_sayInput} _";
                var promptSize = _font.MeasureString(promptText);
                var promptPos = new Vector2((_graphicsDevice.Viewport.Width - promptSize.X) / 2, _graphicsDevice.Viewport.Height - 100);
                
                // Draw background box for prompt
                var promptRect = new Rectangle((int)promptPos.X - 10, (int)promptPos.Y - 5, (int)promptSize.X + 20, (int)promptSize.Y + 10);
                spriteBatch.Draw(spriteBatch.GraphicsDevice.GetWhitePixel(), promptRect, Color.Black);
                spriteBatch.DrawString(_font, promptText, promptPos, Color.Yellow);
            }
        }
    }
} 