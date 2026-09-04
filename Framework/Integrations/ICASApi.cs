using StardewValley;
using System.Collections.Generic;

namespace MarketTown.Framework.Integrations
{
    public interface ICASApi
    {
        void TriggerNpcAction(NPC npc, string action, int? facingDirection = null);
        IEnumerable<NPC> GetCustomNPCs();
        bool TryChangeOutfit(NPC npc, string qualifiedItemId);
    }
}
