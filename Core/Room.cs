using System.Collections.Generic;

namespace WonderGame.Core
{
    public class Room
    {
        public List<RoomObject> Objects { get; set; } = new List<RoomObject>();
    }

    public class RoomObject
    {
        public string Name { get; set; } = "";
        public float X { get; set; }
        public float Y { get; set; }
        public float ScaleX { get; set; } = 1.0f;
        public float ScaleY { get; set; } = 1.0f;
        public string? Description { get; set; }
        public string? DoorTo { get; set; }
    }
} 