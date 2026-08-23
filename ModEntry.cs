using System;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using MarketTown.Framework.Config;
using MarketTown.Framework.Services;

namespace MarketTown
{
    /// <summary>The mod entry point.</summary>
    public class ModEntry : Mod
    {
        /// <summary>The mod configuration from the player.</summary>
        public ModConfig Config { get; private set; }

        private NpcScannerService _npcScannerService;

        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        public override void Entry(IModHelper helper)
        {
            this.Config = this.Helper.ReadConfig<ModConfig>();

            // Register events
            helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
        }

        /// <summary>Raised after the game is launched, right before the first update tick.</summary>
        private void OnGameLaunched(object sender, GameLaunchedEventArgs e)
        {
            this.Monitor.Log("Market Town initialized successfully.", LogLevel.Info);

            // Initialize services
            _npcScannerService = new NpcScannerService(this.Monitor, this.Config, this.Helper);

            // Register GMCM
            ModConfigMenu.Register(this.Helper, this.ModManifest, this.Config);
        }
    }
}
