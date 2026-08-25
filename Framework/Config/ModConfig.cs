namespace MarketTown.Framework.Config
{
    /// <summary>The mod configuration options.</summary>
    public class ModConfig
    {
        public int NpcScanRange { get; set; } = 20;
        public int NpcScanCooldownMinutes { get; set; } = 180;
        public float NpcScanChance { get; set; } = 0.1f;
        public int NpcBrowseRange { get; set; } = 5;
        public int MaxExtraBrowseTables { get; set; } = 2;
        public bool PreventWalkingThroughFurniture { get; set; } = true;
        
        public float BaseBuyChance { get; set; } = 0.5f;
        public float PriceMultiplier { get; set; } = 1.5f;
        
        public float RestockChance { get; set; } = 0.5f;
        public RestockRule RestockMinimumRule { get; set; } = RestockRule.Random;

        public StardewModdingAPI.SButton OpenSalesMenuKey { get; set; } = StardewModdingAPI.SButton.H;
    }

    public enum RestockRule
    {
        SameItem,
        SameCategory,
        Random
    }
}
