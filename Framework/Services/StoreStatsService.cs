using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Linq;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using MarketTown.Framework.Models;

namespace MarketTown.Framework.Services
{
    public class StoreStatsService
    {
        private readonly IMonitor _monitor;
        private readonly IModHelper _helper;
        private readonly IndoorStoreTrackingService _storeTrackingService;

        private Dictionary<string, StoreStatRecord> _storeStats = new Dictionary<string, StoreStatRecord>();
        public IReadOnlyDictionary<string, StoreStatRecord> StoreStats => _storeStats;

        public StoreStatsService(IMonitor monitor, IModHelper helper, IndoorStoreTrackingService storeTrackingService)
        {
            _monitor = monitor;
            _helper = helper;
            _storeTrackingService = storeTrackingService;

            _helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
            _helper.Events.GameLoop.DayEnding += OnDayEnding;
        }

        public void IncrementVisitor(GameLocation location)
        {
            if (location == null) return;
            string key = location.NameOrUniqueName;

            if (!_storeStats.TryGetValue(key, out var record))
            {
                record = new StoreStatRecord();
                _storeStats[key] = record;
            }

            record.TotalVisitors++;
        }

        public void RecordSale(GameLocation location, int price)
        {
            if (location == null) return;
            string key = location.NameOrUniqueName;

            if (!_storeStats.TryGetValue(key, out var record))
            {
                record = new StoreStatRecord();
                _storeStats[key] = record;
            }

            record.TotalSoldItems++;
            record.TotalEarnings += price;
        }

        public void UpdateStoreHours(GameLocation location, int openHour, int closeHour)
        {
            if (location == null) return;
            string key = location.NameOrUniqueName;
            
            if (!_storeStats.TryGetValue(key, out var record))
            {
                record = new StoreStatRecord();
                _storeStats[key] = record;
            }

            record.OpenHour = openHour;
            record.CloseHour = closeHour;
        }

        private void OnSaveLoaded(object sender, SaveLoadedEventArgs e)
        {
            _storeStats.Clear();
            string savePath = Path.Combine(Constants.CurrentSavePath, "MarketTownStoreStats.json");

            if (File.Exists(savePath))
            {
                try
                {
                    string json = File.ReadAllText(savePath);
                    _storeStats = JsonSerializer.Deserialize<Dictionary<string, StoreStatRecord>>(json) ?? new Dictionary<string, StoreStatRecord>();
                    _monitor.Log("Successfully loaded Market Town store stats data.", LogLevel.Trace);
                }
                catch (Exception ex)
                {
                    _monitor.Log($"Failed to load Market Town store stats data. Creating new. Error: {ex}", LogLevel.Error);
                    _storeStats = new Dictionary<string, StoreStatRecord>();
                }
            }
            else
            {
                _storeStats = new Dictionary<string, StoreStatRecord>();
            }
        }

        private void OnDayEnding(object sender, DayEndingEventArgs e)
        {
            // Clear stats for locations that no longer have a register
            var activeLocationNames = _storeTrackingService.ActiveStoreLocations.Select(l => l.NameOrUniqueName).ToHashSet();
            var keysToRemove = _storeStats.Keys.Where(k => !activeLocationNames.Contains(k)).ToList();

            foreach (var key in keysToRemove)
            {
                _storeStats.Remove(key);
                _monitor.Log($"Removed store stats for {key} because the register was removed.", LogLevel.Trace);
            }

            // Save to disk
            string savePath = Path.Combine(Constants.CurrentSavePath, "MarketTownStoreStats.json");
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(_storeStats, options);
                File.WriteAllText(savePath, json);
                _monitor.Log("Successfully saved Market Town store stats data.", LogLevel.Trace);
            }
            catch (Exception ex)
            {
                _monitor.Log($"Failed to save Market Town store stats data. Error: {ex}", LogLevel.Error);
            }
        }
    }
}
