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

        // Key is string: LocationName_CheckoutX_CheckoutY
        private readonly Dictionary<string, List<NPC>> _checkoutQueues = new Dictionary<string, List<NPC>>();

        public CheckoutManagerService(IMonitor monitor, StoreEmployeeService employeeService, IModHelper helper)
        {
            _monitor = monitor;
            _employeeService = employeeService;

            helper.Events.GameLoop.DayStarted += OnDayStarted;
        }

        private void OnDayStarted(object sender, DayStartedEventArgs e)
        {
            _checkoutQueues.Clear();
        }

        public bool TryReserveSlot(GameLocation location, NPC npc, out int slotIndex, out Furniture assignedCheckout)
        {
            slotIndex = -1;
            assignedCheckout = null;

            if (location == null || npc == null) return false;

            var checkouts = new List<Furniture>();
            foreach (var f in location.furniture)
            {
                if (f.ItemId == "d5a1lamdtd.MarketTown_CheckoutSmall" || f.ItemId == "d5a1lamdtd.MarketTown_CheckoutLarge")
                {
                    string employeeName = _employeeService.GetHiredEmployee(location, f.TileLocation);
                    if (!string.IsNullOrEmpty(employeeName))
                    {
                        NPC employee = Game1.getCharacterFromName(employeeName);
                        if (employee != null && employee.currentLocation == location)
                        {
                            checkouts.Add(f);
                        }
                    }
                }
            }

            if (checkouts.Count == 0) return false;

            var availableCheckouts = new List<(Furniture Checkout, int QueueLength)>();

            foreach (var checkout in checkouts)
            {
                string key = GetCheckoutKey(location, checkout.TileLocation);
                if (!_checkoutQueues.TryGetValue(key, out var queue))
                {
                    queue = new List<NPC>();
                    _checkoutQueues[key] = queue;
                }

                int maxSlots = GetCheckoutMaxSlots(checkout);
                
                if (queue.Contains(npc))
                {
                    assignedCheckout = checkout;
                    slotIndex = queue.IndexOf(npc);
                    return true;
                }

                if (queue.Count < maxSlots)
                {
                    availableCheckouts.Add((checkout, queue.Count));
                }
            }

            if (availableCheckouts.Count > 0)
            {
                var minLength = availableCheckouts.Min(c => c.QueueLength);
                var bestCheckouts = availableCheckouts.Where(c => c.QueueLength == minLength).ToList();
                var selected = bestCheckouts[Game1.random.Next(bestCheckouts.Count)];

                assignedCheckout = selected.Checkout;
                string key = GetCheckoutKey(location, assignedCheckout.TileLocation);
                
                _checkoutQueues[key].Add(npc);
                slotIndex = _checkoutQueues[key].Count - 1;
                
                return true;
            }

            return false;
        }

        public void ReleaseSlot(GameLocation location, Vector2 checkoutTile, NPC npc)
        {
            if (location == null) return;
            string key = GetCheckoutKey(location, checkoutTile);
            if (_checkoutQueues.TryGetValue(key, out var queue))
            {
                queue.Remove(npc);
            }
        }

        public void ReleaseAllSlotsForNpc(NPC npc)
        {
            foreach (var queue in _checkoutQueues.Values)
            {
                queue.Remove(npc);
            }
        }

        public int GetQueuePosition(GameLocation location, Vector2 checkoutTile, NPC npc)
        {
            if (location == null) return -1;
            string key = GetCheckoutKey(location, checkoutTile);
            if (_checkoutQueues.TryGetValue(key, out var queue))
            {
                return queue.IndexOf(npc);
            }
            return -1;
        }

        public Vector2 GetSlotTile(Furniture checkout, int slotIndex)
        {
            if (checkout == null) return Vector2.Zero;
            
            if (checkout.ItemId == "d5a1lamdtd.MarketTown_CheckoutLarge")
            {
                if (slotIndex == 0) return new Vector2(checkout.TileLocation.X + 3, checkout.TileLocation.Y + 2);
                if (slotIndex == 1) return new Vector2(checkout.TileLocation.X + 3, checkout.TileLocation.Y + 1);
                return new Vector2(checkout.TileLocation.X + 3, checkout.TileLocation.Y + 0);
            }
            else if (checkout.ItemId == "d5a1lamdtd.MarketTown_CheckoutSmall")
            {
                if (slotIndex == 0) return new Vector2(checkout.TileLocation.X + 2, checkout.TileLocation.Y + 1);
                return new Vector2(checkout.TileLocation.X + 2, checkout.TileLocation.Y + 0);
            }
            return Vector2.Zero;
        }

        private int GetCheckoutMaxSlots(Furniture checkout)
        {
            if (checkout.ItemId == "d5a1lamdtd.MarketTown_CheckoutLarge") return 3;
            if (checkout.ItemId == "d5a1lamdtd.MarketTown_CheckoutSmall") return 2;
            return 0;
        }

        private string GetCheckoutKey(GameLocation location, Vector2 checkoutTile)
        {
            return $"{location.NameOrUniqueName}_{checkoutTile.X}_{checkoutTile.Y}";
        }
    }
}
