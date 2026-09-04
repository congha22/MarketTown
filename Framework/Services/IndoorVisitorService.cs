using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Objects;
using MarketTown.Framework.Behaviors.ShopCategories;
using MarketTown.Framework.Config;
using MarketTown.Framework.Models;

namespace MarketTown.Framework.Services
{
    public class IndoorVisitorService
    {
        private readonly IMonitor _monitor;
        private readonly IModHelper _helper;
        private readonly ModConfig _config;
        private readonly IndoorStoreTrackingService _storeTrackingService;
        private readonly StoreStatsService _storeStatsService;

        /// <summary>Optional. When set, provides theme-specific NPC behaviors and capacity buffs.</summary>
        public ShopBehaviorService ShopBehaviorService { get; set; }

        public class VisitorData
        {
            public int DepartureTime { get; set; }
            public Vector2 SpawnTile { get; set; }
            public bool IsDeparting { get; set; }

            public List<CartItem> ShoppingCart { get; set; } = new List<CartItem>();
            public bool IsCheckingOut { get; set; }
            public Furniture AssignedCheckout { get; set; }
            public int CheckoutSlotIndex { get; set; }
            public float CheckoutWaitTimer { get; set; }
            public bool CheckoutSlotReached { get; set; }
            public bool HasStartedCheckoutEmote { get; set; }

            public float RandomEmoteTimer { get; set; }
            public float RandomEmoteThreshold { get; set; } = Game1.random.Next(15000, 30000);

            // ── Fitting Booth state ───────────────────────────────────────
            /// <summary>True while the NPC is walking toward a fitting booth slot.</summary>
            public bool IsHeadingToBooth { get; set; }
            /// <summary>True while the NPC is inside the booth waiting.</summary>
            public bool IsInFittingBooth { get; set; }
            /// <summary>World-tile of the booth's walkable slot (X=1, Y=1 relative to furniture TileLocation).</summary>
            public Vector2 FittingBoothTile { get; set; }
            /// <summary>Accumulated milliseconds spent inside the booth (per OnVisitorTick delta).</summary>
            public float FittingBoothTimer { get; set; }
            /// <summary>Randomised target duration in ms (3 000–5 000). Set when the NPC arrives.</summary>
            public float FittingBoothDuration { get; set; }
            /// <summary>
            /// Number of fashion items in the cart when the NPC last began a booth visit.
            /// FashionShopBehavior compares the current fashion item count against this value:
            /// only sends them to the booth again if they have bought MORE since the last visit.
            /// </summary>
            public int FashionItemsAtLastBoothEntry { get; set; } = 0;

            // ── Sitting/Reading state ───────────────────────────────────────
            public bool IsHeadingToRead { get; set; }
            public bool IsSittingToRead { get; set; }
            public Vector2 ReadingChairTile { get; set; }
            public Furniture AssignedChair { get; set; }
            public int AssignedChairSlotIndex { get; set; }
            public float ReadingTimer { get; set; }
            public float ReadingDuration { get; set; }
            public int BooksAtLastReadEntry { get; set; } = 0;
        }

        private readonly Dictionary<NPC, VisitorData> _activeVisitors = new Dictionary<NPC, VisitorData>();
        private readonly Dictionary<GameLocation, int> _cachedStoreCapacity = new Dictionary<GameLocation, int>();
        private readonly Dictionary<GameLocation, Warp> _cachedEntryWarps = new Dictionary<GameLocation, Warp>();

        private int _wanderCheckIndex = 0;

        public NpcSalesService SalesService { get; set; }
        public CheckoutManagerService CheckoutManager { get; set; }

        public IndoorVisitorService(IMonitor monitor, IModHelper helper, ModConfig config, IndoorStoreTrackingService storeTrackingService, StoreStatsService storeStatsService)
        {
            _monitor = monitor;
            _helper = helper;
            _config = config;
            _storeTrackingService = storeTrackingService;
            _storeStatsService = storeStatsService;

            helper.Events.GameLoop.DayStarted += OnDayStarted;
            helper.Events.GameLoop.TimeChanged += OnTimeChanged;
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
            helper.Events.Display.RenderedWorld += OnRenderedWorld;
        }

        public bool TryAddToCart(NPC npc, Item item, int price, int taste, Farmer seller)
        {
            if (_activeVisitors.TryGetValue(npc, out var data))
            {
                data.ShoppingCart.Add(new CartItem { Item = item, Price = price, Taste = taste, Seller = seller });
                return true;
            }
            return false;
        }

        private void OnDayStarted(object sender, DayStartedEventArgs e)
        {
            _activeVisitors.Clear();
            _cachedStoreCapacity.Clear();
            _cachedEntryWarps.Clear();
            _wanderCheckIndex = 0;
        }

        private void OnTimeChanged(object sender, TimeChangedEventArgs e)
        {
            if (!Context.IsWorldReady || Game1.timeOfDay >= 2500)
                return;

            // Clear cache every 6 hours (1200, 1800, 2400)
            if (Game1.timeOfDay == 1200 || Game1.timeOfDay == 1800 || Game1.timeOfDay == 2400)
            {
                _cachedStoreCapacity.Clear();
            }

            // 1. Process departures
            if (_activeVisitors.Count > 0)
            {
                var forcedDepartures = new List<NPC>();

                foreach (var kvp in _activeVisitors.ToList())
                {
                    NPC npc = kvp.Key;
                    VisitorData data = kvp.Value;

                    int forceDepartureTime = NpcScheduleHelper.ConvertToHour(data.DepartureTime + _config.IndoorStoreVisitorStayTime);
                    if (npc.currentLocation != null && _storeStatsService.StoreStats.TryGetValue(npc.currentLocation.NameOrUniqueName, out var stats))
                    {
                        forceDepartureTime = stats.CloseHour + 200;
                    }

                    if (Game1.timeOfDay >= forceDepartureTime)
                    {
                        // Force remove if 2 hours past close time
                        forcedDepartures.Add(npc);
                    }
                    else if (Game1.timeOfDay >= data.DepartureTime && !data.IsDeparting)
                    {
                        // Don't interrupt a fitting booth or reading visit — let it finish naturally.
                        if (data.IsInFittingBooth || data.IsHeadingToBooth || data.IsSittingToRead || data.IsHeadingToRead)
                            continue;

                        if (data.ShoppingCart.Count > 0 && CheckoutManager != null)
                        {
                            if (CheckoutManager.TryReserveSlot(npc.currentLocation, npc, out int slotIndex, out Furniture checkout))
                            {
                                data.IsDeparting = true;
                                data.IsCheckingOut = true;
                                data.AssignedCheckout = checkout;
                                data.CheckoutSlotIndex = slotIndex;
                                data.CheckoutSlotReached = false;
                                data.HasStartedCheckoutEmote = false;

                                Vector2 slotTile = CheckoutManager.GetSlotTile(checkout, slotIndex);

                                NpcScheduleHelper.CleanNpc(npc);
                                npc.controller = new StardewValley.Pathfinding.PathFindController(npc, npc.currentLocation, new Point((int)slotTile.X, (int)slotTile.Y), -1, new StardewValley.Pathfinding.PathFindController.endBehavior(OnCheckoutSlotReached));
                                _monitor.Log($"{npc.Name} is going to checkout at {npc.currentLocation.NameOrUniqueName}.", LogLevel.Trace);
                                continue;
                            }
                            else
                            {
                                int closeHour = 2400;
                                if (npc.currentLocation != null && _storeStatsService.StoreStats.TryGetValue(npc.currentLocation.NameOrUniqueName, out var localStats))
                                {
                                    closeHour = localStats.CloseHour;
                                }

                                if (Game1.timeOfDay < closeHour + 100)
                                {
                                    // Wait for a checkout slot to open up
                                    continue;
                                }
                                else
                                {
                                    data.IsDeparting = true;
                                    HandleNoEmployeeDeparture(npc, data, isForced: false);
                                }
                            }
                        }
                        else
                        {
                            data.IsDeparting = true;
                            // Standard departure: Walk back to spawn tile
                            NpcScheduleHelper.CleanNpc(npc);
                            npc.controller = new StardewValley.Pathfinding.PathFindController(npc, npc.currentLocation, new Point((int)data.SpawnTile.X, (int)data.SpawnTile.Y), -1, new StardewValley.Pathfinding.PathFindController.endBehavior(OnDepartureWalkFinished));
                            _monitor.Log($"{npc.Name} is departing {npc.currentLocation.NameOrUniqueName}. Walking to exit...", LogLevel.Trace);
                        }
                    }
                }

                foreach (var npc in forcedDepartures)
                {
                    _monitor.Log($"{npc.Name} overstayed their visit limit. Forcing departure.", LogLevel.Trace);
                    if (_activeVisitors.TryGetValue(npc, out var data))
                    {
                        if (data.ShoppingCart.Count > 0)
                        {
                            HandleNoEmployeeDeparture(npc, data, isForced: true);
                        }

                        if (data.IsCheckingOut && CheckoutManager != null)
                        {
                            CheckoutManager.ReleaseSlot(npc.currentLocation, data.AssignedCheckout.TileLocation, npc);
                        }
                        
                        if (data.IsSittingToRead && data.AssignedChair != null)
                        {
                            if (!IsChairStillOccupied(data.AssignedChair, data))
                            {
                                data.AssignedChair.sittingFarmers.Remove(Game1.player.UniqueMultiplayerID);
                            }
                        }
                    }
                    _activeVisitors.Remove(npc);
                    DepartVisitor(npc);
                }
            }

            // 2. Process new spawns
            foreach (var location in _storeTrackingService.ActiveStoreLocations)
            {
                if (!_storeStatsService.StoreStats.TryGetValue(location.NameOrUniqueName, out var stats))
                {
                    continue; // Skip if we don't have stats (and thus don't have hours)
                }

                // Customer only spawn when time > open and < close, not equal
                if (Game1.timeOfDay > stats.OpenHour && Game1.timeOfDay < stats.CloseHour)
                {
                    // Check if there is at least one active cashier in the shop
                    bool hasActiveCashier = false;
                    if (_storeTrackingService.EmployeeService != null && _storeTrackingService.EmployeeService.StoreEmployees.TryGetValue(location.NameOrUniqueName, out var record))
                    {
                        foreach (var npcName in record.HiredNPCs.Values)
                        {
                            NPC employee = Game1.getCharacterFromName(npcName);
                            if (employee != null && employee.currentLocation == location)
                            {
                                hasActiveCashier = true;
                                break;
                            }
                        }
                    }

                    if (!hasActiveCashier)
                        continue;

                    int currentVisitors = _activeVisitors.Keys.Count(n => n.currentLocation == location);
                    int maxVisitors = GetStoreCapacity(location);

                    if (currentVisitors < maxVisitors)
                    {
                        if (Game1.random.NextDouble() <= _config.IndoorStoreVisitorChance)
                        {
                            SpawnVisitor(location, stats.CloseHour);
                        }
                    }
                }
            }
        }

        private void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            // Stagger checks to 1 NPC every 20 ticks
            if (!Context.IsWorldReady || !e.IsMultipleOf(20)) return;

            if (_activeVisitors.Count == 0) return;

            if (_wanderCheckIndex >= _activeVisitors.Count)
            {
                _wanderCheckIndex = 0;
            }

            var npc = _activeVisitors.Keys.ElementAt(_wanderCheckIndex);
            _wanderCheckIndex++;

            if (npc.currentLocation == null) return;

            // ── Per-visitor tick: always run for timers (booth etc.), before the idle check ──
            // Each NPC is processed once per round-robin cycle of N visitors × 20 ticks.
            // Multiply by visitor count so timers advance at the correct real-world rate.
            if (_activeVisitors.TryGetValue(npc, out var tickData))
            {
                float realDeltaMs = 333f * Math.Max(1, _activeVisitors.Count);
                ShopBehaviorService?.GetBehaviorForLocation(npc.currentLocation)
                    .OnVisitorTick(npc, npc.currentLocation, tickData, realDeltaMs);
            }

            if (npc.isMoving())
            {
                return;
            }

            if (_activeVisitors.TryGetValue(npc, out var visitorData) && visitorData.IsCheckingOut)
            {
                if (CheckoutManager != null && visitorData.AssignedCheckout != null)
                {
                    int currentPos = CheckoutManager.GetQueuePosition(npc.currentLocation, visitorData.AssignedCheckout.TileLocation, npc);
                    if (currentPos != -1 && currentPos != visitorData.CheckoutSlotIndex)
                    {
                        // Moved up in queue
                        visitorData.CheckoutSlotIndex = currentPos;
                        visitorData.CheckoutSlotReached = false;
                        visitorData.HasStartedCheckoutEmote = false;
                        Vector2 newSlotTile = CheckoutManager.GetSlotTile(visitorData.AssignedCheckout, currentPos);

                        NpcScheduleHelper.CleanNpc(npc);
                        npc.controller = new StardewValley.Pathfinding.PathFindController(npc, npc.currentLocation, new Point((int)newSlotTile.X, (int)newSlotTile.Y), -1, new StardewValley.Pathfinding.PathFindController.endBehavior(OnCheckoutSlotReached));
                        return;
                    }
                }

                if (visitorData.CheckoutSlotReached && visitorData.CheckoutSlotIndex == 0)
                {
                    if (!visitorData.HasStartedCheckoutEmote)
                    {
                        visitorData.HasStartedCheckoutEmote = true;
                        npc.doEmote(Game1.random.NextDouble() < 0.5 ? 20 : 56);

                        if (_storeTrackingService.EmployeeService != null && visitorData.AssignedCheckout != null)
                        {
                            _storeTrackingService.EmployeeService.TriggerCheckoutReaction(npc.currentLocation, visitorData.AssignedCheckout.TileLocation);
                        }
                    }

                    // Each NPC update covers N×20 ticks of real time (N = active visitor count).
                    visitorData.CheckoutWaitTimer += 333f * Math.Max(1, _activeVisitors.Count);

                    float requiredWait = visitorData.ShoppingCart.Count * 1500f; // 1.5 seconds per item
                    if (visitorData.CheckoutWaitTimer >= requiredWait)
                    {
                        // Checkout complete
                        if (SalesService != null)
                        {
                            SalesService.ProcessDeferredPurchases(npc, visitorData.ShoppingCart, npc.currentLocation);
                        }
                        visitorData.ShoppingCart.Clear();

                        if (CheckoutManager != null)
                        {
                            CheckoutManager.ReleaseSlot(npc.currentLocation, visitorData.AssignedCheckout.TileLocation, npc);
                        }

                        visitorData.IsCheckingOut = false;

                        // Proceed to exit
                        NpcScheduleHelper.CleanNpc(npc);
                        npc.controller = new StardewValley.Pathfinding.PathFindController(npc, npc.currentLocation, new Point((int)visitorData.SpawnTile.X, (int)visitorData.SpawnTile.Y), -1, new StardewValley.Pathfinding.PathFindController.endBehavior(OnDepartureWalkFinished));
                    }
                }
                return;
            }

            if (_activeVisitors.TryGetValue(npc, out var vData))
            {
                vData.RandomEmoteTimer += 333f; // staggered every 20 ticks
                if (vData.RandomEmoteTimer >= vData.RandomEmoteThreshold)
                {
                    vData.RandomEmoteTimer = 0f;
                    vData.RandomEmoteThreshold = Game1.random.Next(15000, 30000);
                    int[] emotes = new int[] { 20, 56, 32 };
                    npc.doEmote(emotes[Game1.random.Next(emotes.Length)]);
                }
            }

            // Check upcoming schedule
            bool hasUpcomingSchedule = false;
            if (npc.Schedule != null)
            {
                int timeLimit = NpcScheduleHelper.ConvertToHour(Game1.timeOfDay + 30);
                foreach (var key in npc.Schedule.Keys)
                {
                    if (key > Game1.timeOfDay && key <= timeLimit)
                    {
                        hasUpcomingSchedule = true;
                        break;
                    }
                }
            }

            if (hasUpcomingSchedule)
            {
                return;
            }

            if (npc.timerSinceLastMovement >= 7000f) // 7 seconds
            {
                // ── Theme hook: let the active theme try to handle movement first ──
                bool customHandled = ShopBehaviorService != null &&
                    ShopBehaviorService.GetBehaviorForLocation(npc.currentLocation)
                        .TryHandleCustomCustomerWander(npc, npc.currentLocation, vData);

                if (!customHandled)
                {
                    // Default: pick a random walkable tile
                    Vector2 target = GetRandomWalkableTile(npc.currentLocation);
                    if (target != Vector2.Zero)
                    {
                        npc.controller = new StardewValley.Pathfinding.PathFindController(npc, npc.currentLocation, new Point((int)target.X, (int)target.Y), -1);
                        npc.timerSinceLastMovement = 0f;
                    }
                }
            }
        }

        private void OnRenderedWorld(object sender, RenderedWorldEventArgs e)
        {
            if (!Context.IsWorldReady || Game1.currentLocation == null) return;

            foreach (var kvp in _activeVisitors)
            {
                NPC npc = kvp.Key;
                VisitorData data = kvp.Value;

                if (npc.currentLocation == Game1.currentLocation && data.ShoppingCart.Count > 0 && !(data.CheckoutSlotReached && data.CheckoutSlotIndex == 0))
                {
                    string text = data.ShoppingCart.Count.ToString();
                    Vector2 textSize = Game1.smallFont.MeasureString(text);

                    Vector2 position = Game1.GlobalToLocal(Game1.viewport, npc.Position);
                    position.X += (64f - textSize.X) / 2f; // Center horizontally over the tile (64x64)
                    position.Y -= 90f; // Above the NPC's head

                    // Draw a small background for visibility
                    Rectangle bgRect = new Rectangle((int)position.X - 4, (int)position.Y - 4, (int)textSize.X + 8, (int)textSize.Y + 8);
                    e.SpriteBatch.Draw(Game1.fadeToBlackRect, bgRect, Color.White * 0.8f);

                    Utility.drawTextWithShadow(e.SpriteBatch, text, Game1.smallFont, position, Game1.textColor);
                }
            }
        }

        private int GetShopLevel(GameLocation location)
        {
            // Placeholder: currently returning 1. In the future, this can pull from a custom save data or tracking service.
            return 1;
        }

        private int GetStoreCapacity(GameLocation location)
        {
            if (_cachedStoreCapacity.TryGetValue(location, out int capacity))
            {
                return capacity;
            }

            int calculated = CalculateStoreCapacity(location);
            _cachedStoreCapacity[location] = calculated;
            return calculated;
        }

        private int CalculateStoreCapacity(GameLocation location)
        {
            var scores = GetStoreScores(location);
            return scores.MaxCapacity;
        }

        public StoreCapacityScores GetStoreScores(GameLocation location)
        {
            if (location.Map == null || location.Map.Layers.Count == 0)
                return new StoreCapacityScores { MaxCapacity = 2 };

            int area = location.Map.Layers[0].LayerWidth * location.Map.Layers[0].LayerHeight;

            // Base Capacity
            int baseCapacity = Math.Max(1, area / 70);

            var stats = GetStoreStatistics(location);
            int numSellingNodes = stats.SellingNodes;
            int numDecorations = stats.Decorations;

            // Calculate ratios
            int shopLevel = GetShopLevel(location);
            float levelRatio = Math.Min(1.0f, (shopLevel - 1) / 4.0f); // Level 1 -> 0, Level 5 -> 1.0
            float nodeRatio = Math.Min(1.0f, (numSellingNodes * 15.0f) / area);
            float decoRatio = Math.Min(1.0f, (numDecorations * 20.0f) / area);

            float levelScore = levelRatio * 0.5f;
            float sellingScore = nodeRatio * 0.3f;
            float decorationScore = decoRatio * 0.2f;

            // Bonus Score (0.0 to 1.0)
            float bonusScore = levelScore + sellingScore + decorationScore;

            // Calculate final limit
            int maxLimit = baseCapacity + (int)(baseCapacity * bonusScore);
            maxLimit = Math.Min(20, maxLimit);

            var result = new StoreCapacityScores
            {
                ShopLevel = shopLevel,
                ShopLevelScore = levelScore,
                SellingScore = sellingScore,
                DecorationScore = decorationScore,
                BonusScore = bonusScore,
                MaxCapacity = maxLimit
            };

            // ── Theme hook: let the active behavior apply its own buffs ──
            ShopBehaviorService?.GetBehaviorForLocation(location)?.ApplyShopBuffs(result, location);

            return result;
        }

        public (int SellingNodes, int Decorations) GetStoreStatistics(GameLocation location)
        {
            int numSellingNodes = 0;
            int numDecorations = 0;

            foreach (var furniture in location.furniture)
            {
                if (furniture.furniture_type.Value == StardewValley.Objects.Furniture.table && furniture.heldObject.Value != null)
                {
                    numSellingNodes++;
                }
                else
                {
                    numDecorations++;
                }
            }

            foreach (var obj in location.Objects.Values)
            {
                if (obj is StardewValley.Objects.Mannequin mannequin)
                {
                    if (mannequin.hat.Value != null || mannequin.shirt.Value != null || mannequin.pants.Value != null || mannequin.boots.Value != null)
                    {
                        numSellingNodes++;
                    }
                }
            }

            return (numSellingNodes, numDecorations);
        }

        private void SpawnVisitor(GameLocation location, int closeHour)
        {
            var eligibleNpcs = Utility.getAllCharacters().Where(npc =>
                NpcScannerService.IsAllowedCustomer(npc, _storeTrackingService) &&
                !_activeVisitors.ContainsKey(npc) &&
                npc.currentLocation != location
            ).ToList();

            if (eligibleNpcs.Count == 0)
                return;

            var visitor = eligibleNpcs[Game1.random.Next(eligibleNpcs.Count)];

            Vector2 spawnTile = GetEntryTile(location);

            // Fallback if no warp found or no walkable tiles near warp
            if (spawnTile == Vector2.Zero)
            {
                spawnTile = GetRandomWalkableTile(location);
            }

            if (spawnTile == Vector2.Zero)
            {
                _monitor.Log($"Failed to find walkable tile for visitor {visitor.Name} in {location.NameOrUniqueName}", LogLevel.Trace);
                return;
            }

            // Halt and clear schedule
            NpcScheduleHelper.CleanNpc(visitor);

            Game1.warpCharacter(visitor, location, spawnTile);

            // Immediate wander
            Vector2 wanderTarget = GetRandomWalkableTile(location);
            if (wanderTarget != Vector2.Zero)
            {
                visitor.controller = new StardewValley.Pathfinding.PathFindController(visitor, location, new Point((int)wanderTarget.X, (int)wanderTarget.Y), -1);
            }

            // Add to tracking
            int intendedStay = NpcScheduleHelper.ConvertToHour(Game1.timeOfDay + _config.IndoorStoreVisitorStayTime);
            int departureTime = Math.Min(intendedStay, closeHour);
            _activeVisitors[visitor] = new VisitorData
            {
                DepartureTime = departureTime,
                SpawnTile = spawnTile,
                IsDeparting = false
            };

            _storeStatsService.IncrementVisitor(location);

            _monitor.Log($"Spawned visitor {visitor.Name} at {location.NameOrUniqueName}. They will leave at {departureTime}.", LogLevel.Info);
        }

        public bool IsVisitorDeparting(NPC npc)
        {
            if (_activeVisitors.TryGetValue(npc, out var data))
            {
                return data.IsDeparting;
            }
            return false;
        }

        public bool IsChairStillOccupied(Furniture chair, VisitorData excludingData)
        {
            return _activeVisitors.Values.Any(d => d != excludingData && (d.IsSittingToRead || d.IsHeadingToRead) && d.AssignedChair == chair);
        }

        /// <summary>
        /// Finds a free Fitting Booth slot in the given location and reserves it for <paramref name="requesterData"/>.
        /// Returns <c>true</c> and sets <paramref name="tile"/> to the walkable bottom-centre slot (X+1, Y+1)
        /// if a free booth is found; otherwise returns <c>false</c>.
        /// </summary>
        public bool TryReserveBoothTile(GameLocation location, VisitorData requesterData, out Vector2 tile)
        {
            tile = Vector2.Zero;
            if (location == null) return false;

            // Build the set of tiles already claimed by other visitors
            var occupiedTiles = new HashSet<Vector2>();
            foreach (var d in _activeVisitors.Values)
            {
                if (d != requesterData && (d.IsHeadingToBooth || d.IsInFittingBooth))
                    occupiedTiles.Add(d.FittingBoothTile);
            }

            const string BOOTH_ID = "d5a1lamdtd.MarketTown_FittingBooth";
            foreach (var furniture in location.furniture)
            {
                if (furniture.ItemId != BOOTH_ID) continue;

                // Walkable slot = one tile right and one tile down from the furniture's TileLocation
                Vector2 slotTile = new Vector2(furniture.TileLocation.X + 1, furniture.TileLocation.Y + 1);

                if (!occupiedTiles.Contains(slotTile))
                {
                    tile = slotTile;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Finds a free chair/bench slot in the given location and reserves it for <paramref name="requesterData"/>.
        /// Returns <c>true</c> and sets <paramref name="tile"/>, <paramref name="assignedChair"/>, and <paramref name="slotIndex"/>
        /// if a free chair slot is found; otherwise returns <c>false</c>.
        /// </summary>
        public bool TryReserveChairTile(GameLocation location, VisitorData requesterData, out Vector2 tile, out Furniture assignedChair, out int slotIndex)
        {
            tile = Vector2.Zero;
            assignedChair = null;
            slotIndex = -1;
            if (location == null) return false;

            // Build the set of (furniture, slotIndex) already claimed by other visitors
            var occupiedSlots = new HashSet<(Furniture, int)>();
            foreach (var d in _activeVisitors.Values)
            {
                if (d != requesterData && (d.IsHeadingToRead || d.IsSittingToRead) && d.AssignedChair != null)
                {
                    occupiedSlots.Add((d.AssignedChair, d.AssignedChairSlotIndex));
                }
            }

            foreach (var furniture in location.furniture)
            {
                int capacity = furniture.GetSeatCapacity();
                if (capacity > 0)
                {
                    // Check slots 0 to capacity - 1
                    for (int i = 0; i < capacity; i++)
                    {
                        if (!occupiedSlots.Contains((furniture, i)))
                        {
                            // Make sure it's not occupied by a real farmer either
                            if (furniture.sittingFarmers != null && furniture.sittingFarmers.Values.Contains(i))
                                continue;

                            assignedChair = furniture;
                            slotIndex = i;
                            
                            // To pathfind near the chair, we find an adjacent open tile
                            // We use the actual seat position tile if we can, or adjacent
                            List<Vector2> seatPositions = furniture.GetSeatPositions(false);
                            if (i < seatPositions.Count)
                            {
                                tile = seatPositions[i];
                            }
                            else
                            {
                                tile = furniture.TileLocation; // Fallback
                            }
                            
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private void HandleNoEmployeeDeparture(NPC npc, VisitorData data, bool isForced)
        {
            double stealChance = isForced ? 0.25 : 0.50;
            if (Game1.random.NextDouble() <= stealChance)
            {
                // Steal (leave, no pay)
                _monitor.Log($"{npc.Name} couldn't checkout and stole {data.ShoppingCart.Count} items!", LogLevel.Info);
            }
            else
            {
                // Drop all items
                foreach (var cartItem in data.ShoppingCart)
                {
                    Game1.createItemDebris(cartItem.Item, npc.Position, Game1.random.Next(4), npc.currentLocation);
                }
                _monitor.Log($"{npc.Name} couldn't checkout and dropped {data.ShoppingCart.Count} items on the floor.", LogLevel.Info);
            }

            data.ShoppingCart.Clear();
        }

        private void OnCheckoutSlotReached(Character c, GameLocation location)
        {
            if (c is NPC npc && _activeVisitors.TryGetValue(npc, out var data) && data.IsCheckingOut)
            {
                data.CheckoutSlotReached = true;
                data.CheckoutWaitTimer = 0f;
                npc.faceDirection(3); // Face left towards the register
            }
        }

        private void OnDepartureWalkFinished(Character c, GameLocation location)
        {
            if (c is NPC npc && _activeVisitors.ContainsKey(npc))
            {
                _activeVisitors.Remove(npc);
                DepartVisitor(npc);
            }
        }

        private void DepartVisitor(NPC npc)
        {
            NpcScheduleHelper.CleanNpc(npc);
            npc.reloadDefaultLocation();
            npc.TryLoadSchedule();
            var schedule = npc.Schedule;
            NpcScheduleHelper.CleanNpc(npc);

            string lastLocation = npc.DefaultMap;
            Vector2 lastPosition = npc.DefaultPosition / 64f;
            int lastFacing = npc.DefaultFacingDirection;

            if (schedule == null)
            {
                Game1.warpCharacter(npc, lastLocation, lastPosition);
                return;
            }

            foreach (var piece in schedule)
            {
                if (piece.Key > Game1.timeOfDay)
                {
                    Game1.warpCharacter(npc, lastLocation, lastPosition);
                    npc.faceDirection(lastFacing);
                    npc.TryLoadSchedule();
                    _monitor.Log($"{npc.Name} finished visiting the store and warped to {lastLocation} at {lastPosition}.", LogLevel.Info);
                    return;
                }

                lastLocation = piece.Value.targetLocationName;
                lastPosition = piece.Value.targetTile.ToVector2();
                lastFacing = piece.Value.facingDirection;
            }

            Game1.warpCharacter(npc, lastLocation, lastPosition);
            npc.faceDirection(lastFacing);
            npc.TryLoadSchedule();

            _monitor.Log($"{npc.Name} finished visiting the store and warped to {lastLocation} at {lastPosition}.", LogLevel.Info);
        }

        private Vector2 GetRandomWalkableTile(GameLocation location)
        {
            for (int i = 0; i < 50; i++)
            {
                int x = Game1.random.Next(location.Map.Layers[0].LayerWidth);
                int y = Game1.random.Next(location.Map.Layers[0].LayerHeight);
                Vector2 candidate = new Vector2(x, y);

                if (!location.IsTileBlockedBy(candidate, ignorePassables: CollisionMask.Flooring | CollisionMask.Furniture))
                {
                    return candidate;
                }
            }
            return Vector2.Zero;
        }

        private Warp FindBestEntryWarp(GameLocation startLoc)
        {
            if (startLoc == null || startLoc.warps.Count == 0) return null;

            var queue = new Queue<(GameLocation loc, Warp initialWarp, int depth)>();
            var visited = new HashSet<string>();

            foreach (var w in startLoc.warps)
            {
                var nextLoc = Game1.getLocationFromName(w.TargetName);
                if (nextLoc != null)
                {
                    queue.Enqueue((nextLoc, w, 1));
                }
            }

            visited.Add(startLoc.NameOrUniqueName);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();

                if (current.loc.IsOutdoors)
                {
                    return current.initialWarp;
                }

                if (visited.Contains(current.loc.NameOrUniqueName)) continue;
                visited.Add(current.loc.NameOrUniqueName);

                foreach (var w in current.loc.warps)
                {
                    var nextLoc = Game1.getLocationFromName(w.TargetName);
                    if (nextLoc != null && !visited.Contains(nextLoc.NameOrUniqueName))
                    {
                        queue.Enqueue((nextLoc, current.initialWarp, current.depth + 1));
                    }
                }
            }

            return startLoc.warps[0];
        }

        private Vector2 GetWalkableTileNear(GameLocation location, Point center, int radius)
        {
            var candidates = new List<Vector2>();

            for (int x = center.X - radius; x <= center.X + radius; x++)
            {
                for (int y = center.Y - radius; y <= center.Y + radius; y++)
                {
                    // Don't spawn exactly on the warp itself
                    if (x == center.X && y == center.Y) continue;

                    // Bounds check
                    if (x < 0 || y < 0 || x >= location.Map.Layers[0].LayerWidth || y >= location.Map.Layers[0].LayerHeight) continue;

                    Vector2 tile = new Vector2(x, y);

                    if (!location.IsTileBlockedBy(tile, ignorePassables: CollisionMask.Flooring | CollisionMask.Furniture))
                    {
                        // Ensure this tile isn't another warp
                        bool hasWarp = location.warps.Any(w => w.X == x && w.Y == y);
                        if (!hasWarp)
                        {
                            candidates.Add(tile);
                        }
                    }
                }
            }

            if (candidates.Count > 0)
            {
                return candidates[Game1.random.Next(candidates.Count)];
            }

            return Vector2.Zero;
        }

        public int GetActiveVisitorCount(GameLocation location)
        {
            if (location == null) return 0;
            return _activeVisitors.Keys.Count(n => n.currentLocation == location);
        }

        public Vector2 GetEntryTile(GameLocation location)
        {
            if (!_cachedEntryWarps.TryGetValue(location, out Warp bestWarp))
            {
                bestWarp = FindBestEntryWarp(location);
                _cachedEntryWarps[location] = bestWarp;
            }

            if (bestWarp != null)
            {
                return GetWalkableTileNear(location, new Point(bestWarp.X, bestWarp.Y), 3);
            }
            return Vector2.Zero;
        }
    }
}
