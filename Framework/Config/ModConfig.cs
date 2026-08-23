namespace MarketTown.Framework.Config
{
    /// <summary>The mod configuration options.</summary>
    public class ModConfig
    {
        public int NpcScanRange { get; set; } = 20;
        public int NpcScanCooldownMinutes { get; set; } = 180;
        public float NpcScanChance { get; set; } = 0.1f;
    }
}
