using StardewValley;

namespace MarketTown.Framework.Models
{
    public class CartItem
    {
        public Item Item { get; set; }
        public int Price { get; set; }
        public int Taste { get; set; }
        public Farmer Seller { get; set; }
    }
}
