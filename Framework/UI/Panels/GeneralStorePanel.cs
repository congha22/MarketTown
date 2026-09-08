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
            int offset = 220;

            int baseCap = _capacityScores.BaseCapacity;
            string baseCapStr = baseCap < 10 ? $"  {baseCap}" : $"{baseCap}";
            DrawKeyValue(b, "Base capacity:", baseCapStr, x + 20, y, offset);
            y += lineHeight;

            int levelContrib = (int)(baseCap * _capacityScores.ShopLevelScore);
            int maxLevelContrib = (int)(baseCap * 0.5f);

            int sellingContrib = (int)(baseCap * _capacityScores.SellingScore);
            int maxSellingContrib = (int)(baseCap * 0.75f);

            int decoContrib = (int)(baseCap * _capacityScores.DecorationScore);
            int maxDecoContrib = (int)(baseCap * 0.25f);

            DrawKeyValue(b, "Shop level:", $"+{levelContrib} (Max +{maxLevelContrib})", x + 20, y, offset);
            y += lineHeight;
            DrawKeyValue(b, "Stock available:", $"+{sellingContrib} (Max +{maxSellingContrib})", x + 20, y, offset);
            y += lineHeight;
            DrawKeyValue(b, "Decoration:", $"+{decoContrib} (Max +{maxDecoContrib})", x + 20, y, offset);
            y += lineHeight + 10;

            // General store tip
            string parsedTip = Game1.parseText("General store: sells everything, +10% on all items.", Game1.smallFont, width);
            b.DrawString(Game1.smallFont, parsedTip, new Vector2(x, y), Color.DimGray);
            return y + (int)Game1.smallFont.MeasureString(parsedTip).Y + 10;
        }

        public void ReceiveLeftClick(int x, int y) { /* no interactive elements */ }

        public void PerformHoverAction(int x, int y) { /* no tooltips */ }

        // ── helpers ───────────────────────────────────────────────────────────

        private static void DrawKeyValue(SpriteBatch b, string key, string value, int x, int y, int valueOffset = -1, Color? valueColor = null)
        {
            b.DrawString(Game1.smallFont, key, new Vector2(x, y), Game1.textColor);
            float offset = valueOffset > 0 ? valueOffset : Game1.smallFont.MeasureString(key).X + 10;
            Color vColor = valueColor ?? new Color(60, 60, 60);
            b.DrawString(Game1.smallFont, value, new Vector2(x + offset, y), vColor);
        }
    }
}
