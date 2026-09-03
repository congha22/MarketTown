using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using MarketTown.Framework.Models;
using MarketTown.Framework.Services;

namespace MarketTown.Framework.UI.Panels
{
    /// <summary>
    /// The default panel shown when the shop theme is "General".
    /// Displays the Capacity Scores (the information previously hard-coded
    /// in StoreManagerMenu.cs), keeping the menu itself clean and generic.
    /// </summary>
    public class GeneralStorePanel : IShopMenuPanel
    {
        private readonly StoreCapacityScores _capacityScores;

        public GeneralStorePanel(GameLocation location, StoreStatsService statsService, IndoorVisitorService visitorService)
        {
            _capacityScores = visitorService.GetStoreScores(location);
        }

        public void Draw(SpriteBatch b, int x, int y, int width, int height)
        {
            int lineHeight = 35;

            Utility.drawTextWithShadow(b, "Capacity Scores:", Game1.smallFont, new Vector2(x, y), Game1.textColor);
            y += lineHeight;

            DrawKeyValue(b, "Shop Level Score:", $"{_capacityScores.ShopLevelScore * 100:0}% (Max 50%)", x + 20, y);
            y += lineHeight;

            DrawKeyValue(b, "Selling Nodes Score:", $"{_capacityScores.SellingScore * 100:0}% (Max 30%)", x + 20, y);
            y += lineHeight;

            DrawKeyValue(b, "Decoration Score:", $"{_capacityScores.DecorationScore * 100:0}% (Max 20%)", x + 20, y);
            y += lineHeight;

            DrawKeyValue(b, "Max Customers:", _capacityScores.MaxCapacity.ToString(), x + 20, y, Color.DarkBlue);
            y += lineHeight + 10;

            // General store tip
            Utility.drawTextWithShadow(b,
                "General store: sells everything, +10% on all items.",
                Game1.smallFont,
                new Vector2(x, y),
                Color.DimGray);
        }

        public void ReceiveLeftClick(int x, int y) { /* no interactive elements */ }

        public void PerformHoverAction(int x, int y) { /* no tooltips */ }

        // ── helpers ───────────────────────────────────────────────────────────

        private static void DrawKeyValue(SpriteBatch b, string key, string value, int x, int y, Color? valueColor = null)
        {
            Utility.drawTextWithShadow(b, key, Game1.smallFont, new Vector2(x, y), Game1.textColor);
            Vector2 keySize = Game1.smallFont.MeasureString(key);
            Color vColor = valueColor ?? Game1.textShadowColor;
            b.DrawString(Game1.smallFont, value, new Vector2(x + keySize.X + 10, y), vColor);
        }
    }
}
