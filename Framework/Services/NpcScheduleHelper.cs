using Microsoft.Xna.Framework;
using StardewValley;

namespace MarketTown.Framework.Services
{
    /// <summary>Utility helpers for NPC schedule manipulation.</summary>
    internal static class NpcScheduleHelper
    {
        /// <summary>
        /// Converts a raw SDV time integer safely to HHMM format, handling minute overflow.
        /// e.g. ConvertToHour(1060) -> 1100, ConvertToHour(2390) -> 2430.
        /// </summary>
        public static int ConvertToHour(int number)
        {
            string s = number.ToString();
            int hour = int.Parse(s.Substring(0, s.Length - 2));
            int minute = int.Parse(s.Substring(s.Length - 2));

            if (minute >= 60)
            {
                hour += minute / 60;
                minute %= 60;
            }

            return int.Parse(hour.ToString() + minute.ToString("00"));
        }

        /// <summary>
        /// Fully halts and wipes all movement state from an NPC so a fresh schedule
        /// can be loaded without conflicts.
        /// </summary>
        public static void CleanNpc(NPC npc)
        {
            npc.Halt();
            npc.ClearSchedule();
            npc.DirectionsToNewLocation = null;
            npc.queuedSchedulePaths.Clear();
            npc.previousEndPoint = npc.TilePoint;
            npc.temporaryController = null;
            npc.controller = null;
            if (npc.Sprite.CurrentFrame > 15)
                npc.Sprite.CurrentFrame = 0;
            npc.Halt();
        }

        /// <summary>
        /// Finds a walkable tile adjacent to the furniture tile using a random offset (up to 3 tries),
        /// and returns the facing direction (toward the table).
        /// Returns Vector2.Zero if no walkable adjacent tile was found.
        /// </summary>
        public static Vector2 GetAdjacentWalkableTile(GameLocation location, Vector2 furnitureTile, out int facingDirection)
        {
            facingDirection = 2;

            for (int tries = 0; tries < 3; tries++)
            {
                int xMove = Game1.random.Next(-1, 2);
                int yMove = Game1.random.Next(-1, 2);

                Vector2 candidate = new Vector2(furnitureTile.X + xMove, furnitureTile.Y + yMove);

                if (xMove == -1) facingDirection = 1;        // stand left  → face right
                else if (xMove == 1) facingDirection = 3;    // stand right → face left
                else if (yMove == -1) facingDirection = 2;   // stand above → face down
                else facingDirection = 0;                    // stand below → face up

                if (!location.IsTileBlockedBy(candidate, ignorePassables: CollisionMask.Flooring | CollisionMask.Furniture))
                    return candidate;
            }

            return Vector2.Zero;
        }
    }
}
