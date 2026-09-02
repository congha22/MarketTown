namespace MarketTown.Framework.Models
{
    public class StoreStatRecord
    {
        public int TotalVisitors { get; set; }
        public int TotalSoldItems { get; set; }
        public int TotalEarnings { get; set; }
        
        public int OpenHour { get; set; } = 900;
        public int CloseHour { get; set; } = 1700;
    }
}
