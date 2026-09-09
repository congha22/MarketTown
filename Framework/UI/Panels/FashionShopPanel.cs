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
        public string HoverText { get; private set; }
        private Rectangle _capacityArea;

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
            int capacityStartY = y;
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

            _capacityArea = new Rectangle(x, capacityStartY, width, y - capacityStartY);

            // ── Tip ───────────────────────────────────────────────────────────
            string parsedTip = Game1.parseText("Tip: Place mannequins with clothing for customers to buy.", Game1.smallFont, width);
            b.DrawString(Game1.smallFont, parsedTip, new Vector2(x, y), Color.DimGray);
            return y + (int)Game1.smallFont.MeasureString(parsedTip).Y + 10;
        }

        public void ReceiveLeftClick(int x, int y) { /* no interactive elements */ }
        public void PerformHoverAction(int x, int y)
        {
            if (_capacityArea.Contains(x, y))
            {
                HoverText = "Base capacity depends on how large the shop is, capped at 12.\nIncrease capacity by progressing, selling more, and well-decorating the shop.";
            }
            else
            {
                HoverText = null;
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static void DrawKeyValue(SpriteBatch b, string key, string value, int x, int y, int valueOffset = -1, Color? valueColor = null)
        {
            b.DrawString(Game1.smallFont, key, new Vector2(x, y), Game1.textColor);
            float offset = valueOffset > 0 ? valueOffset : Game1.smallFont.MeasureString(key).X + 10;
            Color vColor = valueColor ?? new Color(60, 60, 60);
            b.DrawString(Game1.smallFont, value, new Vector2(x + offset, y), vColor);
        }
    }
}
