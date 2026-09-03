namespace MarketTown.Framework.Models
{
    public class StoreStatRecord
    {
        public int TotalVisitors { get; set; }
        public int TotalSoldItems { get; set; }
        public int TotalEarnings { get; set; }

        public int OpenHour { get; set; } = 900;
        public int CloseHour { get; set; } = 1700;

        /// <summary>The shop theme/category selected by the player. Defaults to "General".</summary>
        public string ShopTheme { get; set; } = "General";
    }
}
