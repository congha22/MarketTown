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

        public int Draw(SpriteBatch b, int x, int y, int width, int height)
        {
            int lineHeight = 35;

            // ── Theme info ────────────────────────────────────────────────────
            string parsedThemeInfo = Game1.parseText("Fashion Boutique — +25% on clothing, hats & boots", Game1.smallFont, width);
            b.DrawString(Game1.smallFont, parsedThemeInfo, new Vector2(x, y), new Color(120, 60, 140));
            y += (int)Game1.smallFont.MeasureString(parsedThemeInfo).Y + 5;

            // ── Fitting Booth count ───────────────────────────────────────────
            Color boothColor = _boothCount > 0 ? new Color(40, 140, 60) : Color.OrangeRed;
            string boothText = _boothCount > 0
                ? $"Fitting Booths installed: {_boothCount}"
                : "No Fitting Booths installed! Customers can't try on clothes.";
            string parsedBoothText = Game1.parseText(boothText, Game1.smallFont, width);
            b.DrawString(Game1.smallFont, parsedBoothText, new Vector2(x, y), boothColor);
            y += (int)Game1.smallFont.MeasureString(parsedBoothText).Y + 10;

            // Divider
            b.Draw(Game1.fadeToBlackRect, new Rectangle(x, y, width, 2), Color.SlateGray * 0.5f);
            y += 15;

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
            string parsedTip = Game1.parseText("Tip: Place mannequins with clothing for customers to buy.", Game1.smallFont, width);
            b.DrawString(Game1.smallFont, parsedTip, new Vector2(x, y), Color.DimGray);
            return y + (int)Game1.smallFont.MeasureString(parsedTip).Y + 10;
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
