using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using MarketTown.Framework.Models;
using MarketTown.Framework.Services;

namespace MarketTown.Framework.UI.Panels
{
    /// <summary>
    /// UI panel shown in the Store Manager menu when the shop is set to "Discovery Shop".
    /// Displays capacity scores, the number of seats available for reading, and a short tips line.
    /// </summary>
    public class DiscoveryShopPanel : IShopMenuPanel
    {
        private readonly StoreCapacityScores _capacityScores;
        private readonly int _seatCount;

        public DiscoveryShopPanel(GameLocation location, StoreStatsService statsService, IndoorVisitorService visitorService)
        {
            _capacityScores = visitorService.GetStoreScores(location);
            // Count total seats available on furniture
            _seatCount = location.furniture.Sum(f => f.GetSeatCapacity());
        }

        public void Draw(SpriteBatch b, int x, int y, int width, int height)
        {
            int lineHeight = 35;

            // ── Theme info ────────────────────────────────────────────────────
            Utility.drawTextWithShadow(b,
                "Discovery Shop — +25% on books, gems & minerals",
                Game1.smallFont, new Vector2(x, y), new Color(60, 100, 160));
            y += lineHeight + 5;

            // ── Seat count ───────────────────────────────────────────
            Color seatColor = _seatCount > 0 ? new Color(40, 140, 60) : Color.OrangeRed;
            string seatText = _seatCount > 0
                ? $"Reading Seats available: {_seatCount}"
                : "No chairs installed! Customers can't sit to read books.";
            Utility.drawTextWithShadow(b, seatText, Game1.smallFont, new Vector2(x, y), seatColor);
            y += lineHeight + 10;

            // ── Capacity Scores ───────────────────────────────────────────────
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

            // ── Tip ───────────────────────────────────────────────────────────
            Utility.drawTextWithShadow(b,
                "Tip: Make sure chairs have an open tile next to them for customers to path to.",
                Game1.smallFont, new Vector2(x, y), Color.DimGray);
        }

        public void ReceiveLeftClick(int x, int y) { /* no interactive elements */ }
        public void PerformHoverAction(int x, int y) { /* no tooltips */ }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static void DrawKeyValue(SpriteBatch b, string key, string value, int x, int y, Color? valueColor = null)
        {
            Utility.drawTextWithShadow(b, key, Game1.smallFont, new Vector2(x, y), Game1.textColor);
            Vector2 keySize = Game1.smallFont.MeasureString(key);
            Color vColor = valueColor ?? Game1.textShadowColor;
            b.DrawString(Game1.smallFont, value, new Vector2(x + keySize.X + 10, y), vColor);
        }
    }
}
