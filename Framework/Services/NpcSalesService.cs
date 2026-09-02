using System;
using System.Collections.Generic;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Objects;
using MarketTown.Framework.Config;
using MarketTown.Framework.Models;

namespace MarketTown.Framework.Services
{
    /// <summary>Handles the logic for NPCs buying items from the player's tables or mannequins.</summary>
    public class NpcSalesService
    {
        private readonly IMonitor _monitor;
        private readonly ModConfig _config;
        private readonly TableRestockService _restockService;
        private readonly SalesTrackingService _salesTrackingService;
        private readonly StoreStatsService _storeStatsService;
        private readonly IndoorVisitorService _indoorVisitorService;

        public NpcSalesService(IMonitor monitor, ModConfig config, TableRestockService restockService, SalesTrackingService salesTrackingService, StoreStatsService storeStatsService, IndoorVisitorService indoorVisitorService)
        {
            _monitor = monitor;
            _config = config;
            _restockService = restockService;
            _salesTrackingService = salesTrackingService;
            _storeStatsService = storeStatsService;
            _indoorVisitorService = indoorVisitorService;
        }

        /// <summary>
        /// Evaluates the NPC's gift taste for the item and processes the purchase if the random chance roll succeeds.
        /// </summary>
        /// <returns>True if the item was bought, false otherwise.</returns>
        public bool TryProcessPurchase(NPC npc, StardewValley.Object targetObject, Item evaluatedItem, int taste)
        {
            float chanceModifier = taste switch
            {
                NPC.gift_taste_love => 0.20f,
                NPC.gift_taste_like => 0.10f,
                NPC.gift_taste_dislike => -0.10f,
                NPC.gift_taste_hate => -0.20f,
                _ => 0.0f
            };

            float finalChance = _config.BaseBuyChance + chanceModifier;

            if (Game1.random.NextDouble() <= finalChance)
            {
                float priceModifier = taste switch
                {
                    NPC.gift_taste_love => 1.2f,
                    NPC.gift_taste_like => 1.1f,
                    NPC.gift_taste_dislike => 0.9f,
                    NPC.gift_taste_hate => 0.8f,
                    _ => 1.0f
                };

                int basePrice = evaluatedItem.sellToStorePrice(-1L);
                if (basePrice <= 0) basePrice = 1; // Fallback for 0-value items

                float qualityModifier = evaluatedItem.Quality switch
                {
                    1 => 1.05f, // Silver
                    2 => 1.15f, // Gold
                    4 => 1.30f, // Iridium
                    _ => 1.0f   // Regular/None
                };

                int sellPrice = (int)(basePrice * _config.PriceMultiplier * priceModifier * qualityModifier);

                // Determine who gets the money (handle separate wallets in multiplayer)
                Farmer seller = Game1.player;
                if (Game1.player.team.useSeparateWallets.Value && targetObject.owner.Value != 0)
                {
                    var owner = Game1.GetPlayer(targetObject.owner.Value);
                    if (owner != null)
                    {
                        seller = owner;
                    }
                    else
                    {
                        seller = Game1.MasterPlayer;
                    }
                }

                // Check if this is an indoor customer who should put it in their cart instead
                bool deferred = _indoorVisitorService.TryAddToCart(npc, evaluatedItem, sellPrice, taste, seller);

                if (!deferred)
                {
                    seller.Money += sellPrice;
                    
                    // Track shipping stats
                    seller.shippedBasic(evaluatedItem.ItemId, 1);
                    seller.stats.ItemsShipped += 1;
                    Game1.stats.checkForShippingAchievements();

                    _salesTrackingService.RecordSale(npc, evaluatedItem, sellPrice, targetObject.Location, taste);
                    _storeStatsService.RecordSale(targetObject.Location, sellPrice);

                    Game1.playSound("purchase");
                    Game1.chatBox.addInfoMessage($"Sold {evaluatedItem.DisplayName} to {npc.displayName ?? npc.Name} for {sellPrice}g with base of {basePrice}");
                }
                else
                {
                    _monitor.Log($"{npc.Name} added '{evaluatedItem.DisplayName}' to their shopping cart.", LogLevel.Debug);
                }

                // Remove item from target
                if (targetObject is Furniture f)
                {
                    f.heldObject.Value = null;
                }
                else if (targetObject is Mannequin mannequin)
                {
                    if (mannequin.hat.Value == evaluatedItem) mannequin.hat.Value = null;
                    else if (mannequin.shirt.Value == evaluatedItem) mannequin.shirt.Value = null;
                    else if (mannequin.pants.Value == evaluatedItem) mannequin.pants.Value = null;
                    else if (mannequin.boots.Value == evaluatedItem) mannequin.boots.Value = null;
                }

                _restockService.OnItemSold(targetObject, evaluatedItem);
                _monitor.Log($"{npc.Name} bought '{evaluatedItem.DisplayName}' for {sellPrice}g (Taste: {taste}, Chance: {finalChance:P0})", LogLevel.Debug);
                return true;
            }

            _monitor.Log($"{npc.Name} evaluated '{evaluatedItem.DisplayName}' (Taste: {taste}) but decided not to buy (Chance: {finalChance:P0}).", LogLevel.Debug);
            return false;
        }

        public void ProcessDeferredPurchases(NPC npc, List<CartItem> cart, GameLocation location)
        {
            if (cart == null || cart.Count == 0) return;

            int totalValue = 0;
            foreach (var item in cart)
            {
                item.Seller.Money += item.Price;
                item.Seller.shippedBasic(item.Item.ItemId, 1);
                item.Seller.stats.ItemsShipped += 1;

                _salesTrackingService.RecordSale(npc, item.Item, item.Price, location, item.Taste);
                _storeStatsService.RecordSale(location, item.Price);

                totalValue += item.Price;
            }

            Game1.stats.checkForShippingAchievements();
            Game1.playSound("purchase");
            Game1.chatBox.addInfoMessage($"Sold {cart.Count} items to {npc.displayName ?? npc.Name} for {totalValue}g total.");
            
            _monitor.Log($"{npc.Name} checked out with {cart.Count} items for {totalValue}g.", LogLevel.Info);
        }
    }
}
