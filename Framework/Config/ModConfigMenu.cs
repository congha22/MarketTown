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
        }
    }
}
