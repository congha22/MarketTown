using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Objects;
using MarketTown.Framework.Models;
using MarketTown.Framework.Integrations;

namespace MarketTown.Framework.Services
{
    public class StoreEmployeeService
    {
        private class EmployeeAnimState
        {
            public float Timer;
            public int CurrentActionDuration;
            public string Action;
            public float EmoteTimer;
            public float EmoteThreshold = Game1.random.Next(15000, 30000);
        }

        private readonly IMonitor _monitor;
        private readonly IModHelper _helper;
        private readonly IndoorStoreTrackingService _storeTrackingService;
        private ICASApi _casApi;
        private Dictionary<NPC, EmployeeAnimState> _animStates = new Dictionary<NPC, EmployeeAnimState>();

        private Dictionary<string, StoreEmployeeRecord> _storeEmployees = new Dictionary<string, StoreEmployeeRecord>();

        public IReadOnlyDictionary<string, StoreEmployeeRecord> StoreEmployees => _storeEmployees;

        public StoreEmployeeService(IMonitor monitor, IModHelper helper, IndoorStoreTrackingService storeTrackingService)
        {
            _monitor = monitor;
            _helper = helper;
            _storeTrackingService = storeTrackingService;

            _helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
            _helper.Events.GameLoop.DayEnding += OnDayEnding;
            _helper.Events.GameLoop.TimeChanged += OnTimeChanged;
            _helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
        }

        public void HireEmployee(GameLocation location, Vector2 checkoutTile, string npcName)
        {
            if (location == null) return;
            string key = location.NameOrUniqueName;

            if (!_storeEmployees.TryGetValue(key, out var record))
            {
                record = new StoreEmployeeRecord();
                _storeEmployees[key] = record;
            }

            string tileKey = $"{checkoutTile.X},{checkoutTile.Y}";
            record.HiredNPCs[tileKey] = npcName;

            _monitor.Log($"Hired {npcName} at {key} checkout tile {tileKey}", LogLevel.Info);

            // Apply immediately if already open
            if (_storeTrackingService.StoreStatsService.StoreStats.TryGetValue(key, out var stats))
            {
                if (Game1.timeOfDay >= stats.OpenHour && Game1.timeOfDay < stats.CloseHour)
                {
                    PlaceEmployeeAtCheckout(location, checkoutTile, npcName);
                }
            }
        }

        public void FireEmployee(GameLocation location, Vector2 checkoutTile)
        {
            if (location == null) return;
            string key = location.NameOrUniqueName;

            if (_storeEmployees.TryGetValue(key, out var record))
            {
                string tileKey = $"{checkoutTile.X},{checkoutTile.Y}";
                if (record.HiredNPCs.TryGetValue(tileKey, out string npcName))
                {
                    record.HiredNPCs.Remove(tileKey);
                    _monitor.Log($"Fired {npcName} from {key} checkout tile {tileKey}", LogLevel.Info);

                    NPC npc = Game1.getCharacterFromName(npcName);
                    if (npc != null)
                    {
                        if (_animStates.ContainsKey(npc))
                        {
                            _animStates[npc].Action = "none";
                            _animStates[npc].Timer = 0;
                        }
                        if (_casApi != null) _casApi.TriggerNpcAction(npc, "stop", 2);

                        SendEmployeeHome(npc);
                        npc.ignoreScheduleToday = false;
                    }
                }
            }
        }

        public bool IsEmployee(NPC npc)
        {
            if (npc == null) return false;
            foreach (var record in _storeEmployees.Values)
            {
                if (record.HiredNPCs.ContainsValue(npc.Name))
                {
                    return true;
                }
            }
            return false;
        }

        public string GetHiredEmployee(GameLocation location, Vector2 checkoutTile)
        {
            if (location == null) return null;
            string key = location.NameOrUniqueName;
            if (_storeEmployees.TryGetValue(key, out var record))
            {
                string tileKey = $"{checkoutTile.X},{checkoutTile.Y}";
                if (record.HiredNPCs.TryGetValue(tileKey, out var npcName))
                {
                    return npcName;
                }
            }
            return null;
        }

        private void OnSaveLoaded(object sender, SaveLoadedEventArgs e)
        {
            _storeEmployees.Clear();
            string savePath = Path.Combine(Constants.CurrentSavePath, "MarketTownStoreEmployees.json");

            if (File.Exists(savePath))
            {
                try
                {
                    string json = File.ReadAllText(savePath);
                    _storeEmployees = JsonSerializer.Deserialize<Dictionary<string, StoreEmployeeRecord>>(json) ?? new Dictionary<string, StoreEmployeeRecord>();
                    _monitor.Log("Successfully loaded Market Town store employees data.", LogLevel.Trace);
                }
                catch (Exception ex)
                {
                    _monitor.Log($"Failed to load Market Town store employees data. Creating new. Error: {ex}", LogLevel.Error);
                    _storeEmployees = new Dictionary<string, StoreEmployeeRecord>();
                }
            }
            else
            {
                _storeEmployees = new Dictionary<string, StoreEmployeeRecord>();
            }
        }

        private void OnDayEnding(object sender, DayEndingEventArgs e)
        {
            // Clear stats for locations that no longer have a register
            var activeLocationNames = _storeTrackingService.ActiveStoreLocations.Select(l => l.NameOrUniqueName).ToHashSet();
            var keysToRemove = _storeEmployees.Keys.Where(k => !activeLocationNames.Contains(k)).ToList();

            foreach (var key in keysToRemove)
            {
                _storeEmployees.Remove(key);
                _monitor.Log($"Removed store employees for {key} because the store is no longer active.", LogLevel.Trace);
            }

            // Save to disk
            string savePath = Path.Combine(Constants.CurrentSavePath, "MarketTownStoreEmployees.json");
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(_storeEmployees, options);
                File.WriteAllText(savePath, json);
                _monitor.Log("Successfully saved Market Town store employees data.", LogLevel.Trace);
            }
            catch (Exception ex)
            {
                _monitor.Log($"Failed to save Market Town store employees data. Error: {ex}", LogLevel.Error);
            }
        }

        private void OnTimeChanged(object sender, TimeChangedEventArgs e)
        {
            // Process employee schedules and pathfinding based on store hours
            foreach (var location in _storeTrackingService.ActiveStoreLocations)
            {
                string key = location.NameOrUniqueName;
                if (_storeEmployees.TryGetValue(key, out var record) && _storeTrackingService.StoreStatsService.StoreStats.TryGetValue(key, out var stats))
                {
                    foreach (var kvp in record.HiredNPCs)
                    {
                        var parts = kvp.Key.Split(',');
                        if (parts.Length == 2 && float.TryParse(parts[0], out float x) && float.TryParse(parts[1], out float y))
                        {
                            Vector2 checkoutTile = new Vector2(x, y);
                            NPC npc = Game1.getCharacterFromName(kvp.Value);
                            if (npc == null) continue;

                            Furniture checkout = location.furniture.FirstOrDefault(f => f.TileLocation == checkoutTile && (f.ItemId == "d5a1lamdtd.MarketTown_CheckoutSmall" || f.ItemId == "d5a1lamdtd.MarketTown_CheckoutLarge"));
                            if (checkout == null) continue;

                            Vector2 standTile = checkoutTile;
                            if (checkout.ItemId == "d5a1lamdtd.MarketTown_CheckoutSmall") standTile = new Vector2(checkoutTile.X, checkoutTile.Y + 1);
                            else if (checkout.ItemId == "d5a1lamdtd.MarketTown_CheckoutLarge") standTile = new Vector2(checkoutTile.X + 1, checkoutTile.Y + 1);

                            if (Game1.timeOfDay == stats.OpenHour)
                            {
                                // Walk to work
                                Vector2 spawnTile = _storeTrackingService.VisitorService.GetEntryTile(location);
                                if (spawnTile != Vector2.Zero)
                                {
                                    NpcScheduleHelper.CleanNpc(npc);
                                    Game1.warpCharacter(npc, location, spawnTile);
                                    npc.controller = new StardewValley.Pathfinding.PathFindController(npc, location, new Point((int)standTile.X, (int)standTile.Y), 2, (c, l) => {
                                        PlaceEmployeeAtCheckout(l, checkoutTile, kvp.Value);
                                    });
                                }
                                else
                                {
                                    PlaceEmployeeAtCheckout(location, checkoutTile, kvp.Value);
                                }
                            }
                            else if (Game1.timeOfDay >= stats.CloseHour && Game1.timeOfDay < stats.CloseHour + 200)
                            {
                                // Walk home once all customers leave
                                if (npc.controller == null && npc.currentLocation == location)
                                {
                                    int visitors = _storeTrackingService.VisitorService.GetActiveVisitorCount(location);
                                    if (visitors == 0)
                                    {
                                        Vector2 doorTile = _storeTrackingService.VisitorService.GetEntryTile(location);
                                        if (doorTile != Vector2.Zero)
                                        {
                                            if (_animStates.ContainsKey(npc))
                                            {
                                                _animStates[npc].Action = "none";
                                                _animStates[npc].Timer = 0;
                                            }
                                            if (_casApi != null) _casApi.TriggerNpcAction(npc, "stop", 2);
                                            
                                            NpcScheduleHelper.CleanNpc(npc);
                                            npc.controller = new StardewValley.Pathfinding.PathFindController(npc, location, new Point((int)doorTile.X, (int)doorTile.Y), 2, (c, l) => {
                                                SendEmployeeHome(npc);
                                            });
                                        }
                                        else
                                        {
                                            SendEmployeeHome(npc);
                                        }
                                    }
                                }
                            }
                            else if (Game1.timeOfDay >= stats.CloseHour + 200)
                            {
                                // Force leave if they got stuck
                                if (npc.currentLocation == location)
                                {
                                    SendEmployeeHome(npc);
                                }
                            }
                            else if (Game1.timeOfDay > stats.OpenHour && Game1.timeOfDay < stats.CloseHour)
                            {
                                // Enforce placement during open hours if not moving (e.g. loaded mid-day)
                                if (npc.currentLocation != location || (npc.Tile != standTile && npc.controller == null))
                                {
                                    PlaceEmployeeAtCheckout(location, checkoutTile, kvp.Value);
                                }
                            }
                        }
                    }
                }
            }
        }

        private void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady || !e.IsMultipleOf(30)) return;

            // Auto-fire employees if their checkout register was moved or removed
            if (e.IsMultipleOf(60))
            {
                foreach (var location in _storeTrackingService.ActiveStoreLocations)
                {
                    string key = location.NameOrUniqueName;
                    if (_storeEmployees.TryGetValue(key, out var record))
                    {
                        List<Vector2> tilesToRemove = new List<Vector2>();
                        foreach (var kvp in record.HiredNPCs)
                        {
                            var parts = kvp.Key.Split(',');
                            if (parts.Length == 2 && float.TryParse(parts[0], out float x) && float.TryParse(parts[1], out float y))
                            {
                                Vector2 checkoutTile = new Vector2(x, y);
                                Furniture checkout = location.furniture.FirstOrDefault(f => f.TileLocation == checkoutTile && (f.ItemId == "d5a1lamdtd.MarketTown_CheckoutSmall" || f.ItemId == "d5a1lamdtd.MarketTown_CheckoutLarge"));
                                if (checkout == null)
                                {
                                    tilesToRemove.Add(checkoutTile);
                                }
                            }
                        }

                        foreach (var tile in tilesToRemove)
                        {
                            FireEmployee(location, tile);
                        }
                    }
                }
            }

            if (_casApi == null)
            {
                _casApi = _helper.ModRegistry.GetApi<ICASApi>("d5a1lamdtd.CASCreateAStardewie");
                if (_casApi == null) return;
            }

            foreach (var record in _storeEmployees.Values)
            {
                foreach (var npcName in record.HiredNPCs.Values)
                {
                    NPC npc = Game1.getCharacterFromName(npcName);
                    if (npc == null || npc.currentLocation == null) continue;
                    
                    if (npc.isMoving() || npc.controller != null) continue;

                    if (!_animStates.TryGetValue(npc, out var state))
                    {
                        state = new EmployeeAnimState { Action = "none" };
                        _animStates[npc] = state;
                    }

                    state.EmoteTimer += 500f; // 30 ticks = approx 500ms
                    if (state.EmoteTimer >= state.EmoteThreshold)
                    {
                        state.EmoteTimer = 0;
                        state.EmoteThreshold = Game1.random.Next(15000, 30000);
                        int[] emotes = new int[] { 20, 56, 32 };
                        npc.doEmote(emotes[Game1.random.Next(emotes.Length)]);
                    }

                    if (state.Action != "none")
                    {
                        state.Timer += 500f; // 30 ticks = approx 500ms
                        if (state.Timer >= state.CurrentActionDuration)
                        {
                            state.Action = "none";
                            state.Timer = 0;
                            _casApi.TriggerNpcAction(npc, "stop", 1); // Face right
                        }
                    }
                    else
                    {
                        if (Game1.random.NextDouble() < 0.05) // 5% chance every 500ms = roughly every 10 seconds
                        {
                            double roll = Game1.random.NextDouble();
                            if (roll < 0.25)
                            {
                                state.Action = "look_up";
                                state.CurrentActionDuration = Game1.random.Next(2000, 3000);
                                state.Timer = 0;
                                _casApi.TriggerNpcAction(npc, "stop", 0);
                            }
                            else if (roll < 0.50)
                            {
                                state.Action = "look_down";
                                state.CurrentActionDuration = Game1.random.Next(2000, 3000);
                                state.Timer = 0;
                                _casApi.TriggerNpcAction(npc, "stop", 2);
                            }
                            else if (roll < 0.65)
                            {
                                state.Action = "fishing";
                                state.CurrentActionDuration = Game1.random.Next(3000, 5000);
                                state.Timer = 0;
                                _casApi.TriggerNpcAction(npc, "fishing", 1);
                            }
                            else if (roll < 0.80)
                            {
                                state.Action = "shearing";
                                state.CurrentActionDuration = Game1.random.Next(3000, 5000);
                                state.Timer = 0;
                                _casApi.TriggerNpcAction(npc, "shearing", 1);
                            }
                            else
                            {
                                state.Action = "milking";
                                state.CurrentActionDuration = Game1.random.Next(3000, 5000);
                                state.Timer = 0;
                                _casApi.TriggerNpcAction(npc, "milking", 1);
                            }
                        }
                    }
                }
            }
        }

        public void TriggerCheckoutReaction(GameLocation location, Vector2 checkoutTile)
        {
            string employeeName = GetHiredEmployee(location, checkoutTile);
            if (!string.IsNullOrEmpty(employeeName))
            {
                NPC employee = Game1.getCharacterFromName(employeeName);
                if (employee != null && employee.currentLocation == location)
                {
                    if (!_animStates.TryGetValue(employee, out var state))
                    {
                        state = new EmployeeAnimState { Action = "none" };
                        _animStates[employee] = state;
                    }

                    employee.faceDirection(1); // Face right
                    employee.doEmote(Game1.random.NextDouble() < 0.5 ? 20 : 56);

                    if (_casApi != null)
                    {
                        double roll = Game1.random.NextDouble();
                        if (roll < 0.33)
                        {
                            state.Action = "fishing";
                            _casApi.TriggerNpcAction(employee, "fishing", 1);
                        }
                        else if (roll < 0.66)
                        {
                            state.Action = "shearing";
                            _casApi.TriggerNpcAction(employee, "shearing", 1);
                        }
                        else
                        {
                            state.Action = "milking";
                            _casApi.TriggerNpcAction(employee, "milking", 1);
                        }

                        state.CurrentActionDuration = Game1.random.Next(3000, 5000);
                        state.Timer = 0;
                    }
                }
            }
        }

        private void PlaceEmployeeAtCheckout(GameLocation location, Vector2 checkoutTile, string npcName)
        {
            NPC npc = Game1.getCharacterFromName(npcName);
            if (npc == null) return;

            // Find checkout furniture
            Furniture checkout = location.furniture.FirstOrDefault(f => f.TileLocation == checkoutTile && (f.ItemId == "d5a1lamdtd.MarketTown_CheckoutSmall" || f.ItemId == "d5a1lamdtd.MarketTown_CheckoutLarge"));
            if (checkout == null) return;

            Vector2 standTile = checkoutTile;
            if (checkout.ItemId == "d5a1lamdtd.MarketTown_CheckoutSmall")
            {
                // Small checkout spot: X + 0, Y + 1 (Wait, for 3x3 small checkout with bounding box 3x2, tile X0 Y1 relative to top-left walkable. Top left is TileLocation)
                standTile = new Vector2(checkoutTile.X + 0, checkoutTile.Y + 1);
            }
            else if (checkout.ItemId == "d5a1lamdtd.MarketTown_CheckoutLarge")
            {
                // Large checkout spot: X + 1, Y + 1 (moved up 1 tile)
                standTile = new Vector2(checkoutTile.X + 1, checkoutTile.Y + 1);
            }

            NpcScheduleHelper.CleanNpc(npc);
            Game1.warpCharacter(npc, location, standTile);
            npc.faceDirection(1); // Face right

            // Clear any lingering controllers or schedules to ensure they stand still
            npc.controller = null;
            npc.ClearSchedule();
            npc.ignoreScheduleToday = true;
        }

        private void SendEmployeeHome(NPC npc)
        {
            if (npc == null) return;
            NpcScheduleHelper.CleanNpc(npc);
            
            string defaultMap = npc.DefaultMap;
            if (string.IsNullOrEmpty(defaultMap)) defaultMap = "Town";
            
            Game1.warpCharacter(npc, defaultMap, npc.DefaultPosition / 64f);
            npc.faceDirection(npc.DefaultFacingDirection);
        }
    }
}
