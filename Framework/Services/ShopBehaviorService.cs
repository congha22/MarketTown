using System.Collections.Generic;
using StardewModdingAPI;
using StardewValley;
using MarketTown.Framework.Behaviors.ShopCategories;
using MarketTown.Framework.Services;

namespace MarketTown.Framework.Services
{
    /// <summary>
    /// Central registry for all shop category behaviors.
    /// Register new themes here (or let future systems register them dynamically).
    /// Exposes <see cref="GetBehaviorForLocation"/> to retrieve the correct behavior
    /// for any active store location based on its saved <c>ShopTheme</c>.
    /// </summary>
    public class ShopBehaviorService
    {
        private readonly IMonitor _monitor;
        private readonly StoreStatsService _storeStatsService;
        private readonly Dictionary<string, IShopCategoryBehavior> _behaviors = new();

        private readonly IShopCategoryBehavior _fallback;

        public ShopBehaviorService(IMonitor monitor, StoreStatsService storeStatsService)
        {
            _monitor = monitor;
            _storeStatsService = storeStatsService;

            // Register the built-in default behavior.
            var general = new GeneralStoreBehavior();
            _fallback = general;
            Register(general);
        }

        // ── Registration ──────────────────────────────────────────────────────

        /// <summary>Register a new shop category behavior. Call this from ModEntry (or a future API).</summary>
        public void Register(IShopCategoryBehavior behavior)
        {
            if (_behaviors.ContainsKey(behavior.CategoryName))
            {
                _monitor.Log($"[ShopBehaviorService] Overwriting existing behavior for category '{behavior.CategoryName}'.", LogLevel.Warn);
            }

            _behaviors[behavior.CategoryName] = behavior;
            _monitor.Log($"[ShopBehaviorService] Registered shop behavior: '{behavior.DisplayName}' ({behavior.CategoryName}).", LogLevel.Trace);
        }

        // ── Lookup ────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the <see cref="IShopCategoryBehavior"/> for the given location,
        /// based on the <c>ShopTheme</c> stored in its <c>StoreStatRecord</c>.
        /// Falls back to the <see cref="GeneralStoreBehavior"/> if not found.
        /// </summary>
        public IShopCategoryBehavior GetBehaviorForLocation(GameLocation location)
        {
            if (location == null) return _fallback;

            string theme = "General";
            if (_storeStatsService.StoreStats.TryGetValue(location.NameOrUniqueName, out var record))
            {
                theme = record.ShopTheme ?? "General";
            }

            if (_behaviors.TryGetValue(theme, out var behavior))
            {
                return behavior;
            }

            _monitor.Log($"[ShopBehaviorService] Unknown shop theme '{theme}' for '{location.NameOrUniqueName}'. Using General fallback.", LogLevel.Warn);
            return _fallback;
        }

        /// <summary>Returns every registered behavior, used to populate theme dropdowns.</summary>
        public IReadOnlyDictionary<string, IShopCategoryBehavior> AllBehaviors => _behaviors;

        // ── Persistence Helper ────────────────────────────────────────────────

        /// <summary>Saves the chosen theme for a location.</summary>
        public void SetTheme(GameLocation location, string categoryName)
        {
            if (location == null) return;

            if (!_behaviors.ContainsKey(categoryName))
            {
                _monitor.Log($"[ShopBehaviorService] Tried to set unknown theme '{categoryName}'. Ignoring.", LogLevel.Warn);
                return;
            }

            _storeStatsService.UpdateShopTheme(location, categoryName);
            _monitor.Log($"[ShopBehaviorService] Set theme '{categoryName}' for '{location.NameOrUniqueName}'.", LogLevel.Info);
        }
    }
}
