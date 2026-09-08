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

        public int GetShopLevel()
        {
            if (TotalVisitors >= 1500 && TotalSoldItems >= 2500 && TotalEarnings >= 150000) return 5;
            if (TotalVisitors >= 650 && TotalSoldItems >= 1000 && TotalEarnings >= 50000) return 4;
            if (TotalVisitors >= 200 && TotalSoldItems >= 350 && TotalEarnings >= 20000) return 3;
            if (TotalVisitors >= 70 && TotalSoldItems >= 120 && TotalEarnings >= 5000) return 2;
            return 1;
        }

        public void GetNextLevelRequirements(out int reqVisitors, out int reqItems, out int reqEarnings)
        {
            int level = GetShopLevel();
            switch (level)
            {
                case 1: reqVisitors = 70; reqItems = 120; reqEarnings = 5000; break;
                case 2: reqVisitors = 200; reqItems = 350; reqEarnings = 20000; break;
                case 3: reqVisitors = 650; reqItems = 1000; reqEarnings = 50000; break;
                case 4: reqVisitors = 1500; reqItems = 2500; reqEarnings = 150000; break;
                default: reqVisitors = 1500; reqItems = 2500; reqEarnings = 150000; break;
            }
        }
    }
}
