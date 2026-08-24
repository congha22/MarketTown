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
        /// Injects a new schedule destination for the NPC at the next 10-minute game tick,
        /// preserving their existing schedule and current movement state.
        /// </summary>
        /// <param name="npc">The NPC to redirect.</param>
        /// <param name="addTime">The HHMM time at which the NPC should arrive (should be Game1.timeOfDay + 10).</param>
        /// <param name="locationName">The NameOrUniqueName of the target location.</param>
        /// <param name="x">Target tile X.</param>
        /// <param name="y">Target tile Y.</param>
        /// <param name="facingDir">Facing direction on arrival (0=up,1=right,2=down,3=left).</param>
        /// <returns>True if the injection succeeded, false otherwise.</returns>
        public static bool AddNewPointToSchedule(NPC npc, string addTime, string locationName, string x, string y, string facingDir)
        {
            // Only the master game controls NPC schedules
            if (!Game1.IsMasterGame) return false;

            // If NPC already has queued paths the schedule system is mid-transition — skip
            if (npc.queuedSchedulePaths.Count != 0) return false;

            try
            {
                var currentDirection = npc.DirectionsToNewLocation;
                Dictionary<int, SchedulePathDescription> schedule = npc.Schedule;

                // We'll build the new schedule as a sorted dict, then serialize it
                SortedDictionary<int, string> tempSche = new SortedDictionary<int, string>();

                if (schedule != null && schedule.Count > 0)
                {
                    if (currentDirection == null && !npc.isMoving())
                    {
                        // -----------------------------------------------------------------------
                        // SCENARIO 1: NPC is standing still — just insert the new entry
                        // -----------------------------------------------------------------------
                        tempSche.Add(int.Parse(addTime), $"{addTime} {locationName} {x} {y} {facingDir}/");

                        foreach (var piece in schedule)
                        {
                            TryAddEntry(tempSche, piece.Key, piece.Value);
                        }
                    }
                    else if (currentDirection != null)
                    {
                        // -----------------------------------------------------------------------
                        // SCENARIO 2: NPC is mid-walk — stop them, inject table, re-queue their
                        //             original destination after
                        // -----------------------------------------------------------------------

                        // Freeze NPC at their current position on the current tick
                        tempSche.Add(Game1.timeOfDay,
                            $"{Game1.timeOfDay} {npc.currentLocation.NameOrUniqueName} {npc.Tile.X} {npc.Tile.Y} {npc.FacingDirection}/");

                        // New destination at next tick
                        tempSche.Add(int.Parse(addTime), $"{addTime} {locationName} {x} {y} {facingDir}/");

                        // Re-queue their original walk one tick after that
                        int resumeTime = NpcScheduleHelper.ConvertToHour(int.Parse(addTime) + 10);
                        string resumeEntry = $"{resumeTime} {currentDirection.targetLocationName} " +
                                            $"{currentDirection.targetTile.X} {currentDirection.targetTile.Y} " +
                                            $"{currentDirection.facingDirection}/";
                        tempSche.Add(resumeTime, resumeEntry);

                        // Re-add the rest of the schedule, skipping the currentDirection entry
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
                    // SCENARIO 3: NPC has no schedule — anchor, inject, return to anchor
                    // -----------------------------------------------------------------------

                    // Anchor at current position
                    tempSche.Add(Game1.timeOfDay,
                        $"{Game1.timeOfDay} {npc.currentLocation.NameOrUniqueName} {npc.Tile.X} {npc.Tile.Y} {npc.FacingDirection}/");

                    // New destination
                    tempSche.Add(int.Parse(addTime), $"{addTime} {locationName} {x} {y} {facingDir}/");

                    // Return to their current position afterward
                    int returnTime = NpcScheduleHelper.ConvertToHour(int.Parse(addTime) + 10);
                    tempSche.Add(returnTime,
                        $"{returnTime} {npc.currentLocation.NameOrUniqueName} {npc.Tile.X} {npc.Tile.Y} {npc.FacingDirection}/");
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
                // Log but don't crash — this is a best-effort operation
                // The caller can log this if needed
                _ = ex;
                return false;
            }
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
