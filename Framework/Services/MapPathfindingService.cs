using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Objects;
using MarketTown.Framework.Config;

namespace MarketTown.Framework.Services
{
    /// <summary>
    /// Applies 'NoPath' tile properties to the map's Back layer for impassable furniture, objects,
    /// and buildings so that Stardew Valley's schedule pathfinding accurately routes NPCs around them.
    /// </summary>
    public class MapPathfindingService
    {
        private readonly IMonitor _monitor;
        private readonly ModConfig _config;
        private readonly IModHelper _helper;

        /// <summary>Tracks all tiles where we dynamically injected the 'NoPath' property by location.</summary>
        private readonly Dictionary<GameLocation, HashSet<Vector2>> _modifiedTiles = new Dictionary<GameLocation, HashSet<Vector2>>();

        public MapPathfindingService(IMonitor monitor, ModConfig config, IModHelper helper)
        {
            _monitor = monitor;
            _config = config;
            _helper = helper;

            _helper.Events.GameLoop.DayStarted += OnDayStarted;
            _helper.Events.GameLoop.DayEnding += OnDayEnding;
        }

        private void OnDayStarted(object sender, DayStartedEventArgs e)
        {
            if (!Game1.IsMasterGame) return;

            // Clear previous day's modifications
            ClearAllModifiedTiles();

            if (!_config.PreventWalkingThroughFurniture)
                return;

            // Apply NoPath to all active game locations
            foreach (var location in Game1.locations)
            {
                UpdateLocationPathProperties(location);
            }

            _monitor.Log("Updated NoPath map properties for impassable furniture and objects across all locations.", LogLevel.Debug);
        }

        private void OnDayEnding(object sender, DayEndingEventArgs e)
        {
            if (!Game1.IsMasterGame) return;

            // Clean up all tile properties before saving so they don't persist into save files
            ClearAllModifiedTiles();
        }

        /// <summary>
        /// Updates the NoPath tile properties for a specific location.
        /// Can be called whenever an NPC is about to path in a location to ensure dynamic furniture is included.
        /// </summary>
        public void UpdateLocationPathProperties(GameLocation location)
        {
            if (location == null || !Game1.IsMasterGame || location.Map == null || !_config.PreventWalkingThroughFurniture)
                return;

            if (!_modifiedTiles.TryGetValue(location, out var tileSet))
            {
                tileSet = new HashSet<Vector2>();
                _modifiedTiles[location] = tileSet;
            }

            // 1. Impassable Furniture (Tables, Displays, etc.)
            foreach (var furniture in location.furniture)
            {
                if (furniture == null || furniture.isPassable()) continue;

                Vector2 origin = furniture.TileLocation;
                int width = furniture.getTilesWide();
                int height = furniture.getTilesHigh();

                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        var tile = new Vector2(origin.X + x, origin.Y + y);
                        ApplyNoPath(location, tile, tileSet);
                    }
                }
            }

            // 2. Impassable Objects (Chests, Signs, Fences, etc.)
            foreach (var obj in location.Objects.Values)
            {
                if (obj == null || obj.isPassable()) continue;
                ApplyNoPath(location, obj.TileLocation, tileSet);
            }

            // 3. Impassable Terrain Features (Trees, Bushes, etc.)
            foreach (var feature in location.terrainFeatures.Values)
            {
                if (feature == null || feature.isPassable()) continue;
                ApplyNoPath(location, feature.Tile, tileSet);
            }

            // 4. Buildings
            foreach (var building in location.buildings)
            {
                if (building == null) continue;

                int originX = building.tileX.Value;
                int originY = building.tileY.Value;
                int width = building.tilesWide.Value;
                int height = building.tilesHigh.Value;

                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        var tile = new Vector2(originX + x, originY + y);
                        ApplyNoPath(location, tile, tileSet);
                    }
                }
            }
        }

        /// <summary>Applies the 'NoPath' property to a single tile if not already present.</summary>
        private void ApplyNoPath(GameLocation location, Vector2 tile, HashSet<Vector2> tileSet)
        {
            int tileX = (int)tile.X;
            int tileY = (int)tile.Y;

            if (!location.doesEitherTileOrTileIndexPropertyEqual(tileX, tileY, "NoPath", "Back", "T"))
            {
                location.setTileProperty(tileX, tileY, "Back", "NoPath", "T");
                tileSet.Add(tile);
            }
        }

        /// <summary>Removes all injected 'NoPath' properties and clears the tracker.</summary>
        private void ClearAllModifiedTiles()
        {
            foreach (var (location, tiles) in _modifiedTiles)
            {
                if (location?.Map == null) continue;

                foreach (var tile in tiles)
                {
                    location.removeTileProperty((int)tile.X, (int)tile.Y, "Back", "NoPath");
                }
            }

            _modifiedTiles.Clear();
        }
    }
}
