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
            string parsedThemeInfo = Game1.parseText("Discovery Shop — +25% on books, gems & minerals", Game1.smallFont, width);
            b.DrawString(Game1.smallFont, parsedThemeInfo, new Vector2(x, y), new Color(60, 100, 160));
            y += (int)Game1.smallFont.MeasureString(parsedThemeInfo).Y + 5;

            // ── Seat count ───────────────────────────────────────────
            Color seatColor = _seatCount > 0 ? new Color(40, 140, 60) : Color.OrangeRed;
            string seatText = _seatCount > 0
                ? $"Reading Seats available: {_seatCount}"
                : "No chairs installed! Customers can't sit to read books.";
            string parsedSeatText = Game1.parseText(seatText, Game1.smallFont, width);
            b.DrawString(Game1.smallFont, parsedSeatText, new Vector2(x, y), seatColor);
            y += (int)Game1.smallFont.MeasureString(parsedSeatText).Y + 10;

            // ── Capacity Scores ───────────────────────────────────────────────
            b.DrawString(Game1.smallFont, $"Capacity limit: {_capacityScores.MaxCapacity}", new Vector2(x, y), Game1.textColor);
            y += lineHeight;

            DrawKeyValue(b, "Shop level:", $"{_capacityScores.ShopLevelScore * 100:0}% (Max 50%)", x + 20, y);
            y += lineHeight;
            DrawKeyValue(b, "Stock available:", $"{_capacityScores.SellingScore * 100:0}% (Max 30%)", x + 20, y);
            y += lineHeight;
            DrawKeyValue(b, "Decoration:", $"{_capacityScores.DecorationScore * 100:0}% (Max 20%)", x + 20, y);
            y += lineHeight + 10;

            // ── Tip ───────────────────────────────────────────────────────────
            string parsedTip = Game1.parseText("Tip: Make sure chairs have an open tile next to them for customers to path to.", Game1.smallFont, width);
            b.DrawString(Game1.smallFont, parsedTip, new Vector2(x, y), Color.DimGray);
        }

        public void ReceiveLeftClick(int x, int y) { /* no interactive elements */ }
        public void PerformHoverAction(int x, int y) { /* no tooltips */ }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static void DrawKeyValue(SpriteBatch b, string key, string value, int x, int y, Color? valueColor = null)
        {
            b.DrawString(Game1.smallFont, key, new Vector2(x, y), Game1.textColor);
            Vector2 keySize = Game1.smallFont.MeasureString(key);
            Color vColor = valueColor ?? new Color(60, 60, 60);
            b.DrawString(Game1.smallFont, value, new Vector2(x + keySize.X + 10, y), vColor);
        }
    }
}
