using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using MarketTown.Framework.Models;
using MarketTown.Framework.Services;

namespace MarketTown.Framework.UI.Panels
{
    /// <summary>
    /// UI panel shown in the Store Manager menu when the shop is set to "Fashion Boutique".
    /// Displays capacity scores, the number of Fitting Booths installed, and a short tips line.
    /// </summary>
    public class FashionShopPanel : IShopMenuPanel
    {
        private const string BOOTH_ID = "d5a1lamdtd.MarketTown_FittingBooth";

        private readonly StoreCapacityScores _capacityScores;
        private readonly int _boothCount;

        public FashionShopPanel(GameLocation location, StoreStatsService statsService, IndoorVisitorService visitorService)
        {
            _capacityScores = visitorService.GetStoreScores(location);
            _boothCount = location.furniture.Count(f => f.ItemId == BOOTH_ID);
        }

        public void Draw(SpriteBatch b, int x, int y, int width, int height)
        {
            int lineHeight = 35;

            // ── Theme info ────────────────────────────────────────────────────
            Utility.drawTextWithShadow(b,
                "Fashion Boutique — +25% on clothing, hats & boots",
                Game1.smallFont, new Vector2(x, y), new Color(120, 60, 140));
            y += lineHeight + 5;

            // ── Fitting Booth count ───────────────────────────────────────────
            Color boothColor = _boothCount > 0 ? new Color(40, 140, 60) : Color.OrangeRed;
            string boothText = _boothCount > 0
                ? $"Fitting Booths installed: {_boothCount}"
                : "No Fitting Booths installed! Customers can't try on clothes.";
            Utility.drawTextWithShadow(b, boothText, Game1.smallFont, new Vector2(x, y), boothColor);
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
                "Tip: Place mannequins with clothing for customers to buy.",
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
