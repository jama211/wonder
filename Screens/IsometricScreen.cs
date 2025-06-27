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
            // Programming/Tech insults
            "You look like a spreadsheet with no formulas.",
            "Your code has more bugs than a picnic.",
            "I've seen better arguments in YouTube comments.",
            "You smell like burnt RAM.",
            "Your programming skills are like Internet Explorer - slow and outdated.",
            "You're the human equivalent of a buffer overflow.",
            "Your brain runs on Internet Explorer 6.",
            "You're like a null pointer exception - completely useless.",
            "Your IQ is lower than your Git commit count.",
            "You code like you're still using punch cards.",
            "Your debugging skills are about as good as a chocolate teapot.",
            "You're the reason Stack Overflow has a downvote button.",
            "Your code looks like it was written by a caffeinated monkey.",
            "You're like a memory leak - slowly making everything worse.",
            "Your algorithms have the efficiency of a potato.",
            "You write code like you're paid by the semicolon.",
            "Your variable names are more confusing than IKEA instructions.",
            "You're the human equivalent of a 404 error.",
            "Your CSS is more broken than your promises.",
            "You debug like you're playing whack-a-mole blindfolded.",
            
            // General silly insults
            "poo poo bum head",
            "You're about as useful as a screen door on a submarine.",
            "Your face looks like it caught fire and someone tried to put it out with a fork.",
            "You're so dense, light bends around you.",
            "You have the personality of a wet mop.",
            "You're like a parking meter - annoying and nobody wants to pay attention to you.",
            "Your brain is like a browser with 47 tabs open, 5 of them are frozen.",
            "You're the reason aliens won't visit Earth.",
            "You have the charm of a paper cut.",
            "Your ideas are about as sharp as a bowling ball.",
            "You're like a human participation trophy.",
            "You have the attention span of a goldfish with ADHD.",
            "You're about as bright as a black hole.",
            "Your common sense is rarer than a unicorn.",
            "You're like a broken pencil - pointless.",
            "You have the grace of a drunk giraffe.",
            "Your logic is more twisted than a pretzel factory.",
            "You're like a human speed bump.",
            "You have the organizational skills of a tornado.",
            "You're about as smooth as chunky peanut butter.",
            "Your wit is duller than a butter knife.",
            "You're like a soup sandwich - messy and pointless.",
            "You have the coordination of a newborn giraffe on ice.",
            "You're about as helpful as a chocolate fireguard.",
            "Your timing is worse than a broken clock.",
            "You're like a human traffic jam.",
            "You have the fashion sense of a scarecrow.",
            "You're about as subtle as a brick through a window.",
            "Your social skills are like a fish out of water.",
            "You're like a human allergen.",
            "You have the enthusiasm of a sloth on sedatives.",
            "You're about as exciting as watching paint dry.",
            "Your creativity is more stale than week-old bread.",
            "You're like a human paperweight.",
            "You have the energy of a dead battery.",
            "You're about as sharp as a marble.",
            "Your ideas are flatter than roadkill.",
            "You're like a human snooze button.",
            "You have the grace of a brick in a china shop.",
            "You're about as useful as a glass hammer.",
            "Your personality is drier than toast.",
            "You're like a human headache.",
            "You have the charm of a root canal.",
            "You're about as interesting as watching grass grow.",
            "Your jokes are older than the pyramids.",
            "You're like a human speed limit.",
            "You have the appeal of expired milk.",
            "You're about as welcome as a skunk at a garden party.",
            "Your conversation skills are like a deflated balloon.",
            "You're like a human rain cloud.",
            "You have the subtlety of a freight train.",
            "You're about as smooth as sandpaper.",
            "Your dance moves look like you're being electrocuted.",
            "You're like a human mosquito.",
            "You have the fashion sense of a blind penguin.",
            "You're about as graceful as a bull in a china shop.",
            "Your singing voice sounds like a cat in a blender.",
            "You're like a human alarm clock that nobody wants to hear.",
            "You have the cooking skills of a house fire.",
            "You're about as coordinated as a three-legged cat.",
            "Your driving is scarier than a horror movie.",
            "You're like a human wet blanket.",
            "You have the organizational skills of a hurricane.",
            "You're about as reliable as a chocolate teapot.",
            "Your memory is worse than a goldfish with amnesia.",
            "You're like a human speed bump on the highway of life.",
            "You have the social awareness of a brick.",
            "You're about as helpful as a screen door on a spaceship.",
            "Your common sense is on permanent vacation.",
            "You're like a human typo.",
            "You have the grace of a drunk elephant.",
            "You're about as sharp as a bowling ball.",
            "Your ideas are more scattered than dandelion seeds.",
            "You're like a human puzzle with half the pieces missing.",
            "You have the attention span of a caffeinated squirrel.",
            "You're about as smooth as gravel.",
            "Your logic is more twisted than a tornado.",
            "You're like a human glitch in the matrix.",
            "You have the coordination of a drunk octopus.",
            "You're about as useful as a solar-powered flashlight.",
            "Your timing is worse than a broken stopwatch.",
            "You're like a human pothole.",
            "You have the grace of a falling piano.",
            "You're about as bright as a burnt-out lightbulb.",
            "Your conversation is more boring than watching cement dry.",
            "You're like a human speed limit in the fast lane.",
            "You have the charm of a rusty nail.",
            "You're about as welcome as a porcupine in a balloon factory.",
            "Your ideas are more stale than yesterday's donuts.",
            "You're like a human hiccup.",
            "You have the enthusiasm of a wet sock.",
            "You're about as exciting as tax paperwork.",
            "Your creativity is flatter than a pancake under a steamroller.",
            "You're like a human paper jam.",
            "You have the energy of a dying flashlight.",
            "You're about as sharp as a butter knife made of jello.",
            "Your wit is duller than dishwater.",
            "You're like a human commercial break.",
            "You have the grace of a falling refrigerator.",
            "You're about as useful as a fork in a soup kitchen.",
            "Your personality is drier than the Sahara desert.",
            "You're like a human Monday morning.",
            "You have the appeal of a dentist appointment.",
            "You're about as smooth as a cheese grater.",
            "Your jokes are more stale than month-old crackers."
        };

        // Dragon state for Room 3
        private bool _dragonDefeated = false;
        private bool _isAwaitingSayInput = false;
        private readonly StringBuilder _sayInput = new();
        private bool _showingSayPrompt = false;
        // Keep a reference to the original MainScreen to preserve its state
        private readonly MainScreen _originalMainScreen;
        
        // Random flavor text while walking
        private int _stepCount = 0;
        private readonly string[] _flavorTexts = {
            "You feel like someone is watching... but from inside your pancreas.",
            "The word \"lunch\" drifts through your head, uninvited.",
            "You step on something. It apologizes.",
            "Somewhere, a duck sneezes. You know this with certainty.",
            "Your left sock feels judgmental.",
            "A memory of purple flickers behind your eyes.",
            "Your shadow seems slightly out of sync.",
            "The universe hiccups. You felt that.",
            "A distant typewriter clicks, spelling your name backwards.",
            "Your teeth taste like Thursday.",
            "Something small scurries across your thoughts.",
            "The number 7 feels particularly smug today.",
            "You remember a door you've never seen.",
            "Your reflection winks first."
        };

        public IsometricScreen(GraphicsDevice graphicsDevice, SpriteFont font, Color themeBackground, Color themeForeground, string startingRoomName, MainScreen originalMainScreen, KeyboardState? previousKeyboardState = null)
        {
            _graphicsDevice = graphicsDevice;
            _font = font;
            _themeForeground = themeForeground;
            _themeBackground = themeBackground;
            _originalMainScreen = originalMainScreen;
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
                // Go back to MainScreen with preserved history if room not found
                _nextScreen = new MainScreen(_graphicsDevice, _font, _themeBackground, _themeForeground, 
                                           _originalMainScreen.GetHistory(), _originalMainScreen.GetCommandHistory());
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
                    // Create a new MainScreen with preserved history to avoid screen conflicts
                    var newMainScreen = new MainScreen(_graphicsDevice, _font, _themeBackground, _themeForeground, 
                                                     _originalMainScreen.GetHistory(), _originalMainScreen.GetCommandHistory(), ignoreFirstEscape: true);
                    newMainScreen.AddLogEntry("> You return to the terminal interface.");
                    _nextScreen = newMainScreen;
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
                            // Create a new MainScreen with preserved history and the log entry
                            var newMainScreen = new MainScreen(_graphicsDevice, _font, _themeBackground, _themeForeground, 
                                                             _originalMainScreen.GetHistory(), _originalMainScreen.GetCommandHistory());
                            newMainScreen.AddLogEntry("> LOG ENTRY 442A:");
                            newMainScreen.AddLogEntry("> Subject 442A attempted use of 'smelly pants' insult. Insufficient offense. Subject devoured.");
                            _nextScreen = newMainScreen;
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
                                // Endgame sequence - create new terminal with preserved history and victory messages
                                var newMainScreen = new MainScreen(_graphicsDevice, _font, _themeBackground, _themeForeground, 
                                                                 _originalMainScreen.GetHistory(), _originalMainScreen.GetCommandHistory());
                                newMainScreen.AddLogEntry("CONNECTION LOST.");
                                newMainScreen.AddLogEntry("TERMINAL CORRUPTION DETECTED.");
                                newMainScreen.AddLogEntry("Thank you for participating in System Glitch: Episode 0.");
                                newMainScreen.AddLogEntry("Thanks for playing.");
                                newMainScreen.AddLogEntry("Type `exit` to quit the simulation.");
                                _nextScreen = newMainScreen;
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
                bool playerMoved = false;

                _playerPosition.X += moveAmount.X;
                var nextPlayerBounds = new Rectangle((int)_playerPosition.X, (int)originalPosition.Y, (int)playerSize.X, (int)playerSize.Y);
                if (_collisionRects.Any(rect => rect.Intersects(nextPlayerBounds)))
                {
                    _playerPosition.X = originalPosition.X;
                }
                else
                {
                    playerMoved = true;
                }

                _playerPosition.Y += moveAmount.Y;
                nextPlayerBounds = new Rectangle((int)_playerPosition.X, (int)_playerPosition.Y, (int)playerSize.X, (int)playerSize.Y);
                if (_collisionRects.Any(rect => rect.Intersects(nextPlayerBounds)))
                {
                    _playerPosition.Y = originalPosition.Y;
                }
                else
                {
                    playerMoved = true;
                }

                // Track steps and occasionally show flavor text
                if (playerMoved)
                {
                    _stepCount++;
                    
                    // Show random flavor text much more rarely - roughly every 200-400 steps
                    if (_stepCount > 100 && _stepCount % 50 == 0 && _random.Next(0, 8) == 0)
                    {
                        var flavorText = _flavorTexts[_random.Next(_flavorTexts.Length)];
                        _interactionMessage = flavorText;
                        _messageTimer = 0;
                    }
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