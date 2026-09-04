using System.Linq;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Objects;
using MarketTown.Framework.Models;
using MarketTown.Framework.Services;
using MarketTown.Framework.UI.Panels;
using MarketTown.Framework.Integrations;
using System.Collections.Generic;

namespace MarketTown.Framework.Behaviors.ShopCategories
{
    public class DiscoveryShopBehavior : IShopCategoryBehavior
    {
        private readonly IMonitor _monitor;
        private readonly IndoorVisitorService _visitorService;
        private readonly IModHelper _helper;

        public DiscoveryShopBehavior(IMonitor monitor, IndoorVisitorService visitorService, IModHelper helper)
        {
            _monitor = monitor;
            _visitorService = visitorService;
            _helper = helper;
        }

        public string CategoryName => "Discovery";
        public string DisplayName  => "Discovery Shop";

        public void ApplyShopBuffs(StoreCapacityScores scores, GameLocation location)
        {
            // Same capacity as general.
        }

        public float GetPriceMultiplier(StardewValley.Object item)
        {
            // +25% for books, gems, and minerals.
            return IsDiscoveryItem(item) ? 1.25f : 1.0f;
        }

        public IShopMenuPanel GetMenuPanel(GameLocation location, StoreStatsService statsService, IndoorVisitorService visitorService)
        {
            return new DiscoveryShopPanel(location, statsService, visitorService);
        }

        public void OnVisitorTick(NPC npc, GameLocation location, IndoorVisitorService.VisitorData data, float deltaMs)
        {
            if (!data.IsSittingToRead)
                return;

            data.ReadingTimer += deltaMs;

            if (data.ReadingTimer >= data.ReadingDuration)
            {
                // Done — stop reading and return to normal wandering
                data.IsSittingToRead = false;
                data.ReadingTimer = 0f;

                npc.Halt();

                ICASApi casApi = _helper.ModRegistry.GetApi<ICASApi>("d5a1lamdtd.CASCreateAStardewie");
                if (casApi != null)
                {
                    casApi.TriggerNpcAction(npc, "stop");
                }

                npc.faceDirection(2);

                if (data.AssignedChair != null)
                {
                    if (!_visitorService.IsChairStillOccupied(data.AssignedChair, data))
                    {
                        data.AssignedChair.sittingFarmers.Remove(Game1.player.UniqueMultiplayerID);
                    }
                }

                // Snap to a nearby open tile
                Vector2 resetTile = GetOpenTileNear(location, data.ReadingChairTile);
                if (resetTile != Vector2.Zero)
                {
                    npc.Position = resetTile * 64f;
                }

                _monitor.Log($"{npc.Name} finished reading and got up.", LogLevel.Debug);
            }
        }

        public bool TryHandleCustomCustomerWander(NPC npc, GameLocation location, IndoorVisitorService.VisitorData data)
        {
            if (data.IsSittingToRead || data.IsHeadingToRead)
            {
                return true;
            }

            if (!IsCasNpc(npc))
                return false;

            // Check if they bought a new book
            int currentBookCount = data.ShoppingCart.Count(ci => ci.Item.Category == -102);

            if (currentBookCount <= data.BooksAtLastReadEntry)
                return false;

            if (!_visitorService.TryReserveChairTile(location, data, out Vector2 chairTile, out Furniture assignedChair, out int slotIndex))
            {
                return false; // No chairs free
            }

            data.IsHeadingToRead = true;
            data.ReadingChairTile = chairTile;
            data.AssignedChair = assignedChair;
            data.AssignedChairSlotIndex = slotIndex;
            data.BooksAtLastReadEntry = currentBookCount;

            NpcScheduleHelper.CleanNpc(npc);
            
            // Pathfind to the tile next to the chair
            Vector2 pathTile = GetOpenTileNear(location, chairTile);
            if (pathTile == Vector2.Zero)
            {
                // Unreserve if no nearby open tile
                data.IsHeadingToRead = false;
                data.AssignedChair = null;
                return false;
            }

            npc.controller = new StardewValley.Pathfinding.PathFindController(
                npc, location,
                new Point((int)pathTile.X, (int)pathTile.Y),
                -1,
                new StardewValley.Pathfinding.PathFindController.endBehavior((c, loc) => OnChairArrived(c as NPC, data)));

            _monitor.Log($"{npc.Name} is heading to a chair to read.", LogLevel.Debug);
            return true;
        }

        private void OnChairArrived(NPC npc, IndoorVisitorService.VisitorData data)
        {
            if (npc == null || data.AssignedChair == null) return;

            data.IsHeadingToRead = false;
            data.IsSittingToRead = true;
            data.ReadingTimer = 0f;
            data.ReadingDuration = Game1.random.Next(10000, 15001); // 10-15 seconds

            // Assign sitting farmer ID
            long farmerId = Game1.player.UniqueMultiplayerID;
            data.AssignedChair.sittingFarmers[farmerId] = data.AssignedChairSlotIndex;

            // Snap onto the chair with a Y offset to lower the NPC visually
            npc.Position = data.ReadingChairTile * 64f + new Vector2(0f, 24f);

            // Face direction of chair
            int facingDirection = data.AssignedChair.GetSittingDirection();
            npc.faceDirection(facingDirection);

            // Call CAS API
            ICASApi casApi = _helper.ModRegistry.GetApi<ICASApi>("d5a1lamdtd.CASCreateAStardewie");
            if (casApi != null)
            {
                casApi.TriggerNpcAction(npc, "sit", facingDirection);
            }

            _monitor.Log($"{npc.Name} sat down to read for ~{data.ReadingDuration / 1000f:0.0}s.", LogLevel.Debug);
        }

        private Vector2 GetOpenTileNear(GameLocation location, Vector2 center)
        {
            // Simple check around the center
            var candidates = new List<Vector2>();
            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    if (x == 0 && y == 0) continue; // Skip the chair itself

                    Vector2 tile = new Vector2(center.X + x, center.Y + y);
                    if (!location.IsTileBlockedBy(tile, ignorePassables: CollisionMask.Flooring | CollisionMask.Furniture))
                    {
                        candidates.Add(tile);
                    }
                }
            }

            if (candidates.Count > 0)
                return candidates[Game1.random.Next(candidates.Count)];

            return Vector2.Zero;
        }

        private static bool IsDiscoveryItem(Item item) =>
            item.Category == -102 || item.Category == -12 || item.Category == -2;

        private bool IsCasNpc(NPC npc)
        {
            ICASApi casApi = _helper.ModRegistry.GetApi<ICASApi>("d5a1lamdtd.CASCreateAStardewie");
            if (casApi == null) return false;
            return casApi.GetCustomNPCs().Any(n => n.Name == npc.Name);
        }
    }
}
