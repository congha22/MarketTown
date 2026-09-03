using StardewValley;
using MarketTown.Framework.Models;
using MarketTown.Framework.Services;
using MarketTown.Framework.UI.Panels;

namespace MarketTown.Framework.Behaviors.ShopCategories
{
    /// <summary>
    /// The fallback "General Store" behavior.
    /// Applies a flat 10% price bonus and otherwise defers all NPC
    /// movement to the default random-wander logic in IndoorVisitorService.
    /// </summary>
    public class GeneralStoreBehavior : IShopCategoryBehavior
    {
        public string CategoryName => "General";
        public string DisplayName => "General Store";

        public void ApplyShopBuffs(StoreCapacityScores scores, GameLocation location)
        {
            // General store has no special capacity bonuses — it's the baseline.
        }

        public float GetPriceMultiplier(StardewValley.Object item)
        {
            // +10% on everything.
            return 1.10f;
        }

        public void OnVisitorTick(NPC npc, GameLocation location, IndoorVisitorService.VisitorData data, float deltaMs)
        {
            // General store has no time-sensitive per-visitor behavior.
        }

        public bool TryHandleCustomCustomerWander(NPC npc, GameLocation location, IndoorVisitorService.VisitorData data)
        {
            // Return false so IndoorVisitorService falls back to its default random wander.
            return false;
        }

        public IShopMenuPanel GetMenuPanel(GameLocation location, StoreStatsService statsService, IndoorVisitorService visitorService)
        {
            return new GeneralStorePanel(location, statsService, visitorService);
        }
    }
}
