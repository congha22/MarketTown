using System;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using MarketTown.Framework.Behaviors.ShopCategories;
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
        private ShopBehaviorService _shopBehaviorService;

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

            // Object and Furniture prevent pickup during shop hours
            harmony.Patch(
                original: AccessTools.Method(typeof(StardewValley.Objects.Furniture), nameof(StardewValley.Objects.Furniture.canBeRemoved)),
                postfix: new HarmonyMethod(typeof(ObjectPatches), nameof(ObjectPatches.CanBeRemoved_Postfix))
            );
            harmony.Patch(
                original: AccessTools.Method(typeof(StardewValley.Object), nameof(StardewValley.Object.performToolAction)),
                prefix: new HarmonyMethod(typeof(ObjectPatches), nameof(ObjectPatches.PerformToolAction_Prefix))
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

            _shopBehaviorService = new ShopBehaviorService(this.Monitor, _storeStatsService);
            // Built-in themes
            _shopBehaviorService.Register(new FashionShopBehavior(this.Monitor, indoorVisitorService, this.Helper));
            _shopBehaviorService.Register(new DiscoveryShopBehavior(this.Monitor, indoorVisitorService, this.Helper));
            // Wire ShopBehaviorService into all hook points
            indoorVisitorService.ShopBehaviorService = _shopBehaviorService;
            salesService.ShopBehaviorService = _shopBehaviorService;
            _indoorStoreTrackingService.ShopBehaviorService = _shopBehaviorService;

            _checkoutManagerService = new CheckoutManagerService(this.Monitor, _storeEmployeeService, this.Helper);
            indoorVisitorService.CheckoutManager = _checkoutManagerService;
            indoorVisitorService.SalesService = salesService;

            _npcScannerService = new NpcScannerService(this.Monitor, this.Config, this.Helper, salesService, _indoorStoreTrackingService, indoorVisitorService, _mapPathfindingService);

            // Register GMCM
            ModConfigMenu.Register(this.Helper, this.ModManifest, this.Config);

            // Register Cheat Command
            this.Helper.ConsoleCommands.Add("mt_cheat", "Market Town cheat commands.\nUsage: mt_cheat shop_stat <visitors> <items> <earnings>", this.OnCheatCommand);
        }

        private void OnCheatCommand(string command, string[] args)
        {
            if (args.Length == 0)
            {
                this.Monitor.Log("Usage: mt_cheat shop_stat <visitors> <items> <earnings>", LogLevel.Error);
                return;
            }

            if (args[0].ToLower() == "shop_stat")
            {
                if (!Context.IsWorldReady) return;
                if (Game1.currentLocation == null) return;
                if (args.Length != 4)
                {
                    this.Monitor.Log("Usage: mt_cheat shop_stat <visitors> <items> <earnings>", LogLevel.Error);
                    return;
                }

                if (!int.TryParse(args[1], out int visitors) || !int.TryParse(args[2], out int items) || !int.TryParse(args[3], out int earnings))
                {
                    this.Monitor.Log("Invalid arguments. Must be integers.", LogLevel.Error);
                    return;
                }

                string key = Game1.currentLocation.NameOrUniqueName;
                _storeStatsService.SetStoreStats(Game1.currentLocation, visitors, items, earnings);
                this.Monitor.Log($"Set stats for {key}: {visitors} visitors, {items} items sold, {earnings}g earnings.", LogLevel.Info);
            }
            else
            {
                this.Monitor.Log($"Unknown cheat subcommand: {args[0]}", LogLevel.Error);
            }
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
