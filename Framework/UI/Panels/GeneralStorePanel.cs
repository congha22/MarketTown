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

        public int Draw(SpriteBatch b, int x, int y, int width, int height)
        {
            // Divider
            b.Draw(Game1.fadeToBlackRect, new Rectangle(x, y, width, 2), Color.SlateGray * 0.5f);
            y += 15;

            int lineHeight = 35;

            b.DrawString(Game1.smallFont, $"Capacity limit: {_capacityScores.MaxCapacity}", new Vector2(x, y), Game1.textColor);
            y += lineHeight;

            DrawKeyValue(b, "Shop level:", $"{_capacityScores.ShopLevelScore * 100:0}% (Max 50%)", x + 20, y);
            y += lineHeight;
            DrawKeyValue(b, "Stock available:", $"{_capacityScores.SellingScore * 100:0}% (Max 30%)", x + 20, y);
            y += lineHeight;
            DrawKeyValue(b, "Decoration:", $"{_capacityScores.DecorationScore * 100:0}% (Max 20%)", x + 20, y);
            y += lineHeight + 10;

            // General store tip
            string parsedTip = Game1.parseText("General store: sells everything, +10% on all items.", Game1.smallFont, width);
            b.DrawString(Game1.smallFont, parsedTip, new Vector2(x, y), Color.DimGray);
            return y + (int)Game1.smallFont.MeasureString(parsedTip).Y + 10;
        }

        public void ReceiveLeftClick(int x, int y) { /* no interactive elements */ }

        public void PerformHoverAction(int x, int y) { /* no tooltips */ }

        // ── helpers ───────────────────────────────────────────────────────────

        private static void DrawKeyValue(SpriteBatch b, string key, string value, int x, int y, Color? valueColor = null)
        {
            b.DrawString(Game1.smallFont, key, new Vector2(x, y), Game1.textColor);
            Vector2 keySize = Game1.smallFont.MeasureString(key);
            Color vColor = valueColor ?? new Color(60, 60, 60);
            b.DrawString(Game1.smallFont, value, new Vector2(x + keySize.X + 10, y), vColor);
        }
    }
}
