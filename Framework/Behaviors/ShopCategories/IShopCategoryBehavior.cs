using StardewValley;
using MarketTown.Framework.Models;
using MarketTown.Framework.Services;
using MarketTown.Framework.UI.Panels;

namespace MarketTown.Framework.Behaviors.ShopCategories
{
    /// <summary>
    /// Defines the contract for a pluggable shop category behavior.
    /// Create a new class implementing this interface to add a new shop theme.
    /// The system will automatically discover and use it via ShopBehaviorService.
    /// </summary>
    public interface IShopCategoryBehavior
    {
        // ── Identity ──────────────────────────────────────────────────────────

        /// <summary>The unique key used to identify this theme (e.g. "General", "Fashion").</summary>
        string CategoryName { get; }

        /// <summary>Human-readable display name shown in the Store Manager menu dropdown.</summary>
        string DisplayName { get; }

        // ── Stat Hooks ────────────────────────────────────────────────────────

        /// <summary>
        /// Called during capacity score calculation. Implementations can boost or reduce
        /// scores to give their theme a unique capacity feel.
        /// </summary>
        void ApplyShopBuffs(StoreCapacityScores scores, GameLocation location);

        /// <summary>
        /// Returns a flat price multiplier for an item sold in this store.
        /// Return 1.0f for no change, 1.2f for +20%, etc.
        /// </summary>
        float GetPriceMultiplier(StardewValley.Object item);

        // ── NPC Behavior Hook ─────────────────────────────────────────────────

        /// <summary>
        /// Called every ~333ms per active visitor (staggered, once per 20-tick cycle),
        /// regardless of whether the NPC is moving or idle.
        /// Use this to tick timers (e.g. booth wait) that must advance continuously.
        /// </summary>
        void OnVisitorTick(NPC npc, GameLocation location, IndoorVisitorService.VisitorData data, float deltaMs);

        /// <summary>
        /// Called when a visiting NPC has been idle long enough to move.
        /// Return <c>true</c> if this behavior handled the movement (e.g. walked the
        /// NPC to a changing room). Return <c>false</c> to fall back to default random
        /// wandering in IndoorVisitorService.
        /// </summary>
        bool TryHandleCustomCustomerWander(NPC npc, GameLocation location, IndoorVisitorService.VisitorData data);

        // ── UI Hook ───────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the UI panel that should be rendered in the lower-left section of the
        /// Store Manager menu when this theme is active.
        /// </summary>
        IShopMenuPanel GetMenuPanel(GameLocation location, StoreStatsService statsService, IndoorVisitorService visitorService);
    }
}
