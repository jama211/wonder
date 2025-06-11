using System.Collections.Generic;
using WonderGame.Data;

namespace WonderGame.Core
{
    public class Room
    {
        public List<RoomObject> Objects { get; set; } = new List<RoomObject>();
    }
} 