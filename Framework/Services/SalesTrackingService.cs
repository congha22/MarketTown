using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using MarketTown.Framework.Models;

namespace MarketTown.Framework.Services
{
    public class SalesTrackingService
    {
        private readonly IMonitor _monitor;
        private readonly IModHelper _helper;

        private MarketTownSaveData _saveData;
        private readonly List<SaleRecord> _todaySales = new List<SaleRecord>();

        public MarketTownSaveData SaveData => _saveData;
        public IReadOnlyList<SaleRecord> TodaySales => _todaySales;

        public SalesTrackingService(IMonitor monitor, IModHelper helper)
        {
            _monitor = monitor;
            _helper = helper;

            _helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
            _helper.Events.GameLoop.DayEnding += OnDayEnding;
        }

        public void RecordSale(NPC buyer, Item item, int price, GameLocation location, int taste)
        {
            if (_saveData == null) return;

            // 1. Add to Today's Sales
            var record = new SaleRecord
            {
                BuyerName = buyer.Name,
                QualifiedItemId = item.QualifiedItemId,
                ItemName = item.Name,
                Quality = item.Quality,
                GiftTaste = taste,
                SoldPrice = price,
                LocationName = location?.NameOrUniqueName ?? "Unknown",
                TimeOfDay = Game1.timeOfDay
            };
            _todaySales.Add(record);

            // 2. Update Item Stats
            if (!_saveData.ItemStats.TryGetValue(item.QualifiedItemId, out var itemStats))
            {
                itemStats = new ItemSaleStats();
                _saveData.ItemStats[item.QualifiedItemId] = itemStats;
            }

            itemStats.TotalSold++;
            itemStats.TotalEarnings += price;
            
            switch (item.Quality)
            {
                case 0: itemStats.TotalRegular++; break;
                case 1: itemStats.TotalSilver++; break;
                case 2: itemStats.TotalGold++; break;
                case 4: itemStats.TotalIridium++; break;
            }

            // 3. Update Category Stats
            if (!_saveData.CategoryStats.TryGetValue(item.Category, out var catStats))
            {
                catStats = new CategorySaleStats();
                _saveData.CategoryStats[item.Category] = catStats;
            }

            catStats.TotalSold++;
            catStats.TotalEarnings += price;

            // 4. Update Customer Stats
            if (!_saveData.CustomerStats.TryGetValue(buyer.Name, out var customerStats))
            {
                customerStats = new CustomerSaleStats();
                _saveData.CustomerStats[buyer.Name] = customerStats;
            }

            customerStats.TotalPurchased++;
            customerStats.TotalSpent += price;

            switch (taste)
            {
                case NPC.gift_taste_love: customerStats.TotalLove++; break;
                case NPC.gift_taste_like: customerStats.TotalLike++; break;
                case NPC.gift_taste_neutral: customerStats.TotalNeutral++; break;
                case NPC.gift_taste_dislike: customerStats.TotalDislike++; break;
                case NPC.gift_taste_hate: customerStats.TotalHate++; break;
            }
        }

        private void OnSaveLoaded(object sender, SaveLoadedEventArgs e)
        {
            _todaySales.Clear();
            string savePath = Path.Combine(Constants.CurrentSavePath, "MarketTownSales.json");

            if (File.Exists(savePath))
            {
                try
                {
                    string json = File.ReadAllText(savePath);
                    _saveData = JsonSerializer.Deserialize<MarketTownSaveData>(json) ?? new MarketTownSaveData();
                    _monitor.Log("Successfully loaded Market Town sales data.", LogLevel.Trace);
                }
                catch (Exception ex)
                {
                    _monitor.Log($"Failed to load Market Town sales data. Creating new. Error: {ex}", LogLevel.Error);
                    _saveData = new MarketTownSaveData();
                }
            }
            else
            {
                _saveData = new MarketTownSaveData();
            }
        }

        private void OnDayEnding(object sender, DayEndingEventArgs e)
        {
            if (_saveData == null) return;

            // Move today's sales into YesterdaySales
            _saveData.YesterdaySales.Clear();
            _saveData.YesterdaySales.AddRange(_todaySales);
            _todaySales.Clear();

            // Save to disk
            string savePath = Path.Combine(Constants.CurrentSavePath, "MarketTownSales.json");
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(_saveData, options);
                File.WriteAllText(savePath, json);
                _monitor.Log("Successfully saved Market Town sales data.", LogLevel.Trace);
            }
            catch (Exception ex)
            {
                _monitor.Log($"Failed to save Market Town sales data. Error: {ex}", LogLevel.Error);
            }
        }
    }
}
