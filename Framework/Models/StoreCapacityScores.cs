namespace MarketTown.Framework.Models
{
    public class StoreCapacityScores
    {
        public int ShopLevel { get; set; }
        public float ShopLevelScore { get; set; }
        public float SellingScore { get; set; }
        public float DecorationScore { get; set; }
        public float BonusScore { get; set; }
        public int BaseCapacity { get; set; }
        public int MaxCapacity { get; set; }
    }
}
