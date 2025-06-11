using System;

namespace WonderGame.Data
{
    public class RoomObject
    {
        public string Name { get; set; } = "";
        public int X { get; set; }
        public int Y { get; set; }
        public float ScaleX { get; set; } = 1.0f;
        public float ScaleY { get; set; } = 1.0f;
        public string? Description { get; set; }
        public string? DoorTo { get; set; }
        public string? GroupId { get; set; }
        
        public RoomObject Clone()
        {
            return (RoomObject)this.MemberwiseClone();
        }
    }
} 