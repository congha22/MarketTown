using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;
using MarketTown.Framework.Models;

namespace MarketTown.Framework.UI
{
    public class CustomerStatsView
    {
        private readonly Dictionary<string, CustomerSaleStats> _customerStats;
        private readonly Rectangle _bounds;

        private enum SortMode { Total, Name, Earnings }
        private SortMode _currentSort = SortMode.Total;

        private ClickableComponent _sortBtn;
        private List<KeyValuePair<string, CustomerSaleStats>> _filteredItems = new List<KeyValuePair<string, CustomerSaleStats>>();
        private int _scrollIndex = 0;

        private ClickableTextureComponent _upButton;
        private ClickableTextureComponent _downButton;

        private bool _scrolling = false;

        public CustomerStatsView(Dictionary<string, CustomerSaleStats> customerStats, Rectangle bounds)
        {
            _customerStats = customerStats;
            _bounds = new Rectangle(bounds.X, bounds.Y, bounds.Width - 30, bounds.Height);

            _upButton = new ClickableTextureComponent(
                new Rectangle(_bounds.Right + 20, _bounds.Y + 140, 44, 48),
                Game1.mouseCursors,
                new Rectangle(421, 459, 11, 12), 4f);

            _downButton = new ClickableTextureComponent(
                new Rectangle(_bounds.Right + 20, _bounds.Y + 140 + (130 * 5) - 48, 44, 48),
                Game1.mouseCursors,
                new Rectangle(421, 472, 11, 12), 4f);

            int bx = _bounds.X;
            int by = _bounds.Y + 100;

            _sortBtn = new ClickableComponent(new Rectangle(bx + 118 * 7 + 20, by, 120, 40), "Total");

            UpdateFilteredItems();
        }

        private void UpdateFilteredItems()
        {
            _scrollIndex = 0;
            _filteredItems = _customerStats.ToList();

            if (_currentSort == SortMode.Total)
                _filteredItems.Sort((a, b) => b.Value.TotalPurchased.CompareTo(a.Value.TotalPurchased));
            else if (_currentSort == SortMode.Earnings)
                _filteredItems.Sort((a, b) => b.Value.TotalSpent.CompareTo(a.Value.TotalSpent));
            else if (_currentSort == SortMode.Name)
                _filteredItems.Sort((a, b) => (Game1.getCharacterFromName(a.Key)?.displayName ?? a.Key).CompareTo(Game1.getCharacterFromName(b.Key)?.displayName ?? b.Key));
        }

        public void ReceiveLeftClick(int x, int y)
        {
            if (_sortBtn.bounds.Contains(x, y))
            {
                _currentSort = (SortMode)(((int)_currentSort + 1) % 3);
                _sortBtn.name = _currentSort switch { SortMode.Total => "Total", SortMode.Name => "A-Z", _ => "Earning" };
                Game1.playSound("smallSelect");
                UpdateFilteredItems();
                return;
            }

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

                int totalRows = (int)Math.Ceiling(_filteredItems.Count / 3f);
                int maxScrollRows = totalRows - 5;
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
            else if (direction < 0 && _scrollIndex < _filteredItems.Count - 15)
                _scrollIndex += 3;

            if (_scrollIndex < 0) _scrollIndex = 0;
        }

        public void Draw(SpriteBatch b)
        {
            if (_filteredItems.Count > 15)
            {
                _upButton.draw(b);
                _downButton.draw(b);

                // Draw scroll track
                IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(403, 383, 6, 6), _upButton.bounds.X + 12, _upButton.bounds.Bottom + 4, 20, _downButton.bounds.Y - _upButton.bounds.Bottom - 8, Color.White, 4f, false);

                // Draw thumb
                int totalRows = (int)Math.Ceiling(_filteredItems.Count / 3f);
                int maxScrollRows = totalRows - 5;
                if (maxScrollRows > 0)
                {
                    int currentRow = _scrollIndex / 3;
                    float progress = (float)currentRow / maxScrollRows;
                    int trackHeight = _downButton.bounds.Y - _upButton.bounds.Bottom - 48;
                    int thumbY = _upButton.bounds.Bottom + 4 + (int)(progress * trackHeight);
                    b.Draw(Game1.mouseCursors, new Rectangle(_upButton.bounds.X + 10, thumbY, 24, 40), new Rectangle(435, 463, 6, 10), Color.White);
                }
            }

            // Draw Sort Button
            IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(384, 396, 15, 15), _sortBtn.bounds.X, _sortBtn.bounds.Y, _sortBtn.bounds.Width, _sortBtn.bounds.Height, Color.LightBlue, 2f, false);
            Utility.drawTextWithShadow(b, _sortBtn.name, Game1.smallFont, new Vector2(_sortBtn.bounds.X + 10, _sortBtn.bounds.Y + 8), Game1.textColor);

            int totalCustomers = _filteredItems.Count;
            int totalSold = _filteredItems.Sum(s => s.Value.TotalPurchased);
            int totalEarned = _filteredItems.Sum(s => s.Value.TotalSpent);

            string summaryStr = $"Customers: {totalCustomers} | {totalSold} items | {totalEarned}g";
            Utility.drawTextWithShadow(b, summaryStr, Game1.smallFont, new Vector2(_bounds.X, _bounds.Y + 115), Game1.textColor);

            int totalLove = _filteredItems.Sum(s => s.Value.TotalLove);
            int totalLike = _filteredItems.Sum(s => s.Value.TotalLike);
            int totalNeutral = _filteredItems.Sum(s => s.Value.TotalNeutral);
            int totalDislike = _filteredItems.Sum(s => s.Value.TotalDislike);
            int totalHate = _filteredItems.Sum(s => s.Value.TotalHate);

            int currentY = _bounds.Y + 155;
            int curX = _bounds.X;
            DrawTaste(b, NPC.gift_taste_love, totalLove, ref curX, currentY);
            DrawTaste(b, NPC.gift_taste_like, totalLike, ref curX, currentY);
            DrawTaste(b, NPC.gift_taste_neutral, totalNeutral, ref curX, currentY);
            DrawTaste(b, NPC.gift_taste_dislike, totalDislike, ref curX, currentY);
            DrawTaste(b, NPC.gift_taste_hate, totalHate, ref curX, currentY);

            // Draw Item Grid
            int cellWidth = _bounds.Width / 3;
            int cellHeight = 115;
            int startY = _bounds.Y + 195;

            for (int i = 0; i < 15; i++) // Show up to 15 cells (3 cols x 5 rows)
            {
                int dataIndex = _scrollIndex + i;
                if (dataIndex >= _filteredItems.Count) break;

                var item = _filteredItems[dataIndex];

                int row = i / 3;
                int col = i % 3;

                int x = _bounds.X + (col * cellWidth);
                int y = startY + (row * cellHeight);

                DrawCustomerCell(b, item.Key, item.Value, new Rectangle(x, y, cellWidth, cellHeight));
            }
        }

        private void DrawCustomerCell(SpriteBatch b, string npcName, CustomerSaleStats stats, Rectangle cell)
        {
            IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(384, 396, 15, 15), cell.X, cell.Y, cell.Width, cell.Height, Color.White, 4f, false);

            var npc = Game1.getCharacterFromName(npcName);
            string displayName = npc?.displayName ?? npcName;

            if (npc != null)
            {
                b.Draw(npc.Sprite.Texture, new Vector2(cell.X + 23, cell.Y + 8), new Rectangle(0, 0, 16, 24), Color.White, 0f, Vector2.Zero, 2.5f, SpriteEffects.None, 1f);
            }

            b.DrawString(Game1.smallFont, displayName, new Vector2(cell.X + 75, cell.Y + 20), Game1.textColor, 0f, Vector2.Zero, 1f, SpriteEffects.None, 1f);

            string statsStr = $"Items: {stats.TotalPurchased} | Spent: {stats.TotalSpent}g";
            b.DrawString(Game1.smallFont, statsStr, new Vector2(cell.X + 75, cell.Y + 50), Game1.textColor * 0.8f, 0f, Vector2.Zero, 0.65f, SpriteEffects.None, 1f);

            // Draw Taste Stats
            int currentY = cell.Y + 75;
            int curX = cell.X + 25;

            // Draw Tastes (Love, Like, Neutral, Dislike, Hate)
            DrawTaste(b, NPC.gift_taste_love, stats.TotalLove, ref curX, currentY);
            DrawTaste(b, NPC.gift_taste_like, stats.TotalLike, ref curX, currentY);
            DrawTaste(b, NPC.gift_taste_neutral, stats.TotalNeutral, ref curX, currentY);
            DrawTaste(b, NPC.gift_taste_dislike, stats.TotalDislike, ref curX, currentY);
            DrawTaste(b, NPC.gift_taste_hate, stats.TotalHate, ref curX, currentY);
        }

        private void DrawTaste(SpriteBatch b, int taste, int count, ref int x, int y)
        {
            int emoteIndex = taste switch
            {
                NPC.gift_taste_love => 20,
                NPC.gift_taste_like => 32,
                NPC.gift_taste_dislike => 4,
                NPC.gift_taste_hate => 36,
                _ => 56 // Neutral
            };
            Rectangle emoteSource = new Rectangle((emoteIndex % 4) * 16, (emoteIndex / 4) * 16, 16, 16);
            b.Draw(Game1.emoteSpriteSheet, new Rectangle(x, y, 24, 24), emoteSource, Color.White);

            x += 30;
            string text = $"{count} ";
            b.DrawString(Game1.smallFont, text, new Vector2(x, y + 2), Game1.textColor * 0.8f, 0f, Vector2.Zero, 0.75f, SpriteEffects.None, 1f);
            x += (int)(Game1.smallFont.MeasureString(text).X * 0.75f) + 5;
        }
    }
}
