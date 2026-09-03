using System;
using StardewValley;
using StardewValley.Objects;
using MarketTown.Framework.Services;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using HarmonyLib;

namespace MarketTown.Framework.Patches
{
    internal static class ObjectPatches
    {
        public static void CanBeRemoved_Postfix(StardewValley.Object __instance, Farmer who, ref bool __result)
        {
            if (!__result) return; // If already false, do nothing

            if (IsProtectedStoreItem(__instance))
            {
                if (StoreStatsService.Instance != null && StoreStatsService.Instance.IsStoreOpen(__instance.Location))
                {
                    __result = false;
                }
            }
        }

        public static bool PerformToolAction_Prefix(StardewValley.Object __instance, Tool t, ref bool __result)
        {
            if (IsProtectedStoreItem(__instance))
            {
                if (StoreStatsService.Instance != null && StoreStatsService.Instance.IsStoreOpen(__instance.Location))
                {
                    // Don't allow it to be broken/removed while the store is open
                    // return false to skip original logic and returning false for performToolAction
                    __result = false;
                    
                    // Optional: show a message
                    if (Game1.player.ActiveObject == null) // Check if not holding something that might interact? Or just show a message.
                    {
                        // Game1.showRedMessage("Cannot remove registers while the store is open.");
                    }
                    
                    return false; 
                }
            }
            return true;
        }

        private static bool IsProtectedStoreItem(StardewValley.Object obj)
        {
            if (obj == null) return false;

            return obj.ItemId == "d5a1lamdtd.MarketTown_CheckoutSmall" ||
                   obj.ItemId == "d5a1lamdtd.MarketTown_CheckoutLarge" ||
                   obj.ItemId == "d5a1lamdtd.MarketTown_StoreRegister";
        }
    }
}
