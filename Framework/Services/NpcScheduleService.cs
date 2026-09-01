using System;
using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Pathfinding;

namespace MarketTown.Framework.Services
{
    /// <summary>Handles injecting new destinations into an NPC's schedule at runtime.</summary>
    internal static class NpcScheduleService
    {
        /// <summary>
        /// Injects a list of schedule destinations sequentially (each +10 minutes apart) for the NPC,
        /// preserving their existing schedule and current movement state.
        /// </summary>
        public static bool AddNewPointsToSchedule(NPC npc, List<(string locationName, Microsoft.Xna.Framework.Vector2 standTile, int facing, int scheduledTime)> stops)
        {
            // Only the master game controls NPC schedules
            if (!Game1.IsMasterGame || stops == null || stops.Count == 0) return false;

            // If NPC already has queued paths the schedule system is mid-transition — skip
            if (npc.queuedSchedulePaths.Count != 0) return false;

            try
            {
                var currentDirection = npc.DirectionsToNewLocation;
                Dictionary<int, SchedulePathDescription> schedule = npc.Schedule;

                // We'll build the new schedule as a sorted dict, then serialize it
                SortedDictionary<int, string> tempSche = new SortedDictionary<int, string>();

                int lastStopTime = stops.Last().scheduledTime;
                int resumeTime = NpcScheduleHelper.ConvertToHour(lastStopTime + 10);

                if (schedule != null && schedule.Count > 0)
                {
                    if (currentDirection == null && !npc.isMoving())
                    {
                        // -----------------------------------------------------------------------
                        // SCENARIO 1: NPC is standing still — insert all stops sequentially
                        // -----------------------------------------------------------------------
                        
                        // Anchor at current position so the schedule parser knows their true location.
                        // (Crucial if they were warped programmatically, avoiding invalid path generation)
                        int anchorTime = Game1.timeOfDay;
                        while (schedule.ContainsKey(anchorTime) || stops.Any(s => s.scheduledTime == anchorTime))
                        {
                            anchorTime -= 10;
                            if (anchorTime % 100 > 50) anchorTime -= 40;
                        }
                        tempSche[anchorTime] = $"{anchorTime} {npc.currentLocation.NameOrUniqueName} {npc.Tile.X} {npc.Tile.Y} {npc.FacingDirection}/";

                        foreach (var stop in stops)
                        {
                            string stopKey = stop.scheduledTime.ToString();
                            tempSche[stop.scheduledTime] = $"{stopKey} {stop.locationName} {(int)stop.standTile.X} {(int)stop.standTile.Y} {stop.facing}/";
                        }

                        foreach (var piece in schedule)
                        {
                            TryAddEntry(tempSche, piece.Key, piece.Value);
                        }
                    }
                    else if (currentDirection != null)
                    {
                        // -----------------------------------------------------------------------
                        // SCENARIO 2: NPC is mid-walk — stop them, inject stops, re-queue their
                        //             original destination after
                        // -----------------------------------------------------------------------

                        // Freeze NPC at their current position on the current tick
                        tempSche[Game1.timeOfDay] =
                            $"{Game1.timeOfDay} {npc.currentLocation.NameOrUniqueName} {npc.Tile.X} {npc.Tile.Y} {npc.FacingDirection}/";

                        // Add all browse stops
                        foreach (var stop in stops)
                        {
                            string stopKey = stop.scheduledTime.ToString();
                            tempSche[stop.scheduledTime] = $"{stopKey} {stop.locationName} {(int)stop.standTile.X} {(int)stop.standTile.Y} {stop.facing}/";
                        }

                        // Re-queue their original walk one tick after the last browse stop
                        string resumeEntry = $"{resumeTime} {currentDirection.targetLocationName} " +
                                            $"{currentDirection.targetTile.X} {currentDirection.targetTile.Y} " +
                                            $"{currentDirection.facingDirection}/";
                        tempSche[resumeTime] = resumeEntry;

                        // Re-add the rest of the schedule, skipping the currentDirection entry.
                        // Use piece.Key directly — past entries land at their original key (no collision
                        // since all injected entries are >= Game1.timeOfDay) and are ignored by the game.
                        foreach (var piece in schedule)
                        {
                            if (piece.Value.time == currentDirection.time) continue; // already re-added above
                            TryAddEntry(tempSche, piece.Key, piece.Value);
                        }
                    }
                }
                else
                {
                    // -----------------------------------------------------------------------
                    // SCENARIO 3: NPC has no schedule — anchor, inject stops
                    // -----------------------------------------------------------------------

                    // Anchor at current position
                    tempSche[Game1.timeOfDay] =
                        $"{Game1.timeOfDay} {npc.currentLocation.NameOrUniqueName} {npc.Tile.X} {npc.Tile.Y} {npc.FacingDirection}/";

                    // Add all browse stops
                    foreach (var stop in stops)
                    {
                        string stopKey = stop.scheduledTime.ToString();
                        tempSche[stop.scheduledTime] = $"{stopKey} {stop.locationName} {(int)stop.standTile.X} {(int)stop.standTile.Y} {stop.facing}/";
                    }
                }

                if (!tempSche.Any()) return false;

                // Serialize the sorted dict back to a schedule string
                string initSche = string.Concat(tempSche.Values);

                NpcScheduleHelper.CleanNpc(npc);
                npc.TryLoadSchedule("default", initSche);
                return true;
            }
            catch (Exception ex)
            {
                _ = ex;
                return false;
            }
        }

        /// <summary>
        /// Injects a single schedule destination for the NPC at the next 10-minute game tick,
        /// preserving their existing schedule and current movement state.
        /// </summary>
        public static bool AddNewPointToSchedule(NPC npc, string addTime, string locationName, string x, string y, string facingDir)
        {
            var singleStop = new List<(string locationName, Microsoft.Xna.Framework.Vector2 standTile, int facing, int scheduledTime)>
            {
                (locationName, new Microsoft.Xna.Framework.Vector2(float.Parse(x), float.Parse(y)), int.Parse(facingDir), int.Parse(addTime))
            };
            return AddNewPointsToSchedule(npc, singleStop);
        }

        /// <summary>
        /// Appends a new schedule point to the END of the NPC's current schedule.
        /// Used for wandering / end-of-schedule behaviors.
        /// </summary>
        public static void AddNextMoveSchedulePoint(NPC npc, string addTime, string locationName, string x, string y, string facingDir)
        {
            if (!Game1.IsMasterGame) return;

            string initSche = "";
            NpcScheduleHelper.CleanNpc(npc);

            Dictionary<int, SchedulePathDescription> schedule = npc.Schedule;

            if (schedule != null)
            {
                var last = schedule.LastOrDefault();
                if (last.Value != null)
                {
                    initSche += $"{last.Key} {last.Value.targetLocationName} " +
                                $"{last.Value.targetTile.X} {last.Value.targetTile.Y} " +
                                $"{last.Value.facingDirection}/";
                }
            }
            else
            {
                initSche += $"{Game1.timeOfDay} {npc.currentLocation.NameOrUniqueName} {npc.Tile.X} {npc.Tile.Y} {npc.FacingDirection}/";
            }

            initSche += $"{addTime} {locationName} {x} {y} {facingDir}/";

            npc.TryLoadSchedule("default", initSche);
        }

        // -----------------------------------------------------------------------
        // Private helpers
        // -----------------------------------------------------------------------

        /// <summary>
        /// Tries to add an entry to the sorted dict. If the key is already taken,
        /// nudges the time by +10 up to 5 times to find a free slot.
        /// </summary>
        private static void TryAddEntry(SortedDictionary<int, string> dict, int key, SchedulePathDescription desc)
        {
            string Serialize(int t) =>
                $"{t} {desc.targetLocationName} {desc.targetTile.X} {desc.targetTile.Y} {desc.facingDirection}/";

            if (!dict.ContainsKey(key))
            {
                dict.Add(key, Serialize(key));
                return;
            }

            int offset = 10;
            for (int i = 0; i < 5; i++)
            {
                int newKey = NpcScheduleHelper.ConvertToHour(key + offset);
                if (!dict.ContainsKey(newKey))
                {
                    dict.Add(newKey, Serialize(newKey));
                    return;
                }
                offset += 10;
            }
            // If still colliding after 5 tries, skip this entry silently
        }
    }
}
