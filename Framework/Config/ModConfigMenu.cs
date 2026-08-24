using StardewModdingAPI;
using MarketTown.Framework.Integrations;

namespace MarketTown.Framework.Config
{
    /// <summary>Registers the mod configuration with Generic Mod Config Menu.</summary>
    public static class ModConfigMenu
    {
        public static void Register(IModHelper helper, IManifest manifest, ModConfig config)
        {
            var configMenu = helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu == null)
                return;

            // register mod
            configMenu.Register(
                mod: manifest,
                reset: () => 
                {
                    config.NpcScanRange = 20;
                    config.NpcScanCooldownMinutes = 180;
                    config.NpcScanChance = 0.1f;
                    config.NpcBrowseRange = 5;
                    config.MaxExtraBrowseTables = 2;
                    config.PreventWalkingThroughFurniture = true;
                    config.BaseBuyChance = 0.5f;
                    config.PriceMultiplier = 1.5f;
                    config.RestockChance = 0.5f;
                    config.RestockMinimumRule = RestockRule.Random;
                },
                save: () => helper.WriteConfig(config)
            );

            // add config options
            configMenu.AddNumberOption(
                mod: manifest,
                name: () => "NPC Scan Range",
                tooltip: () => "How many tiles away an NPC can detect a store table.",
                getValue: () => config.NpcScanRange,
                setValue: value => config.NpcScanRange = value,
                min: 1,
                max: 50
            );

            configMenu.AddNumberOption(
                mod: manifest,
                name: () => "NPC Scan Cooldown",
                tooltip: () => "How many in-game minutes an NPC will wait before scanning again after finding an item.",
                getValue: () => config.NpcScanCooldownMinutes,
                setValue: value => config.NpcScanCooldownMinutes = value,
                min: 10,
                max: 1440
            );

            configMenu.AddNumberOption(
                mod: manifest,
                name: () => "NPC Scan Chance",
                tooltip: () => "The chance (0.0 to 1.0) an NPC will decide to inspect an item they see.",
                getValue: () => config.NpcScanChance,
                setValue: value => config.NpcScanChance = value,
                min: 0.0f,
                max: 1.0f
            );

            configMenu.AddNumberOption(
                mod: manifest,
                name: () => "Nearby Browse Range",
                tooltip: () => "How many tiles away from the first table to search for additional tables to browse.",
                getValue: () => config.NpcBrowseRange,
                setValue: value => config.NpcBrowseRange = value,
                min: 1,
                max: 20
            );

            configMenu.AddNumberOption(
                mod: manifest,
                name: () => "Max Extra Browse Tables",
                tooltip: () => "Maximum number of extra nearby tables the NPC will visit after the first one (0 to 5).",
                getValue: () => config.MaxExtraBrowseTables,
                setValue: value => config.MaxExtraBrowseTables = value,
                min: 0,
                max: 5
            );

            configMenu.AddBoolOption(
                mod: manifest,
                name: () => "Prevent Walking Through Furniture",
                tooltip: () => "When enabled, dynamically sets NoPath map tile properties so NPC pathfinding routes around placed tables and obstacles.",
                getValue: () => config.PreventWalkingThroughFurniture,
                setValue: value => config.PreventWalkingThroughFurniture = value
            );

            configMenu.AddNumberOption(
                mod: manifest,
                name: () => "Base Buy Chance",
                tooltip: () => "The base chance (0.0 to 1.0) an NPC will buy an item after browsing it.",
                getValue: () => config.BaseBuyChance,
                setValue: value => config.BaseBuyChance = value,
                min: 0.0f,
                max: 1.0f
            );

            configMenu.AddNumberOption(
                mod: manifest,
                name: () => "Price Multiplier",
                tooltip: () => "Multiplier for the item's shipping price.",
                getValue: () => config.PriceMultiplier,
                setValue: value => config.PriceMultiplier = value,
                min: 0.1f,
                max: 10.0f
            );

            configMenu.AddNumberOption(
                mod: manifest,
                name: () => "Restock Chance",
                tooltip: () => "Chance (0.0 to 1.0) that a table will restock from a chest every 10 in-game minutes.",
                getValue: () => config.RestockChance,
                setValue: value => config.RestockChance = value,
                min: 0.0f,
                max: 1.0f
            );

            configMenu.AddTextOption(
                mod: manifest,
                name: () => "Restock Minimum Rule",
                tooltip: () => "The minimum priority rule when picking items from chests to restock tables.",
                getValue: () => config.RestockMinimumRule.ToString(),
                setValue: value => config.RestockMinimumRule = (RestockRule)System.Enum.Parse(typeof(RestockRule), value),
                allowedValues: System.Enum.GetNames(typeof(RestockRule))
            );
        }
    }
}
