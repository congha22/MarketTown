using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using MarketTown.Framework.UI;

namespace MarketTown.Framework.Services
{
    public class IndoorStoreTrackingService
    {
        private readonly IMonitor _monitor;
        private readonly IModHelper _helper;
        private readonly SalesTrackingService _salesTrackingService;

        private const string REGISTER_ID = "d5a1lamdtd.MarketTown_StoreRegister";

        public HashSet<GameLocation> ActiveStoreLocations { get; } = new HashSet<GameLocation>();

        public IndoorStoreTrackingService(IMonitor monitor, IModHelper helper, SalesTrackingService salesTrackingService)
        {
            _monitor = monitor;
            _helper = helper;
            _salesTrackingService = salesTrackingService;

            helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
            helper.Events.World.ObjectListChanged += OnObjectListChanged;
            helper.Events.Input.ButtonPressed += OnButtonPressed;
            helper.Events.Display.RenderedHud += OnRenderedHud;
        }

        private void OnSaveLoaded(object sender, SaveLoadedEventArgs e)
        {
            ActiveStoreLocations.Clear();

            // Scan all locations for the register (Utility.ForEachLocation safely searches all interiors/sheds/cabins)
            Utility.ForEachLocation((location) =>
            {
                if (!location.IsOutdoors && HasRegister(location))
                {
                    ActiveStoreLocations.Add(location);
                    _monitor.Log($"Found indoor store at {location.NameOrUniqueName}", LogLevel.Trace);
                }
                return true; // continue iteration
            });
        }

        private void OnObjectListChanged(object sender, ObjectListChangedEventArgs e)
        {
            if (e.Location.IsOutdoors) return;

            bool isRegisterPlaced = e.Added.Any(p => p.Value.bigCraftable.Value && p.Value.ItemId == REGISTER_ID);
            bool isRegisterRemoved = e.Removed.Any(p => p.Value.bigCraftable.Value && p.Value.ItemId == REGISTER_ID);

            if (isRegisterPlaced)
            {
                ActiveStoreLocations.Add(e.Location);
                _monitor.Log($"Store register placed at {e.Location.NameOrUniqueName}, added to active stores.", LogLevel.Trace);
            }
            else if (isRegisterRemoved)
            {
                // Verify if it was the last one in this location
                if (!HasRegister(e.Location))
                {
                    ActiveStoreLocations.Remove(e.Location);
                    _monitor.Log($"Last store register removed at {e.Location.NameOrUniqueName}, removed from active stores.", LogLevel.Trace);
                }
            }
        }

        public StoreStatsService StoreStatsService { get; set; }
        public IndoorVisitorService VisitorService { get; set; }

        private void OnButtonPressed(object sender, ButtonPressedEventArgs e)
        {
            if (!Context.IsWorldReady || Game1.activeClickableMenu != null) return;

            if (e.Button.IsActionButton())
            {
                Vector2 tile = e.Cursor.GrabTile;
                if (Game1.currentLocation.objects.TryGetValue(tile, out StardewValley.Object obj))
                {
                    if (obj.bigCraftable.Value && obj.ItemId == REGISTER_ID)
                    {
                        if (StoreStatsService != null && VisitorService != null)
                        {
                            Game1.activeClickableMenu = new StoreManagerMenu(StoreStatsService, VisitorService, Game1.currentLocation, _helper);
                            _helper.Input.Suppress(e.Button);
                        }
                    }
                }
            }
        }

        private void OnRenderedHud(object sender, RenderedHudEventArgs e)
        {
            if (!Context.IsWorldReady || Game1.activeClickableMenu != null) return;

            Vector2 tile = Game1.currentCursorTile;
            if (Game1.currentLocation.objects.TryGetValue(tile, out StardewValley.Object obj))
            {
                if (obj.bigCraftable.Value && obj.ItemId == REGISTER_ID)
                {
                    // Show interaction cursor (4 = speech bubble / interact cursor)
                    Game1.mouseCursor = 4;
                }
            }
        }

        private bool HasRegister(GameLocation location)
        {
            return location.objects.Values.Any(obj => obj.bigCraftable.Value && obj.ItemId == REGISTER_ID);
        }
    }
}
