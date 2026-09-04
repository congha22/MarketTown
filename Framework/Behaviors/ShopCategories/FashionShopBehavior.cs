using System.Linq;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Objects;
using MarketTown.Framework.Models;
using MarketTown.Framework.Services;
using MarketTown.Framework.UI.Panels;
using MarketTown.Framework.Integrations;

namespace MarketTown.Framework.Behaviors.ShopCategories
{
    /// <summary>
    /// Shop category behavior for a Fashion Boutique.
    ///
    /// Key effects:
    ///   - +25% sell price on clothing, hats, and boots.
    ///   - When a visitor has a clothing/hat/boot item in their cart they immediately
    ///     path to a free Fitting Booth slot (X=1, Y=1 of the furniture).
    ///   - They wait 3–5 seconds (per OnVisitorTick) then resume normal idle wandering.
    ///   - Departure / checkout logic is unaffected (IndoorVisitorService guards it).
    /// </summary>
    public class FashionShopBehavior : IShopCategoryBehavior
    {
        private readonly IMonitor _monitor;
        private readonly IndoorVisitorService _visitorService;
        private readonly IModHelper _helper;

        public FashionShopBehavior(IMonitor monitor, IndoorVisitorService visitorService, IModHelper helper)
        {
            _monitor = monitor;
            _visitorService = visitorService;
            _helper = helper;
        }

        // ── Identity ──────────────────────────────────────────────────────────

        public string CategoryName => "Fashion";
        public string DisplayName  => "Fashion Boutique";

        // ── Stat Hooks ────────────────────────────────────────────────────────

        public void ApplyShopBuffs(StoreCapacityScores scores, GameLocation location)
        {
            // Fashion stores don't change capacity — same as general.
        }

        public float GetPriceMultiplier(StardewValley.Object item)
        {
            // +25% for clothing/hats/boots; no bonus for other item types.
            return IsFashionItem(item) ? 1.25f : 1.0f;
        }

        // ── UI Hook ───────────────────────────────────────────────────────────

        public IShopMenuPanel GetMenuPanel(GameLocation location, StoreStatsService statsService, IndoorVisitorService visitorService)
        {
            return new FashionShopPanel(location, statsService, visitorService);
        }

        // ── Per-visitor tick: booth timer ─────────────────────────────────────

        public void OnVisitorTick(NPC npc, GameLocation location, IndoorVisitorService.VisitorData data, float deltaMs)
        {
            if (!data.IsInFittingBooth)
                return;

            data.FittingBoothTimer += deltaMs;

            if (data.FittingBoothTimer >= data.FittingBoothDuration)
            {
                // Done — leave the booth and return to normal idle wandering.
                data.IsInFittingBooth = false;
                data.FittingBoothTile = Vector2.Zero;
                data.FittingBoothTimer = 0f;

                // Call CAS API to try changing outfit if there are clothing items in the cart
                ICASApi casApi = _helper.ModRegistry.GetApi<ICASApi>("d5a1lamdtd.CASCreateAStardewie");
                if (casApi != null)
                {
                    bool changed = false;
                    foreach (var cartItem in data.ShoppingCart)
                    {
                        if (IsFashionItem(cartItem.Item))
                        {
                            if (casApi.TryChangeOutfit(npc, cartItem.Item.QualifiedItemId))
                            {
                                changed = true;
                            }
                        }
                    }

                    if (changed)
                    {
                        npc.doEmote(20); // Heart emote for a successful change
                        _monitor.Log($"{npc.Name} tried on and kept new clothes via CAS API.", LogLevel.Debug);
                    }
                }

                _monitor.Log($"{npc.Name} finished trying on clothes and exited the fitting booth.", LogLevel.Debug);
            }
        }

        // ── Wander Hook: send NPC to fitting booth ────────────────────────────

        public bool TryHandleCustomCustomerWander(NPC npc, GameLocation location, IndoorVisitorService.VisitorData data)
        {
            // ── Already in/heading to booth: hold position ────────────────────
            if (data.IsInFittingBooth || data.IsHeadingToBooth)
            {
                // Return true so IndoorVisitorService skips default random wander.
                return true;
            }

            // ── Check if they have NEW fashion items since their last booth visit ──────
            // Count how many fashion items are currently in the cart.
            int currentFashionCount = data.ShoppingCart.Count(ci => IsFashionItem(ci.Item));

            // Only go to the booth if they bought MORE fashion items since the last visit.
            // This prevents them looping back to the booth after they've already tried on
            // what they bought — they only go again when a new scan adds new items.
            if (currentFashionCount <= data.FashionItemsAtLastBoothEntry)
                return false; // Nothing new to try on.

            // ── Find a free booth ─────────────────────────────────────────────
            if (!_visitorService.TryReserveBoothTile(location, data, out Vector2 boothTile))
            {
                // All booths are occupied — wander normally and try again later.
                return false;
            }

            // ── Dispatch NPC toward the booth slot ────────────────────────────
            data.IsHeadingToBooth = true;
            data.FittingBoothTile = boothTile;

            NpcScheduleHelper.CleanNpc(npc);
            npc.controller = new StardewValley.Pathfinding.PathFindController(
                npc, location,
                new Point((int)boothTile.X, (int)boothTile.Y),
                -1,
                new StardewValley.Pathfinding.PathFindController.endBehavior((c, loc) => OnBoothArrived(c as NPC, data)));

            _monitor.Log($"{npc.Name} is heading to a fitting booth at tile {boothTile}.", LogLevel.Debug);
            return true;
        }

        // ── Private helpers ───────────────────────────────────────────────────

        /// <summary>Called by PathFindController when the NPC reaches the booth slot.</summary>
        private void OnBoothArrived(NPC npc, IndoorVisitorService.VisitorData data)
        {
            if (npc == null) return;

            // Snapshot the current fashion item count so we don't send them back
            // to the booth for the same purchase after they exit.
            data.FashionItemsAtLastBoothEntry = data.ShoppingCart.Count(ci => IsFashionItem(ci.Item));

            data.IsHeadingToBooth  = false;
            data.IsInFittingBooth  = true;
            data.FittingBoothTimer = 0f;
            // Random stay: 3 000 – 5 000 ms
            data.FittingBoothDuration = Game1.random.Next(3000, 5001);

            // Face up (into the booth)
            npc.faceDirection(0);
            npc.doEmote(16); // Exclamation — surprise/excitement

            _monitor.Log($"{npc.Name} entered fitting booth. Will stay ~{data.FittingBoothDuration / 1000f:0.0}s.", LogLevel.Debug);
        }

        /// <summary>Returns true for items a fashion boutique would want to try on.</summary>
        private static bool IsFashionItem(Item item) =>
            item is Clothing || item is Hat || item is Boots;
    }
}
