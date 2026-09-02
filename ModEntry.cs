using System;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using MarketTown.Framework.Config;
using MarketTown.Framework.Services;
using MarketTown.Framework.UI;
using StardewValley;
using HarmonyLib;
using MarketTown.Framework.Patches;

namespace MarketTown
{
    /// <summary>The mod entry point.</summary>
    public class ModEntry : Mod
    {
        /// <summary>The mod configuration from the player.</summary>
        public ModConfig Config { get; private set; }

        private MapPathfindingService _mapPathfindingService;
        private NpcScannerService _npcScannerService;
        private SalesTrackingService _salesTrackingService;
        private IndoorStoreTrackingService _indoorStoreTrackingService;
        private StoreStatsService _storeStatsService;
        private StoreEmployeeService _storeEmployeeService;
        private CheckoutManagerService _checkoutManagerService;

        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        public override void Entry(IModHelper helper)
        {
            this.Config = this.Helper.ReadConfig<ModConfig>();

            // Register events
            helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
            helper.Events.Input.ButtonPressed += this.OnButtonPressed;

            // Apply Harmony patches
            var harmony = new Harmony(this.ModManifest.UniqueID);
            harmony.Patch(
                original: AccessTools.Method(typeof(StardewValley.Objects.Furniture), nameof(StardewValley.Objects.Furniture.IntersectsForCollision)),
                postfix: new HarmonyMethod(typeof(FurniturePatches), nameof(FurniturePatches.IntersectsForCollision_Postfix))
            );
            harmony.Patch(
                original: AccessTools.Method(typeof(StardewValley.Objects.Furniture), nameof(StardewValley.Objects.Furniture.draw), new Type[] { typeof(Microsoft.Xna.Framework.Graphics.SpriteBatch), typeof(int), typeof(int), typeof(float) }),
                prefix: new HarmonyMethod(typeof(FurniturePatches), nameof(FurniturePatches.Draw_Prefix))
            );
        }

        /// <summary>Raised after the game is launched, right before the first update tick.</summary>
        private void OnGameLaunched(object sender, GameLaunchedEventArgs e)
        {
            this.Monitor.Log("Market Town initialized successfully.", LogLevel.Info);

            // Initialize services
            _mapPathfindingService = new MapPathfindingService(this.Monitor, this.Config, this.Helper);

            var tableRestockService = new TableRestockService(this.Monitor, this.Config, this.Helper);
            _salesTrackingService = new SalesTrackingService(this.Monitor, this.Helper);
            
            _indoorStoreTrackingService = new IndoorStoreTrackingService(this.Monitor, this.Helper, _salesTrackingService);
            _storeStatsService = new StoreStatsService(this.Monitor, this.Helper, _indoorStoreTrackingService);

            var indoorVisitorService = new IndoorVisitorService(this.Monitor, this.Helper, this.Config, _indoorStoreTrackingService, _storeStatsService);

            var salesService = new NpcSalesService(this.Monitor, this.Config, tableRestockService, _salesTrackingService, _storeStatsService, indoorVisitorService);

            _indoorStoreTrackingService.StoreStatsService = _storeStatsService;
            _indoorStoreTrackingService.VisitorService = indoorVisitorService;
            
            _storeEmployeeService = new StoreEmployeeService(this.Monitor, this.Helper, _indoorStoreTrackingService);
            _indoorStoreTrackingService.EmployeeService = _storeEmployeeService;
            
            _checkoutManagerService = new CheckoutManagerService(this.Monitor, _storeEmployeeService, this.Helper);
            indoorVisitorService.CheckoutManager = _checkoutManagerService;
            indoorVisitorService.SalesService = salesService;

            _npcScannerService = new NpcScannerService(this.Monitor, this.Config, this.Helper, salesService, _indoorStoreTrackingService, indoorVisitorService, _mapPathfindingService);

            // Register GMCM
            ModConfigMenu.Register(this.Helper, this.ModManifest, this.Config);
        }

        private void OnButtonPressed(object sender, ButtonPressedEventArgs e)
        {
            // Ignore if player hasn't loaded a save yet
            if (!Context.IsWorldReady)
                return;

            if (e.Button == this.Config.OpenSalesMenuKey)
            {
                if (Game1.activeClickableMenu == null)
                {
                    Game1.activeClickableMenu = new SalesMenu(_salesTrackingService, this.Helper);
                }
            }
        }
    }
}
