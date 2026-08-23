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

        private List<NPC> _cachedNpcs = new List<NPC>();
        private int _npcScanIndex = 0;

        /// <summary>Tracks the next available scan time (in total minutes) for each NPC by name.</summary>
        private Dictionary<string, int> _npcScanCooldowns = new Dictionary<string, int>();

        public NpcScannerService(IMonitor monitor, ModConfig config, IModHelper helper)
        {
            _monitor = monitor;
            _config = config;
            _helper = helper;

            _helper.Events.GameLoop.DayStarted += OnDayStarted;
            _helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
        }

        private void OnDayStarted(object sender, DayStartedEventArgs e)
        {
            _cachedNpcs.Clear();
            _npcScanIndex = 0;
            _npcScanCooldowns.Clear();

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

            // Scan for tables with items
            foreach (var furniture in npc.currentLocation.furniture)
            {
                if (furniture.furniture_type.Value == Furniture.table && furniture.heldObject.Value != null)
                {
                    float distance = Utility.distance(npc.TilePoint.X, npc.TilePoint.X, furniture.TileLocation.X, furniture.TileLocation.Y);
                    if (distance <= _config.NpcScanRange)
                    {
                        Game1.chatBox.addErrorMessage(distance.ToString());
                        // Apply chance roll
                        if (Game1.random.NextDouble() <= _config.NpcScanChance)
                        {
                            _monitor.Log($"{npc.Name} spotted {furniture.heldObject.Value.Name} on a table!", LogLevel.Debug);

                            // Put NPC on cooldown
                            _npcScanCooldowns[npc.Name] = currentTotalMinutes + _config.NpcScanCooldownMinutes;

                            // Break out of loop since they found something
                            break;
                        }
                    }
                }
            }
        }
    }
}
