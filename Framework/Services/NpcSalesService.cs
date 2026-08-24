using System;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Objects;
using MarketTown.Framework.Config;

namespace MarketTown.Framework.Services
{
    /// <summary>Handles the logic for NPCs buying items from the player's tables or mannequins.</summary>
    public class NpcSalesService
    {
        private readonly IMonitor _monitor;
        private readonly ModConfig _config;
        private readonly TableRestockService _restockService;

        public NpcSalesService(IMonitor monitor, ModConfig config, TableRestockService restockService)
        {
            _monitor = monitor;
            _config = config;
            _restockService = restockService;
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

                seller.Money += sellPrice;
                Game1.playSound("purchase");
                Game1.chatBox.addInfoMessage($"Sold {evaluatedItem.DisplayName} to {npc.Name} for {sellPrice}g with base of {basePrice}");

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
    }
}
