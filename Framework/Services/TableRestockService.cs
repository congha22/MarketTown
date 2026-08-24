using System;
using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Objects;
using MarketTown.Framework.Config;

namespace MarketTown.Framework.Services
{
    public class TableRestockService
    {
        private readonly IMonitor _monitor;
        private readonly ModConfig _config;
        private readonly IModHelper _helper;

        private readonly List<PendingRestock> _pendingRestocks = new List<PendingRestock>();

        private class PendingRestock
        {
            public StardewValley.Object DisplayObject { get; set; }
            public Item LastSoldItem { get; set; }
            public GameLocation Location { get; set; }
        }

        public TableRestockService(IMonitor monitor, ModConfig config, IModHelper helper)
        {
            _monitor = monitor;
            _config = config;
            _helper = helper;

            _helper.Events.GameLoop.TimeChanged += OnTimeChanged;
            _helper.Events.GameLoop.DayEnding += OnDayEnding;
        }

        public void OnItemSold(StardewValley.Object displayObject, Item soldItem)
        {
            var location = displayObject.Location;
            if (location == null) return;

            var existing = _pendingRestocks.FirstOrDefault(p => p.DisplayObject == displayObject);
            if (existing != null)
            {
                existing.LastSoldItem = soldItem;
            }
            else
            {
                _pendingRestocks.Add(new PendingRestock
                {
                    DisplayObject = displayObject,
                    LastSoldItem = soldItem,
                    Location = location
                });
            }
        }

        private void OnTimeChanged(object sender, TimeChangedEventArgs e)
        {
            ProcessAllPendingRestocks(force: false);
        }

        private void OnDayEnding(object sender, DayEndingEventArgs e)
        {
            ProcessAllPendingRestocks(force: true);
            _pendingRestocks.Clear(); // Clears memory before save/sleep
        }

        private void ProcessAllPendingRestocks(bool force)
        {
            if (_pendingRestocks.Count == 0) return;

            // Clean up missing display objects
            _pendingRestocks.RemoveAll(p =>
                (p.DisplayObject is Furniture f && !p.Location.furniture.Contains(f) && !p.Location.Objects.Values.Contains(f)) ||
                (p.DisplayObject is Mannequin m && !p.Location.Objects.Values.Contains(m))
            );

            var byLocation = _pendingRestocks.GroupBy(p => p.Location).ToList();

            foreach (var locGroup in byLocation)
            {
                var location = locGroup.Key;

                var validChests = new List<Chest>();
                foreach (var obj in location.Objects.Values)
                {
                    if (obj is Chest chest)
                    {
                        if (chest.ItemId == "MT.Objects.MarketTownStorageSmall" ||
                            chest.ItemId == "MT.Objects.MarketTownStorageLarge")
                        {
                            validChests.Add(chest);
                        }
                    }
                }

                if (validChests.Count == 0)
                    continue;

                var toRemove = new List<PendingRestock>();

                foreach (var pending in locGroup)
                {
                    if (!force && Game1.random.NextDouble() > _config.RestockChance)
                        continue;

                    if (ProcessRestock(pending.DisplayObject, pending.LastSoldItem, location, validChests))
                    {
                        toRemove.Add(pending);
                    }
                }

                foreach (var r in toRemove)
                {
                    _pendingRestocks.Remove(r);
                }
            }
        }

        private bool ProcessRestock(StardewValley.Object displayObject, Item soldItem, GameLocation location, List<Chest> chests)
        {
            var nearbyChests = new List<Chest>();
            foreach (var chest in chests)
            {
                int maxDist = chest.ItemId == "MT.Objects.MarketTownStorageLarge" ? 20 : 10;

                int distX = (int)Math.Abs(chest.TileLocation.X - displayObject.TileLocation.X);
                int distY = (int)Math.Abs(chest.TileLocation.Y - displayObject.TileLocation.Y);

                if (distX <= maxDist && distY <= maxDist)
                {
                    nearbyChests.Add(chest);
                }
            }

            if (nearbyChests.Count == 0)
                return false;

            if (displayObject is Mannequin mannequin)
            {
                return RestockMannequin(mannequin, nearbyChests);
            }
            else if (displayObject is Furniture furniture && furniture.furniture_type.Value == Furniture.table)
            {
                return RestockTable(furniture, soldItem, nearbyChests);
            }

            return false;
        }

        private bool RestockMannequin(Mannequin mannequin, List<Chest> chests)
        {
            if (mannequin.hat.Value == null)
                TryFillMannequinSlot(item => mannequin.hat.Value = (Hat)item, chests, item => item is Hat);

            if (mannequin.shirt.Value == null)
                TryFillMannequinSlot(item => mannequin.shirt.Value = (Clothing)item, chests, item => item is Clothing c && c.clothesType.Value == Clothing.ClothesType.SHIRT);

            if (mannequin.pants.Value == null)
                TryFillMannequinSlot(item => mannequin.pants.Value = (Clothing)item, chests, item => item is Clothing c && c.clothesType.Value == Clothing.ClothesType.PANTS);

            if (mannequin.boots.Value == null)
                TryFillMannequinSlot(item => mannequin.boots.Value = (Boots)item, chests, item => item is Boots);

            return mannequin.hat.Value != null && mannequin.shirt.Value != null && mannequin.pants.Value != null && mannequin.boots.Value != null;
        }

        private bool TryFillMannequinSlot(Action<Item> setSlot, List<Chest> chests, Func<Item, bool> matchFunc)
        {
            foreach (var chest in chests)
            {
                for (int i = 0; i < chest.Items.Count; i++)
                {
                    var item = chest.Items[i];
                    if (item != null && matchFunc(item))
                    {
                        setSlot(item.getOne());
                        item.Stack--;
                        if (item.Stack <= 0)
                        {
                            chest.Items[i] = null;
                            chest.clearNulls();
                        }
                        return true;
                    }
                }
            }
            return false;
        }

        private bool RestockTable(Furniture table, Item soldItem, List<Chest> chests)
        {
            if (table.heldObject.Value != null)
                return true;

            StardewValley.Object bestItem = null;
            Chest bestChest = null;
            int bestItemIndex = -1;
            int bestScore = -1;

            int minScore = _config.RestockMinimumRule switch
            {
                RestockRule.SameItem => 3,
                RestockRule.SameCategory => 2,
                _ => 1
            };

            foreach (var chest in chests)
            {
                for (int i = 0; i < chest.Items.Count; i++)
                {
                    var item = chest.Items[i];
                    if (item == null || !(item is StardewValley.Object objItem))
                        continue;

                    int score = 1;

                    if (soldItem != null)
                    {
                        if (item.Name == soldItem.Name && item.ItemId == soldItem.ItemId)
                            score = 3;
                        else if (item.Category == soldItem.Category)
                            score = 2;
                    }

                    if (score >= minScore && score > bestScore)
                    {
                        bestScore = score;
                        bestItem = objItem;
                        bestChest = chest;
                        bestItemIndex = i;

                        if (bestScore == 3)
                            break;
                    }
                }
                if (bestScore == 3)
                    break;
            }

            if (bestItem != null)
            {
                table.heldObject.Value = (StardewValley.Object)bestItem.getOne();
                bestItem.Stack--;
                if (bestItem.Stack <= 0)
                {
                    bestChest.Items[bestItemIndex] = null;
                    bestChest.clearNulls();
                }

                _monitor.Log($"Restocked {table.Name} with {bestItem.DisplayName} from chest (Score: {bestScore}).", LogLevel.Debug);
                return true;
            }

            return false;
        }
    }
}
