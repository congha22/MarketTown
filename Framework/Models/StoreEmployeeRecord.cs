using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace MarketTown.Framework.Models
{
    public class StoreEmployeeRecord
    {
        // Dictionary mapping string representation of TileLocation (e.g. "X,Y") to the NPC's Name
        public Dictionary<string, string> HiredNPCs { get; set; } = new Dictionary<string, string>();
    }
}
