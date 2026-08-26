using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.ItemTypeDefinitions;
using System.Linq;
using MarketTown.Framework.Models;

namespace MarketTown.Framework.UI
{
    public class SalesLogView
    {
        private readonly IReadOnlyList<SaleRecord> _sales;
        private readonly Rectangle _bounds;
        private int _scrollIndex = 0;

        private StardewValley.Menus.ClickableTextureComponent _upButton;
        private StardewValley.Menus.ClickableTextureComponent _downButton;
        private bool _scrolling = false;

        public SalesLogView(IReadOnlyList<SaleRecord> sales, Rectangle bounds)
        {
            _sales = sales;
            _bounds = new Rectangle(bounds.X, bounds.Y + 140, bounds.Width - 30, bounds.Height - 160);

            _upButton = new StardewValley.Menus.ClickableTextureComponent(
                new Rectangle(_bounds.Right + 20, _bounds.Y, 44, 48),
                Game1.mouseCursors,
                new Rectangle(421, 459, 11, 12), 4f);

            _downButton = new StardewValley.Menus.ClickableTextureComponent(
                new Rectangle(_bounds.Right + 20, _bounds.Y + (130 * 5) - 48, 44, 48),
                Game1.mouseCursors,
                new Rectangle(421, 472, 11, 12), 4f);
        }

        public void ReceiveLeftClick(int x, int y)
        {
            if (_upButton.containsPoint(x, y))
            {
                ReceiveScrollWheelAction(1);
                Game1.playSound("shwip");
            }
            else if (_downButton.containsPoint(x, y))
            {
                ReceiveScrollWheelAction(-1);
                Game1.playSound("shwip");
            }
            else
            {
                Rectangle scrollTrack = new Rectangle(_upButton.bounds.X, _upButton.bounds.Bottom, _upButton.bounds.Width, _downButton.bounds.Y - _upButton.bounds.Bottom);
                if (scrollTrack.Contains(x, y))
                {
                    _scrolling = true;
                    LeftClickHeld(x, y);
                }
            }
        }

        public void LeftClickHeld(int x, int y)
        {
            if (_scrolling)
            {
                int trackHeight = _downButton.bounds.Y - _upButton.bounds.Bottom - 40;
                int yPos = y - _upButton.bounds.Bottom - 20; // 20 is half thumb height
                float progress = Math.Clamp((float)yPos / trackHeight, 0f, 1f);

                int totalRows = (int)Math.Ceiling(_sales.Count / 3f);
                int maxScrollRows = totalRows - 7;
                if (maxScrollRows > 0)
                {
                    _scrollIndex = (int)Math.Round(progress * maxScrollRows) * 3;
                }
            }
        }

        public void ReleaseLeftClick(int x, int y)
        {
            _scrolling = false;
        }

        public void PerformHoverAction(int x, int y)
        {
            _upButton.tryHover(x, y);
            _downButton.tryHover(x, y);
        }

        public void ReceiveScrollWheelAction(int direction)
        {
            if (direction > 0 && _scrollIndex > 0)
                _scrollIndex -= 3;
            else if (direction < 0 && _scrollIndex < _sales.Count - 21)
                _scrollIndex += 3;

            if (_scrollIndex < 0) _scrollIndex = 0;
        }

        public void Draw(SpriteBatch b)
        {
            if (_sales.Count == 0)
            {
                Utility.drawTextWithShadow(b, "No sales to display.", Game1.dialogueFont, new Vector2(_bounds.X + 50, _bounds.Y + 50), Game1.textColor);
                return;
            }

            if (_sales.Count > 21)
            {
                _upButton.draw(b);
                _downButton.draw(b);

                // Draw scroll track
                StardewValley.Menus.IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(403, 383, 6, 6), _upButton.bounds.X + 12, _upButton.bounds.Bottom + 4, 20, _downButton.bounds.Y - _upButton.bounds.Bottom - 8, Color.White, 4f, false);

                // Draw thumb
                int totalRows = (int)Math.Ceiling(_sales.Count / 3f);
                int maxScrollRows = totalRows - 7;
                if (maxScrollRows > 0)
                {
                    int currentRow = _scrollIndex / 3;
                    float progress = (float)currentRow / maxScrollRows;
                    int trackHeight = _downButton.bounds.Y - _upButton.bounds.Bottom - 48;
                    int thumbY = _upButton.bounds.Bottom + 4 + (int)(progress * trackHeight);
                    b.Draw(Game1.mouseCursors, new Rectangle(_upButton.bounds.X + 10, thumbY, 24, 40), new Rectangle(435, 463, 6, 10), Color.White);
                }
            }

            int totalSold = _sales.Count;
            int totalEarned = _sales.Sum(s => s.SoldPrice);
            string summaryStr = $"Total: {totalSold} sold | {totalEarned}g earned";
            Utility.drawTextWithShadow(b, summaryStr, Game1.smallFont, new Vector2(_bounds.X, _bounds.Y - 40), Game1.textColor, scale: 1.2f);

            int totalLove = _sales.Count(s => s.GiftTaste == NPC.gift_taste_love);
            int totalLike = _sales.Count(s => s.GiftTaste == NPC.gift_taste_like);
            int totalNeutral = _sales.Count(s => s.GiftTaste == NPC.gift_taste_neutral);
            int totalDislike = _sales.Count(s => s.GiftTaste == NPC.gift_taste_dislike);
            int totalHate = _sales.Count(s => s.GiftTaste == NPC.gift_taste_hate);

            int curX = _bounds.Right - 350;
            int currentY = _bounds.Y - 35;
            DrawTaste(b, NPC.gift_taste_love, totalLove, ref curX, currentY);
            DrawTaste(b, NPC.gift_taste_like, totalLike, ref curX, currentY);
            DrawTaste(b, NPC.gift_taste_neutral, totalNeutral, ref curX, currentY);
            DrawTaste(b, NPC.gift_taste_dislike, totalDislike, ref curX, currentY);
            DrawTaste(b, NPC.gift_taste_hate, totalHate, ref curX, currentY);

            int cellWidth = _bounds.Width / 3;
            int cellHeight = 90;

            for (int i = 0; i < 21; i++) // Show up to 21 cells (3 cols x 7 rows)
            {
                int dataIndex = _scrollIndex + i;
                if (dataIndex >= _sales.Count) break;

                var sale = _sales[dataIndex];

                int row = i / 3;
                int col = i % 3;

                int x = _bounds.X + (col * cellWidth);
                int y = _bounds.Y + (row * cellHeight);

                DrawSaleCell(b, sale, new Rectangle(x, y, cellWidth, cellHeight));
            }
        }

        private void DrawSaleCell(SpriteBatch b, SaleRecord sale, Rectangle cell)
        {
            // Draw background
            StardewValley.Menus.IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(384, 396, 15, 15), cell.X, cell.Y, cell.Width, cell.Height, Color.White, 4f, false);

            // NPC Portrait
            var npc = Game1.getCharacterFromName(sale.BuyerName);
            if (npc != null)
            {
                // Draw their overworld sprite (first frame) at 3x scale
                b.Draw(npc.Sprite.Texture, new Vector2(cell.X + 15, cell.Y + 5), new Rectangle(0, 0, 16, 24), Color.White, 0f, Vector2.Zero, 3f, SpriteEffects.None, 1f);

                // Emote (taste) overlay
                int emoteIndex = sale.GiftTaste switch
                {
                    NPC.gift_taste_love => 20,
                    NPC.gift_taste_like => 56,
                    NPC.gift_taste_dislike => 4,
                    NPC.gift_taste_hate => 36,
                    _ => 32
                };
                Rectangle emoteSource = new Rectangle((emoteIndex % 4) * 16, (emoteIndex / 4) * 16, 16, 16);
                b.Draw(Game1.emoteSpriteSheet, new Rectangle(cell.X + 50, cell.Y + 10, 32, 32), emoteSource, Color.White);
            }

            // Item Icon
            ParsedItemData itemData = ItemRegistry.GetDataOrErrorItem(sale.QualifiedItemId);
            if (itemData != null)
            {
                b.Draw(itemData.GetTexture(), new Rectangle(cell.X + 90, cell.Y + 15, 64, 64), itemData.GetSourceRect(), Color.White);

                // Quality star
                if (sale.Quality > 0)
                {
                    Rectangle starSource = sale.Quality switch
                    {
                        1 => new Rectangle(338, 400, 8, 8),
                        2 => new Rectangle(346, 400, 8, 8),
                        _ => new Rectangle(346, 392, 8, 8) // Iridium
                    };
                    b.Draw(Game1.mouseCursors, new Rectangle(cell.X + 90 + 40, cell.Y + 15 + 40, 24, 24), starSource, Color.White);
                }
            }

            // Price
            string priceStr = sale.SoldPrice.ToString();
            Vector2 priceSize = Game1.dialogueFont.MeasureString(priceStr);
            Utility.drawTextWithShadow(b, priceStr, Game1.dialogueFont, new Vector2(cell.Right - priceSize.X - 55, cell.Y + cell.Height / 2 - priceSize.Y / 2), Game1.textColor, scale: 0.9f);

            // Gold Icon
            b.Draw(Game1.mouseCursors, new Vector2(cell.Right - 57, cell.Y + cell.Height / 2 - 30), new Rectangle(280, 410, 16, 16), Color.White, 0f, Vector2.Zero, 3f, SpriteEffects.None, 1f);
        }

        private void DrawTaste(SpriteBatch b, int taste, int count, ref int x, int y)
        {
            int emoteIndex = taste switch
            {
                NPC.gift_taste_love => 20,
                NPC.gift_taste_like => 56,
                NPC.gift_taste_dislike => 4,
                NPC.gift_taste_hate => 36,
                _ => 32 // Neutral
            };
            Rectangle emoteSource = new Rectangle((emoteIndex % 4) * 16, (emoteIndex / 4) * 16, 16, 16);
            b.Draw(Game1.emoteSpriteSheet, new Rectangle(x, y, 24, 24), emoteSource, Color.White);

            x += 30;
            string text = $"{count} ";
            b.DrawString(Game1.smallFont, text, new Vector2(x, y + 2), Game1.textColor * 0.8f, 0f, Vector2.Zero, 0.9f, SpriteEffects.None, 1f);
            x += (int)(Game1.smallFont.MeasureString(text).X * 0.75f) + 5;
        }
    }
}
