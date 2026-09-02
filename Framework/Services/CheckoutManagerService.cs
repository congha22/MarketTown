using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Objects;

namespace MarketTown.Framework.Services
{
    public class CheckoutManagerService
    {
        private readonly IMonitor _monitor;
        private readonly StoreEmployeeService _employeeService;

        // Tracks which slots are reserved by which NPC (for a specific checkout tile location in a game location)
        // Key is string: LocationName_CheckoutX_CheckoutY_SlotX_SlotY
        private readonly Dictionary<string, NPC> _reservedSlots = new Dictionary<string, NPC>();

        public CheckoutManagerService(IMonitor monitor, StoreEmployeeService employeeService, IModHelper helper)
        {
            _monitor = monitor;
            _employeeService = employeeService;

            helper.Events.GameLoop.DayStarted += OnDayStarted;
        }

        private void OnDayStarted(object sender, DayStartedEventArgs e)
        {
            _reservedSlots.Clear();
        }

        public bool TryReserveSlot(GameLocation location, NPC npc, out Vector2 slotTile, out Furniture assignedCheckout)
        {
            slotTile = Vector2.Zero;
            assignedCheckout = null;

            if (location == null || npc == null) return false;

            // Find all checkouts in the location that have an employee assigned
            var checkouts = new List<Furniture>();
            foreach (var f in location.furniture)
            {
                if (f.ItemId == "d5a1lamdtd.MarketTown_CheckoutSmall" || f.ItemId == "d5a1lamdtd.MarketTown_CheckoutLarge")
                {
                    string employeeName = _employeeService.GetHiredEmployee(location, f.TileLocation);
                    if (!string.IsNullOrEmpty(employeeName))
                    {
                        // Make sure the employee is actually in the location and not going home
                        NPC employee = Game1.getCharacterFromName(employeeName);
                        if (employee != null && employee.currentLocation == location)
                        {
                            checkouts.Add(f);
                        }
                    }
                }
            }

            if (checkouts.Count == 0) return false;

            // Gather all available slots across all manned checkouts
            var availableSlots = new List<(Furniture Checkout, Vector2 Slot, int Priority)>();

            foreach (var checkout in checkouts)
            {
                var slots = GetCheckoutSlots(checkout);
                for (int i = 0; i < slots.Count; i++)
                {
                    Vector2 s = slots[i];
                    string key = GetSlotKey(location, checkout.TileLocation, s);
                    
                    // If slot is not reserved, or reserved by the same NPC (retry)
                    if (!_reservedSlots.TryGetValue(key, out var reservedBy) || reservedBy == npc)
                    {
                        // Priority is the index (lowest empty tile is i=0, i=1, etc. which translates to highest Y or lowest index depending on how we ordered them)
                        // Wait, user said "mark the lowest empty tile as I am going to that slot".
                        // "lowest" could mean visually lowest (highest Y) or lowest index. Let's use the list order where 0 is highest priority.
                        availableSlots.Add((checkout, s, i));
                    }
                }
            }

            if (availableSlots.Count > 0)
            {
                // Sort by priority (lowest index), then randomly pick among those with the same priority
                var bestPriority = availableSlots.Min(s => s.Priority);
                var bestSlots = availableSlots.Where(s => s.Priority == bestPriority).ToList();
                var selected = bestSlots[Game1.random.Next(bestSlots.Count)];

                assignedCheckout = selected.Checkout;
                slotTile = selected.Slot;

                string key = GetSlotKey(location, assignedCheckout.TileLocation, slotTile);
                _reservedSlots[key] = npc;
                
                return true;
            }

            return false;
        }

        public void ReleaseSlot(GameLocation location, Vector2 checkoutTile, Vector2 slotTile, NPC npc)
        {
            if (location == null) return;
            string key = GetSlotKey(location, checkoutTile, slotTile);
            if (_reservedSlots.TryGetValue(key, out var reservedBy) && reservedBy == npc)
            {
                _reservedSlots.Remove(key);
            }
        }

        public void ReleaseAllSlotsForNpc(NPC npc)
        {
            var keysToRemove = _reservedSlots.Where(kvp => kvp.Value == npc).Select(kvp => kvp.Key).ToList();
            foreach (var key in keysToRemove)
            {
                _reservedSlots.Remove(key);
            }
        }

        private List<Vector2> GetCheckoutSlots(Furniture checkout)
        {
            var slots = new List<Vector2>();
            if (checkout.ItemId == "d5a1lamdtd.MarketTown_CheckoutLarge")
            {
                // X+3, Y+0,1,2
                slots.Add(new Vector2(checkout.TileLocation.X + 3, checkout.TileLocation.Y + 2)); // Highest Y (visually lowest) first? Or Y+0 first?
                slots.Add(new Vector2(checkout.TileLocation.X + 3, checkout.TileLocation.Y + 1));
                slots.Add(new Vector2(checkout.TileLocation.X + 3, checkout.TileLocation.Y + 0));
            }
            else if (checkout.ItemId == "d5a1lamdtd.MarketTown_CheckoutSmall")
            {
                // X+2, Y+0,1
                slots.Add(new Vector2(checkout.TileLocation.X + 2, checkout.TileLocation.Y + 1));
                slots.Add(new Vector2(checkout.TileLocation.X + 2, checkout.TileLocation.Y + 0));
            }
            return slots; // Ordered by priority (highest Y first, which is lowest tile on screen)
        }

        private string GetSlotKey(GameLocation location, Vector2 checkoutTile, Vector2 slotTile)
        {
            return $"{location.NameOrUniqueName}_{checkoutTile.X}_{checkoutTile.Y}_{slotTile.X}_{slotTile.Y}";
        }
    }
}
