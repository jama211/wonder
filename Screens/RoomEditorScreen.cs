using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using WonderGame.Data;
using WonderGame.Core;

namespace WonderGame.Screens
{
    public class RoomEditorScreen : IScreen, ITextInputReceiver
    {
        private readonly GraphicsDevice _graphicsDevice;
        private readonly SpriteFont _font;
        private readonly Color _themeBackground;
        private readonly Color _themeForeground;
        private readonly string _roomName;
        
        private List<WorldObject> _worldObjects;
        private readonly List<RoomObject> _originalObjectData;

        private IScreen? _nextScreen;

        private WorldObject? _selectedObject;
        private List<WorldObject> _selectedGroup = new List<WorldObject>();

        private MouseState _previousMouseState;
        private KeyboardState _previousKeyboardState;

        private bool _isDragging;
        private bool _isResizing;
        private Vector2 _dragOffset;
        private Corner _activeResizeCorner;

        private Rectangle _saveButton;
        private Rectangle _revertButton;
        private bool _showConfirmDialog;
        private Rectangle _confirmYesButton;
        private Rectangle _confirmNoButton;
        
        // QOL: Property Inspector
        private Rectangle _inspectorPane;
        private string _editingProperty = "";
        private string _propertyValueBuffer = "";

        private const int GridSize = 10;

        public RoomEditorScreen(GraphicsDevice graphicsDevice, SpriteFont font, Color themeBackground, Color themeForeground, string roomName, KeyboardState? previousKeyboardState = null)
        {
            _graphicsDevice = graphicsDevice;
            _font = font;
            _themeBackground = themeBackground;
            _themeForeground = themeForeground;
            _roomName = roomName;
            _worldObjects = new List<WorldObject>();
            _previousKeyboardState = previousKeyboardState ?? Keyboard.GetState();

            var path = Path.Combine("Data", "Rooms", $"{_roomName}.json");
            if (File.Exists(path))
            {
                var jsonString = File.ReadAllText(path);
                var roomData = JsonSerializer.Deserialize<Room>(jsonString);
                if (roomData?.Objects != null)
                {
                    _originalObjectData = roomData.Objects.Select(o => o.Clone()).ToList(); // Deep copy for revert
                    foreach (var objData in roomData.Objects)
                    {
                        _worldObjects.Add(new WorldObject(objData, _font));
                    }
                }
                else
                {
                    _originalObjectData = new List<RoomObject>();
                }
            }
            else
            {
                _originalObjectData = new List<RoomObject>();
            }

            _saveButton = new Rectangle(_graphicsDevice.Viewport.Width - 120, _graphicsDevice.Viewport.Height - 60, 100, 40);
            _revertButton = new Rectangle(_graphicsDevice.Viewport.Width - 240, _graphicsDevice.Viewport.Height - 60, 100, 40);
            _inspectorPane = new Rectangle(10, 50, 220, 300);
        }

        public IScreen? GetNextScreen() => _nextScreen;

        public void Update(GameTime gameTime)
        {
            var mouseState = Mouse.GetState();
            var keyboardState = Keyboard.GetState();
            var mousePos = new Point(mouseState.X, mouseState.Y);

            if ((keyboardState.IsKeyDown(Keys.Escape) && _previousKeyboardState.IsKeyUp(Keys.Escape)) ||
                (keyboardState.IsKeyDown(Keys.P) && _previousKeyboardState.IsKeyUp(Keys.P)))
            {
                // Create a temporary MainScreen for the IsometricScreen constructor
                var tempMainScreen = new MainScreen(_graphicsDevice, _font, _themeBackground, _themeForeground);
                _nextScreen = new IsometricScreen(_graphicsDevice, _font, _themeBackground, _themeForeground, _roomName, tempMainScreen, keyboardState);
                return;
            }

            if (_showConfirmDialog)
            {
                HandleConfirmDialog(mouseState);
                _previousMouseState = mouseState;
                _previousKeyboardState = keyboardState;
                return;
            }
            
            if (_editingProperty != "")
            {
                // Text input is now handled by the ITextInputReceiver interface
                _previousKeyboardState = keyboardState;
                _previousMouseState = mouseState;
                return;
            }

            HandleButtons(mouseState);
            HandleSelection(mouseState, mousePos);
            HandleDragAndResize(mouseState, mousePos);
            HandleDuplication(keyboardState);

            _previousMouseState = mouseState;
            _previousKeyboardState = keyboardState;
        }

        private void HandleButtons(MouseState mouseState)
        {
            var mousePos = new Point(mouseState.X, mouseState.Y);
            if (mouseState.LeftButton == ButtonState.Pressed && _previousMouseState.LeftButton == ButtonState.Released)
            {
                if (_saveButton.Contains(mousePos))
                {
                    _showConfirmDialog = true;
                    var dialogWidth = 300;
                    var dialogHeight = 100;
                    var dialogX = (_graphicsDevice.Viewport.Width - dialogWidth) / 2;
                    var dialogY = (_graphicsDevice.Viewport.Height - dialogHeight) / 2;
                    _confirmYesButton = new Rectangle(dialogX + 40, dialogY + 50, 80, 30);
                    _confirmNoButton = new Rectangle(dialogX + 180, dialogY + 50, 80, 30);
                }
                else if (_revertButton.Contains(mousePos))
                {
                    RevertChanges();
                }
            }
        }

        private void HandleConfirmDialog(MouseState mouseState)
        {
            var mousePos = new Point(mouseState.X, mouseState.Y);
            if (mouseState.LeftButton == ButtonState.Pressed && _previousMouseState.LeftButton == ButtonState.Released)
            {
                if (_confirmYesButton.Contains(mousePos))
                {
                    SaveChanges();
                    _showConfirmDialog = false;
                }
                else if (_confirmNoButton.Contains(mousePos))
                {
                    _showConfirmDialog = false;
                }
            }
        }

        private void HandleSelection(MouseState mouseState, Point mousePos)
        {
            if (mouseState.LeftButton == ButtonState.Pressed && _previousMouseState.LeftButton == ButtonState.Released)
            {
                // 1. Handle inspector click first
                if (_inspectorPane.Contains(mousePos) && _selectedObject != null)
                {
                    int yOffset = _inspectorPane.Y + 30;
                    CheckPropertyClick(mousePos, yOffset, "Name");
                    CheckPropertyClick(mousePos, yOffset + 20, "X");
                    CheckPropertyClick(mousePos, yOffset + 40, "Y");
                    CheckPropertyClick(mousePos, yOffset + 60, "ScaleX");
                    CheckPropertyClick(mousePos, yOffset + 80, "ScaleY");
                    CheckPropertyClick(mousePos, yOffset + 100, "Description");
                    CheckPropertyClick(mousePos, yOffset + 120, "DoorTo");
                    CheckPropertyClick(mousePos, yOffset + 140, "GroupId");
                    return; // Prevent deselection
                }

                // 2. Check for resize handle clicks on the currently selected object
                if (_selectedObject != null)
                {
                    var corner = GetResizeHandleForPoint(mousePos);
                    if (corner != Corner.None)
                    {
                        _isResizing = true;
                        _isDragging = false;
                        _activeResizeCorner = corner;
                        return; // Click handled, exit
                    }
                }

                // 3. Check for object selection/drag clicks
                foreach (var obj in _worldObjects.OrderByDescending(o => o.BoundingBox.Width * o.BoundingBox.Height))
                {
                    if (GetGroupBoundingBox(obj).Contains(mousePos))
                    {
                        // We clicked an object, select it and prepare for dragging.
                        _selectedObject = obj;
                        _selectedGroup.Clear();
                        if (!string.IsNullOrEmpty(obj.Data.GroupId))
                        {
                            _selectedGroup = _worldObjects.Where(o => o.Data.GroupId == obj.Data.GroupId).ToList();
                        }
                        else
                        {
                            _selectedGroup.Add(obj);
                        }
                        
                        _isDragging = true;
                        _isResizing = false;
                        var groupCenter = GetGroupBoundingBox(_selectedObject).Center;
                        _dragOffset = new Vector2(mousePos.X - groupCenter.X, mousePos.Y - groupCenter.Y);
                        return; // Click handled, exit
                    }
                }
                
                // 4. If we reach here, the click was on empty space. Deselect.
                _selectedObject = null;
                _selectedGroup.Clear();
            }
            
            if (mouseState.LeftButton == ButtonState.Released)
            {
                _isDragging = false;
                _isResizing = false;
                _activeResizeCorner = Corner.None;
            }
        }

        private void CheckPropertyClick(Point mousePos, int y, string propertyName)
        {
            var propRect = new Rectangle(_inspectorPane.X + 70, y, _inspectorPane.Width - 80, 20);
            if (propRect.Contains(mousePos))
            {
                _editingProperty = propertyName;
                _propertyValueBuffer = GetPropertyValue(propertyName) ?? "";
            }
        }

        private void HandleDragAndResize(MouseState mouseState, Point mousePos)
        {
            if (_selectedObject == null) return;

            if (_isDragging)
            {
                var targetCenter = new Vector2(mousePos.X - _dragOffset.X, mousePos.Y - _dragOffset.Y);

                // QOL: Grid Snapping
                targetCenter.X = (int)Math.Round(targetCenter.X / GridSize) * GridSize;
                targetCenter.Y = (int)Math.Round(targetCenter.Y / GridSize) * GridSize;
                
                var groupBBox = GetGroupBoundingBox(_selectedObject);
                var movementDelta = targetCenter - groupBBox.Center.ToVector2();

                foreach (var member in _selectedGroup)
                {
                    member.Data.X += (int)movementDelta.X;
                    member.Data.Y += (int)movementDelta.Y;
                    member.UpdateBoundingBox();
                }
            }
            else if (_isResizing && _selectedObject != null)
            {
                HandleResize(mousePos);
            }
        }

        private void HandleResize(Point mousePos)
        {
            if (_selectedObject == null || _activeResizeCorner == Corner.None) return;

            var groupBBox = GetGroupBoundingBox(_selectedObject);
            var members = _selectedGroup.Count > 0 ? _selectedGroup : new List<WorldObject> { _selectedObject };
            
            // Calculate the initial total size of the group from their actual bounding boxes
            Vector2 groupInitialSize = new Vector2(groupBBox.Width, groupBBox.Height);
            if (groupInitialSize.X == 0 || groupInitialSize.Y == 0) return;

            int snappedMouseX = (int)Math.Round(mousePos.X / (float)GridSize) * GridSize;
            int snappedMouseY = (int)Math.Round(mousePos.Y / (float)GridSize) * GridSize;

            float newGroupX = groupBBox.X;
            float newGroupY = groupBBox.Y;
            float newGroupWidth = groupBBox.Width;
            float newGroupHeight = groupBBox.Height;

            switch (_activeResizeCorner)
            {
                case Corner.BottomRight:
                    newGroupWidth = Math.Max(GridSize, snappedMouseX - groupBBox.Left);
                    newGroupHeight = Math.Max(GridSize, snappedMouseY - groupBBox.Top);
                    break;
                case Corner.BottomLeft:
                    newGroupWidth = Math.Max(GridSize, groupBBox.Right - snappedMouseX);
                    newGroupHeight = Math.Max(GridSize, snappedMouseY - groupBBox.Top);
                    newGroupX = groupBBox.Right - newGroupWidth;
                    break;
                case Corner.TopRight:
                    newGroupWidth = Math.Max(GridSize, snappedMouseX - groupBBox.Left);
                    newGroupHeight = Math.Max(GridSize, groupBBox.Bottom - snappedMouseY);
                    newGroupY = groupBBox.Bottom - newGroupHeight;
                    break;
                case Corner.TopLeft:
                    newGroupWidth = Math.Max(GridSize, groupBBox.Right - snappedMouseX);
                    newGroupHeight = Math.Max(GridSize, groupBBox.Bottom - snappedMouseY);
                    newGroupX = groupBBox.Right - newGroupWidth;
                    newGroupY = groupBBox.Bottom - newGroupHeight;
                    break;
            }
            
            // Calculate scale based on the change from the initial bounding box size
            float scaleX = newGroupWidth / groupInitialSize.X;
            float scaleY = newGroupHeight / groupInitialSize.Y;

            foreach (var member in members)
            {
                // Position relative to the group's top-left corner
                float relativeX = member.BoundingBox.X - groupBBox.X;
                float relativeY = member.BoundingBox.Y - groupBBox.Y;

                member.Data.X = (int)(newGroupX + relativeX * scaleX);
                member.Data.Y = (int)(newGroupY + relativeY * scaleY);

                // Apply the scale change to the existing scale
                member.Data.ScaleX *= scaleX;
                member.Data.ScaleY *= scaleY;
                
                member.UpdateBoundingBox();
            }
        }

        private void HandleDuplication(KeyboardState keyboardState)
        {
            if (_selectedObject != null && keyboardState.IsKeyDown(Keys.D) && _previousKeyboardState.IsKeyUp(Keys.D))
            {
                var newObjectData = _selectedObject.Data.Clone();
                newObjectData.X += 20; // Offset the new object
                var newWorldObject = new WorldObject(newObjectData, _font);
                _worldObjects.Add(newWorldObject);

                _selectedObject = newWorldObject; // Select the new object
            }
        }


        private void SaveChanges()
        {
            var roomData = new Room { Objects = _worldObjects.Select(o => o.Data).ToList() };
            var options = new JsonSerializerOptions { WriteIndented = true, };
            var jsonString = JsonSerializer.Serialize(roomData, options);
            var path = Path.Combine("Data", "Rooms", $"{_roomName}.json");
            File.WriteAllText(path, jsonString);
        }

        private void RevertChanges()
        {
            _worldObjects.Clear();
            foreach (var objData in _originalObjectData)
            {
                _worldObjects.Add(new WorldObject(objData.Clone(), _font));
            }
            _selectedObject = null;
            _selectedGroup.Clear();
        }

        public void Draw(GameTime gameTime)
        {
            var spriteBatch = Core.Global.SpriteBatch;
            if (spriteBatch == null) return;
            
            // Draw grid
            for (int x = 0; x < _graphicsDevice.Viewport.Width; x += GridSize)
            {
                DrawLine(spriteBatch, new Vector2(x, 0), new Vector2(x, _graphicsDevice.Viewport.Height), Color.DarkSlateGray);
            }
            for (int y = 0; y < _graphicsDevice.Viewport.Height; y += GridSize)
            {
                DrawLine(spriteBatch, new Vector2(0, y), new Vector2(_graphicsDevice.Viewport.Width, y), Color.DarkSlateGray);
            }

            foreach (var obj in _worldObjects)
            {
                obj.Draw(spriteBatch, _themeForeground);
            }

            if (_selectedObject != null)
            {
                var bbox = GetGroupBoundingBox(_selectedObject);
                DrawRectangle(spriteBatch, bbox, Color.Yellow, 2);
                // Draw resize handles
                const int handleSize = 8;
                DrawRectangle(spriteBatch, new Rectangle(bbox.Left - handleSize / 2, bbox.Top - handleSize / 2, handleSize, handleSize), Color.Yellow);
                DrawRectangle(spriteBatch, new Rectangle(bbox.Right - handleSize / 2, bbox.Top - handleSize / 2, handleSize, handleSize), Color.Yellow);
                DrawRectangle(spriteBatch, new Rectangle(bbox.Left - handleSize / 2, bbox.Bottom - handleSize / 2, handleSize, handleSize), Color.Yellow);
                DrawRectangle(spriteBatch, new Rectangle(bbox.Right - handleSize / 2, bbox.Bottom - handleSize / 2, handleSize, handleSize), Color.Yellow);
            }

            DrawUI(spriteBatch);

            if (_showConfirmDialog)
            {
                DrawConfirmDialog(spriteBatch);
            }
        }

        private void DrawUI(SpriteBatch spriteBatch)
        {
            // Draw buttons
            DrawButton(spriteBatch, _saveButton, "Save");
            DrawButton(spriteBatch, _revertButton, "Revert");

            // Draw instructions
            var instructions = "[P/ESC] Return to Game | [D] Duplicate Selected";
            spriteBatch.DrawString(_font, instructions, new Vector2(20, 20), Color.LightGray);
            
            // Draw Inspector
            DrawInspector(spriteBatch);
        }

        private void DrawInspector(SpriteBatch spriteBatch)
        {
            // Pane background
            var paneColor = new Color(Color.Black, 0.8f);
            spriteBatch.Draw(Core.Global.Pixel, _inspectorPane, paneColor);
            DrawRectangle(spriteBatch, _inspectorPane, Color.LightGray); // Border
            spriteBatch.DrawString(_font, "Property Inspector", new Vector2(_inspectorPane.X + 10, _inspectorPane.Y + 10), Color.White);

            if (_selectedObject != null)
            {
                int yOffset = _inspectorPane.Y + 30;
                DrawProperty(spriteBatch, "Name", GetPropertyValue("Name"), yOffset);
                DrawProperty(spriteBatch, "X", GetPropertyValue("X"), yOffset + 20);
                DrawProperty(spriteBatch, "Y", GetPropertyValue("Y"), yOffset + 40);
                DrawProperty(spriteBatch, "ScaleX", GetPropertyValue("ScaleX"), yOffset + 60);
                DrawProperty(spriteBatch, "ScaleY", GetPropertyValue("ScaleY"), yOffset + 80);
                DrawProperty(spriteBatch, "Description", GetPropertyValue("Description"), yOffset + 100);
                DrawProperty(spriteBatch, "DoorTo", GetPropertyValue("DoorTo"), yOffset + 120);
                DrawProperty(spriteBatch, "GroupId", GetPropertyValue("GroupId"), yOffset + 140);
            }
        }

        private string? GetPropertyValue(string propName)
        {
            if (_selectedObject == null) return null;
            return propName switch
            {
                "Name" => _selectedObject.Data.Name,
                "X" => _selectedObject.Data.X.ToString(),
                "Y" => _selectedObject.Data.Y.ToString(),
                "ScaleX" => _selectedObject.Data.ScaleX.ToString("F2"),
                "ScaleY" => _selectedObject.Data.ScaleY.ToString("F2"),
                "Description" => _selectedObject.Data.Description,
                "DoorTo" => _selectedObject.Data.DoorTo,
                "GroupId" => _selectedObject.Data.GroupId,
                _ => null
            };
        }


        private void DrawProperty(SpriteBatch spriteBatch, string name, string? value, int y)
        {
            value ??= "null";
            var color = Color.White;

            if (_editingProperty == name)
            {
                value = _propertyValueBuffer + (DateTime.Now.Millisecond / 500 % 2 == 0 ? "_" : "");
                color = Color.Yellow;
            }

            spriteBatch.DrawString(_font, $"{name}:", new Vector2(_inspectorPane.X + 10, y), Color.LightGray);
            spriteBatch.DrawString(_font, value, new Vector2(_inspectorPane.X + 110, y), color);
        }


        private void DrawButton(SpriteBatch spriteBatch, Rectangle bounds, string text)
        {
            var bgColor = bounds.Contains(Mouse.GetState().Position) ? Color.DarkGreen : Color.SlateGray;
            spriteBatch.Draw(Core.Global.Pixel, bounds, bgColor);
            DrawRectangle(spriteBatch, bounds, Color.White);
            var textSize = _font.MeasureString(text);
            var textPos = new Vector2(bounds.X + (bounds.Width - textSize.X) / 2, bounds.Y + (bounds.Height - textSize.Y) / 2);
            spriteBatch.DrawString(_font, text, textPos, Color.White);
        }

        private void DrawConfirmDialog(SpriteBatch spriteBatch)
        {
            var dialogWidth = 300;
            var dialogHeight = 100;
            var dialogX = (_graphicsDevice.Viewport.Width - dialogWidth) / 2;
            var dialogY = (_graphicsDevice.Viewport.Height - dialogHeight) / 2;
            var dialogRect = new Rectangle(dialogX, dialogY, dialogWidth, dialogHeight);

            spriteBatch.Draw(Core.Global.Pixel, dialogRect, new Color(Color.Black, 0.9f));
            DrawRectangle(spriteBatch, dialogRect, Color.White);
            spriteBatch.DrawString(_font, "Save changes?", new Vector2(dialogX + 20, dialogY + 20), Color.White);
            DrawButton(spriteBatch, _confirmYesButton, "Yes");
            DrawButton(spriteBatch, _confirmNoButton, "No");
        }

        private void DrawLine(SpriteBatch spriteBatch, Vector2 start, Vector2 end, Color color, int thickness = 1)
        {
            Vector2 edge = end - start;
            float angle = (float)Math.Atan2(edge.Y, edge.X);
            spriteBatch.Draw(Core.Global.Pixel, new Rectangle((int)start.X, (int)start.Y, (int)edge.Length(), thickness), null, color, angle, new Vector2(0, 0.5f), SpriteEffects.None, 0);
        }

        private void DrawRectangle(SpriteBatch spriteBatch, Rectangle rect, Color color, int thickness = 1)
        {
            DrawLine(spriteBatch, new Vector2(rect.X, rect.Y), new Vector2(rect.Right, rect.Y), color, thickness);
            DrawLine(spriteBatch, new Vector2(rect.X, rect.Y), new Vector2(rect.X, rect.Bottom), color, thickness);
            DrawLine(spriteBatch, new Vector2(rect.Right, rect.Y), new Vector2(rect.Right, rect.Bottom), color, thickness);
            DrawLine(spriteBatch, new Vector2(rect.X, rect.Bottom), new Vector2(rect.Right, rect.Bottom), color, thickness);
        }

        private Rectangle GetGroupBoundingBox(WorldObject obj)
        {
            if (string.IsNullOrEmpty(obj.Data.GroupId))
            {
                return obj.BoundingBox;
            }

            var groupMembers = _worldObjects.Where(o => o.Data.GroupId == obj.Data.GroupId).ToList();
            if (!groupMembers.Any()) return obj.BoundingBox;

            var firstBox = groupMembers.First().BoundingBox;
            var minX = firstBox.Left;
            var minY = firstBox.Top;
            var maxX = firstBox.Right;
            var maxY = firstBox.Bottom;

            foreach (var member in groupMembers.Skip(1))
            {
                minX = Math.Min(minX, member.BoundingBox.Left);
                minY = Math.Min(minY, member.BoundingBox.Top);
                maxX = Math.Max(maxX, member.BoundingBox.Right);
                maxY = Math.Max(maxY, member.BoundingBox.Bottom);
            }
            return new Rectangle(minX, minY, maxX - minX, maxY - minY);
        }
        
        private enum Corner { None, TopLeft, TopRight, BottomLeft, BottomRight }

        private Corner GetResizeHandleForPoint(Point point)
        {
            if (_selectedObject == null) return Corner.None;
            var bbox = GetGroupBoundingBox(_selectedObject);
            const int handleHitboxSize = 16;

            if (new Rectangle(bbox.Left - handleHitboxSize / 2, bbox.Top - handleHitboxSize / 2, handleHitboxSize, handleHitboxSize).Contains(point)) return Corner.TopLeft;
            if (new Rectangle(bbox.Right - handleHitboxSize / 2, bbox.Top - handleHitboxSize / 2, handleHitboxSize, handleHitboxSize).Contains(point)) return Corner.TopRight;
            if (new Rectangle(bbox.Left - handleHitboxSize / 2, bbox.Bottom - handleHitboxSize / 2, handleHitboxSize, handleHitboxSize).Contains(point)) return Corner.BottomLeft;
            if (new Rectangle(bbox.Right - handleHitboxSize / 2, bbox.Bottom - handleHitboxSize / 2, handleHitboxSize, handleHitboxSize).Contains(point)) return Corner.BottomRight;

            return Corner.None;
        }

        public void OnTextInput(char character)
        {
            if (_editingProperty != "")
            {
                _propertyValueBuffer += character;
            }
        }

        public void OnBackspace()
        {
            if (_editingProperty != "" && _propertyValueBuffer.Length > 0)
            {
                _propertyValueBuffer = _propertyValueBuffer.Substring(0, _propertyValueBuffer.Length - 1);
            }
        }

        public void OnEnter()
        {
            if (_editingProperty != "")
            {
                ApplyPropertyValue(_editingProperty, _propertyValueBuffer);
                _editingProperty = "";
                _propertyValueBuffer = "";
            }
        }

        private void ApplyPropertyValue(string propertyName, string value)
        {
            if (_selectedObject == null) return;

            try
            {
                switch (propertyName)
                {
                    case "Name": _selectedObject.Data.Name = value; break;
                    case "X": _selectedObject.Data.X = int.Parse(value); break;
                    case "Y": _selectedObject.Data.Y = int.Parse(value); break;
                    case "ScaleX": _selectedObject.Data.ScaleX = float.Parse(value); break;
                    case "ScaleY": _selectedObject.Data.ScaleY = float.Parse(value); break;
                    case "Description": _selectedObject.Data.Description = value; break;
                    case "DoorTo": _selectedObject.Data.DoorTo = value == "null" ? null : value; break;
                    case "GroupId": _selectedObject.Data.GroupId = value == "null" ? null : value; break;
                }
                _selectedObject.UpdateBoundingBox();
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to apply property value: {e.Message}");
            }
        }
    }
} 