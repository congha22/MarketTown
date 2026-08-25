using System;
using System.Collections.Generic;

namespace MarketTown.Framework.Models
{
    public class MarketTownSaveData
    {
        public List<SaleRecord> YesterdaySales { get; set; } = new List<SaleRecord>();
        public Dictionary<string, ItemSaleStats> ItemStats { get; set; } = new Dictionary<string, ItemSaleStats>();
        public Dictionary<int, CategorySaleStats> CategoryStats { get; set; } = new Dictionary<int, CategorySaleStats>();
        public Dictionary<string, CustomerSaleStats> CustomerStats { get; set; } = new Dictionary<string, CustomerSaleStats>();
    }

    public class SaleRecord
    {
        public string BuyerName { get; set; }
        public string QualifiedItemId { get; set; }
        public string ItemName { get; set; }
        public int Quality { get; set; }
        public int GiftTaste { get; set; }
        public int SoldPrice { get; set; }
        public string LocationName { get; set; }
        public int TimeOfDay { get; set; }
    }

    public class ItemSaleStats
    {
        public int TotalSold { get; set; }
        public int TotalRegular { get; set; }
        public int TotalSilver { get; set; }
        public int TotalGold { get; set; }
        public int TotalIridium { get; set; }
        public int TotalEarnings { get; set; }
    }

    public class CategorySaleStats
    {
        public int TotalSold { get; set; }
        public int TotalEarnings { get; set; }
    }

    public class CustomerSaleStats
    {
        public int TotalPurchased { get; set; }
        public int TotalSpent { get; set; }
        
        public int TotalLove { get; set; }
        public int TotalLike { get; set; }
        public int TotalNeutral { get; set; }
        public int TotalDislike { get; set; }
        public int TotalHate { get; set; }
    }
}
