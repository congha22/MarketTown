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
    /// <summary>Handles the logic for NPCs scanning for items to buy.</summary>
    public class NpcScannerService
    {
        private readonly IMonitor _monitor;
        private readonly ModConfig _config;
        private readonly IModHelper _helper;
        private readonly MapPathfindingService _pathfindingService;

        private List<NPC> _cachedNpcs = new List<NPC>();
        private int _npcScanIndex = 0;

        /// <summary>Tracks the next available scan time (in total minutes) for each NPC by name.</summary>
        private Dictionary<string, int> _npcScanCooldowns = new Dictionary<string, int>();

        /// <summary>Tracks active table stops that NPCs are currently browsing.</summary>
        private readonly List<BrowsingTarget> _activeBrowsingTargets = new List<BrowsingTarget>();

        /// <summary>Represents a specific table stop in an NPC's browse sequence.</summary>
        private class BrowsingTarget
        {
            public string NpcName { get; set; }
            public Furniture Table { get; set; }
            public Microsoft.Xna.Framework.Point StandTile { get; set; }
            public int FacingDirection { get; set; }
            public int ScheduledTime { get; set; }

            /// <summary>Whether the NPC has reached this table stand tile.</summary>
            public bool HasArrived { get; set; }

            /// <summary>Tick when the NPC arrived at the stand tile.</summary>
            public uint ArrivalTick { get; set; }

            /// <summary>Whether the NPC has finished inspecting and shown their final reaction emote.</summary>
            public bool HasReacted { get; set; }
        }

        public NpcScannerService(IMonitor monitor, ModConfig config, IModHelper helper, MapPathfindingService pathfindingService = null)
        {
            _monitor = monitor;
            _config = config;
            _helper = helper;
            _pathfindingService = pathfindingService;

            _helper.Events.GameLoop.DayStarted += OnDayStarted;
            _helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
        }

        private void OnDayStarted(object sender, DayStartedEventArgs e)
        {
            _cachedNpcs.Clear();
            _npcScanIndex = 0;
            _npcScanCooldowns.Clear();
            _activeBrowsingTargets.Clear();

            // Cache all sociable villagers
            foreach (var npc in Utility.getAllCharacters())
            {
                if (npc.IsVillager && npc.CanSocialize)
                {
                    _cachedNpcs.Add(npc);
                }
            }
        }

        private void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady || _cachedNpcs.Count == 0)
                return;

            // Only run before 1AM (2500)
            if (Game1.timeOfDay > 2500)
                return;

            // Handle NPC table inspection & taste reaction sequence
            CheckBrowsingEmotes();

            // Distribute scan to 1 NPC every 5 ticks
            if (e.IsMultipleOf(5))
            {
                if (_npcScanIndex >= _cachedNpcs.Count)
                {
                    _npcScanIndex = 0;
                }

                NPC npc = _cachedNpcs[_npcScanIndex];
                ProcessNpcScan(npc);

                _npcScanIndex++;
            }
        }

        /// <summary>
        /// Manages the 2-stage table inspection sequence:
        /// 1) Upon arrival: face table and show analyzing emote (dots / ?).
        /// 2) After ~1.2s delay: evaluate the item's gift taste and show reaction emote (love/like/neutral/dislike/hate).
        /// </summary>
        private void CheckBrowsingEmotes()
        {
            if (_activeBrowsingTargets.Count == 0)
                return;

            // Clean up stale targets from past time ticks
            _activeBrowsingTargets.RemoveAll(t => Game1.timeOfDay > NpcScheduleHelper.ConvertToHour(t.ScheduledTime + 40));

            for (int i = 0; i < _activeBrowsingTargets.Count; i++)
            {
                var target = _activeBrowsingTargets[i];
                if (target.HasReacted) continue;

                var npc = _cachedNpcs.FirstOrDefault(n => n.Name == target.NpcName);
                if (npc == null || npc.currentLocation == null) continue;

                // Stage 1: Check arrival
                if (!target.HasArrived)
                {
                    bool isAtTile = npc.TilePoint == target.StandTile
                        || Microsoft.Xna.Framework.Vector2.Distance(npc.Tile, target.StandTile.ToVector2()) < 1.2f;

                    if (isAtTile && !npc.isMoving())
                    {
                        target.HasArrived = true;
                        target.ArrivalTick = (uint)Game1.ticks;
                        npc.faceDirection(target.FacingDirection);

                        // Initial analyzing emote: 8 (ellipsis ...) or 40 (question ?)
                        int inspectEmote = Game1.random.NextDouble() < 0.5 ? 8 : 40;
                        npc.doEmote(inspectEmote);

                        _monitor.Log($"{npc.Name} arrived at table (tile: {target.StandTile}), analyzing item...", LogLevel.Debug);
                    }
                }
                // Stage 2: After inspecting for 300 ticks, evaluate taste and display final reaction
                else if (Game1.ticks - target.ArrivalTick >= 300)
                {
                    target.HasReacted = true;
                    npc.faceDirection(target.FacingDirection);

                    var item = target.Table?.heldObject?.Value;
                    int reactionEmote;

                    if (item != null)
                    {
                        int taste = npc.getGiftTasteForThisItem(item);
                        reactionEmote = taste switch
                        {
                            NPC.gift_taste_love => 20,       // Heart ❤️ (Loved)
                            NPC.gift_taste_like => 32,       // Happy 😊 (Liked)
                            NPC.gift_taste_dislike => 28,    // Sad/sweatdrop 💧 (Disliked)
                            NPC.gift_taste_hate => 12,       // Angry 💢 (Hated)
                            _ => 56                          // Music note 🎵 (Neutral)
                        };

                        // Variety variations
                        if (taste == NPC.gift_taste_love && Game1.random.NextDouble() < 0.3) reactionEmote = 60; // Blush
                        if (taste == NPC.gift_taste_hate && Game1.random.NextDouble() < 0.5) reactionEmote = 36; // X mark
                        if (taste == NPC.gift_taste_neutral && Game1.random.NextDouble() < 0.5) reactionEmote = 32; // Happy

                        _monitor.Log($"{npc.Name} evaluated '{item.DisplayName}' (Taste: {taste}) -> reacted with emote {reactionEmote}.", LogLevel.Debug);
                    }
                    else
                    {
                        // Table is empty
                        reactionEmote = 40; // Question ❓
                        _monitor.Log($"{npc.Name} checked table (empty) -> reacted with emote {reactionEmote}.", LogLevel.Debug);
                    }

                    npc.doEmote(reactionEmote);
                }
            }
        }

        private void ProcessNpcScan(NPC npc)
        {
            // Check cooldown
            int currentTotalMinutes = (Game1.timeOfDay / 100 * 60) + (Game1.timeOfDay % 100);
            if (_npcScanCooldowns.TryGetValue(npc.Name, out int nextAvailableScanTime))
            {
                if (currentTotalMinutes < nextAvailableScanTime)
                {
                    return; // Still on cooldown
                }
            }

            // Check location valid
            if (npc.currentLocation == null)
                return;

            bool isOutdoor = npc.currentLocation.IsOutdoors;
            bool hasIndoorLicense = false;

            // TODO: In the future, check if npc.currentLocation has a Store License placed inside
            if (!isOutdoor && !hasIndoorLicense)
            {
                return;
            }

            // Scan for all valid tables with items within range
            var validTables = new List<Furniture>();
            foreach (var furniture in npc.currentLocation.furniture)
            {
                if (furniture.furniture_type.Value == Furniture.table && furniture.heldObject.Value != null)
                {
                    float distance = Utility.distance(npc.TilePoint.X, furniture.TileLocation.X, npc.TilePoint.Y, furniture.TileLocation.Y);
                    if (distance <= _config.NpcScanRange)
                    {
                        validTables.Add(furniture);
                    }
                }
            }

            if (validTables.Count > 0)
            {
                // Apply chance roll
                if (Game1.random.NextDouble() <= _config.NpcScanChance)
                {
                    // Pick a random valid table from the list
                    Furniture selectedTable = validTables[Game1.random.Next(validTables.Count)];

                    _monitor.Log($"{npc.Name} spotted '{selectedTable.heldObject.Value.Name}' on a table at {selectedTable.TileLocation} — searching nearby tables to browse.", LogLevel.Debug);

                    // Apply cooldown before pathing (prevents double-assignment)
                    _npcScanCooldowns[npc.Name] = currentTotalMinutes + _config.NpcScanCooldownMinutes;

                    // Send the NPC toward the selected table and any nearby browse tables
                    SendNpcToTable(npc, selectedTable);
                }
            }
        }

        /// <summary>
        /// Finds the initial table and 0 to 2 nearby tables, and injects sequential schedule stops.
        /// </summary>
        private void SendNpcToTable(NPC npc, StardewValley.Objects.Furniture initialTable)
        {
            // Find other tables holding items within browse range of the initial table
            var nearbyTables = new List<Furniture>();
            foreach (var f in npc.currentLocation.furniture)
            {
                if (f != null && f != initialTable && f.furniture_type.Value == Furniture.table && f.heldObject.Value != null)
                {
                    float dist = Microsoft.Xna.Framework.Vector2.Distance(f.TileLocation, initialTable.TileLocation);
                    if (dist <= _config.NpcBrowseRange)
                    {
                        nearbyTables.Add(f);
                    }
                }
            }

            // Randomly choose 0 up to MaxExtraBrowseTables (default: 2) additional tables
            int maxExtra = Math.Min(_config.MaxExtraBrowseTables, nearbyTables.Count);
            int countExtra = maxExtra > 0 ? Game1.random.Next(0, maxExtra + 1) : 0;

            var selectedTables = new List<Furniture> { initialTable };
            if (countExtra > 0)
            {
                var extra = nearbyTables.OrderBy(_ => Game1.random.Next()).Take(countExtra);
                selectedTables.AddRange(extra);
            }

            // Build schedule stops for each selected table (+10 minutes apart)
            var stops = new List<(Furniture table, string locationName, Microsoft.Xna.Framework.Vector2 standTile, int facing, int scheduledTime)>();
            int currentTime = NpcScheduleHelper.ConvertToHour(Game1.timeOfDay + 10);

            foreach (var table in selectedTables)
            {
                var standTile = NpcScheduleHelper.GetAdjacentWalkableTile(npc.currentLocation, table.TileLocation, out int facing);
                if (standTile != Microsoft.Xna.Framework.Vector2.Zero)
                {
                    stops.Add((table, npc.currentLocation.NameOrUniqueName, standTile, facing, currentTime));
                    currentTime = NpcScheduleHelper.ConvertToHour(currentTime + 10);
                }
            }

            if (stops.Count == 0)
            {
                _monitor.Log($"{npc.Name}: no walkable tiles adjacent to spotted tables — skipping.", LogLevel.Debug);
                return;
            }

            // Ensure NoPath properties are up-to-date for all impassable furniture before pathfinder runs
            _pathfindingService?.UpdateLocationPathProperties(npc.currentLocation);

            var scheduleStops = stops.Select(s => (s.locationName, s.standTile, s.facing, s.scheduledTime)).ToList();
            bool success = NpcScheduleService.AddNewPointsToSchedule(npc, scheduleStops);

            if (success)
            {
                _monitor.Log($"{npc.Name} queued {stops.Count} browse stop(s) starting at time {stops[0].scheduledTime}.", LogLevel.Debug);

                // Register active browsing targets for inspection & taste evaluation
                foreach (var stop in stops)
                {
                    _activeBrowsingTargets.Add(new BrowsingTarget
                    {
                        NpcName = npc.Name,
                        Table = stop.table,
                        StandTile = stop.standTile.ToPoint(),
                        FacingDirection = stop.facing,
                        ScheduledTime = stop.scheduledTime,
                        HasArrived = false,
                        ArrivalTick = 0,
                        HasReacted = false
                    });
                }
            }
            else
            {
                _monitor.Log($"{npc.Name}: schedule injection failed (likely mid-transition). Skipping.", LogLevel.Debug);
            }
        }
    }
}
