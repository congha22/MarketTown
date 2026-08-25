using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.ItemTypeDefinitions;
using StardewValley.Menus;
using MarketTown.Framework.Models;

namespace MarketTown.Framework.UI
{
    public class ItemStatsView
    {
        private readonly Dictionary<string, ItemSaleStats> _itemStats;
        private readonly Dictionary<int, CategorySaleStats> _catStats;
        private readonly Rectangle _bounds;

        private enum FilterCat { All, Agriculture, Artisan, Fish, Mining, Fashion, Misc }
        private FilterCat _currentCat = FilterCat.All;

        private enum SortMode { Total, Name, Earnings }
        private SortMode _currentSort = SortMode.Total;

        private List<ClickableComponent> _catButtons = new List<ClickableComponent>();
        private ClickableComponent _sortBtn;
        private List<KeyValuePair<string, ItemSaleStats>> _filteredItems = new List<KeyValuePair<string, ItemSaleStats>>();
        private int _scrollIndex = 0;

        private StardewValley.Menus.ClickableTextureComponent _upButton;
        private StardewValley.Menus.ClickableTextureComponent _downButton;
        private bool _scrolling = false;

        public ItemStatsView(Dictionary<string, ItemSaleStats> itemStats, Dictionary<int, CategorySaleStats> catStats, Rectangle bounds)
        {
            _itemStats = itemStats;
            _catStats = catStats;
            _bounds = new Rectangle(bounds.X, bounds.Y, bounds.Width - 30, bounds.Height);

            _upButton = new StardewValley.Menus.ClickableTextureComponent(
                new Rectangle(_bounds.Right + 20, _bounds.Y + 140, 44, 48),
                Game1.mouseCursors,
                new Rectangle(421, 459, 11, 12), 4f);

            _downButton = new StardewValley.Menus.ClickableTextureComponent(
                new Rectangle(_bounds.Right + 20, _bounds.Y + 140 + (130 * 5) - 48, 44, 48),
                Game1.mouseCursors,
                new Rectangle(421, 472, 11, 12), 4f);

            int bx = _bounds.X;
            int by = _bounds.Y + 100;
            int bw = 118;

            _catButtons.Add(new ClickableComponent(new Rectangle(bx, by, bw, 40), "All"));
            _catButtons.Add(new ClickableComponent(new Rectangle(bx + bw * 1, by, bw, 40), "Farming"));
            _catButtons.Add(new ClickableComponent(new Rectangle(bx + bw * 2, by, bw, 40), "Artisan"));
            _catButtons.Add(new ClickableComponent(new Rectangle(bx + bw * 3, by, bw, 40), "Fish"));
            _catButtons.Add(new ClickableComponent(new Rectangle(bx + bw * 4, by, bw, 40), "Mining"));
            _catButtons.Add(new ClickableComponent(new Rectangle(bx + bw * 5, by, bw, 40), "Fashion"));
            _catButtons.Add(new ClickableComponent(new Rectangle(bx + bw * 6, by, bw, 40), "Misc"));

            _sortBtn = new ClickableComponent(new Rectangle(bx + bw * 7 + 20, by, 120, 40), "Total");

            UpdateFilteredItems();
        }

        private void UpdateFilteredItems()
        {
            _scrollIndex = 0;
            var allowedCats = GetAllowedCategories(_currentCat);

            if (_currentCat == FilterCat.All)
            {
                _filteredItems = _itemStats.ToList();
            }
            else
            {
                _filteredItems = _itemStats.Where(kvp =>
                {
                    var data = ItemRegistry.GetDataOrErrorItem(kvp.Key);
                    return allowedCats.Contains(data.Category);
                }).ToList();
            }

            if (_currentSort == SortMode.Total)
                _filteredItems.Sort((a, b) => b.Value.TotalSold.CompareTo(a.Value.TotalSold));
            else if (_currentSort == SortMode.Earnings)
                _filteredItems.Sort((a, b) => b.Value.TotalEarnings.CompareTo(a.Value.TotalEarnings));
            else if (_currentSort == SortMode.Name)
                _filteredItems.Sort((a, b) => ItemRegistry.GetDataOrErrorItem(a.Key).DisplayName.CompareTo(ItemRegistry.GetDataOrErrorItem(b.Key).DisplayName));
        }

        private HashSet<int> GetAllowedCategories(FilterCat cat)
        {
            return cat switch
            {
                FilterCat.Agriculture => new HashSet<int> { -81, -80, -79, -75, -74, -19 },
                FilterCat.Artisan => new HashSet<int> { -26, -27, -18, -17, -6, -5 },
                FilterCat.Fish => new HashSet<int> { -4, -23, -22, -21 },
                FilterCat.Mining => new HashSet<int> { -12, -2, -15, 0 },
                FilterCat.Fashion => new HashSet<int> { -95, -100 },
                FilterCat.Misc => new HashSet<int> { -102, -28, -8, -7, -16, -20 },
                _ => new HashSet<int>()
            };
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

            for (int i = 0; i < _catButtons.Count; i++)
            {
                if (_catButtons[i].containsPoint(x, y))
                {
                    _currentCat = (FilterCat)i;
                    Game1.playSound("smallSelect");
                    UpdateFilteredItems();
                    return;
                }
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
                StardewValley.Menus.IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(403, 383, 6, 6), _upButton.bounds.X + 12, _upButton.bounds.Bottom + 4, 20, _downButton.bounds.Y - _upButton.bounds.Bottom - 8, Color.White, 4f, false);

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

            // Draw Category Buttons
            for (int i = 0; i < _catButtons.Count; i++)
            {
                var btn = _catButtons[i];
                bool isSelected = (int)_currentCat == i;
                IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(384, 396, 15, 15), btn.bounds.X, btn.bounds.Y, btn.bounds.Width, btn.bounds.Height, isSelected ? Color.Wheat : Color.White, 2f, false);
                Utility.drawTextWithShadow(b, btn.name, Game1.smallFont, new Vector2(btn.bounds.X + 10, btn.bounds.Y + 8), Game1.textColor);
            }

            // Draw Sort Button
            IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(384, 396, 15, 15), _sortBtn.bounds.X, _sortBtn.bounds.Y, _sortBtn.bounds.Width, _sortBtn.bounds.Height, Color.LightBlue, 2f, false);
            Utility.drawTextWithShadow(b, _sortBtn.name, Game1.smallFont, new Vector2(_sortBtn.bounds.X + 10, _sortBtn.bounds.Y + 8), Game1.textColor);

            // Calculate and draw summary for category
            int totalSold = 0;
            int totalEarned = 0;

            if (_currentCat == FilterCat.All)
            {
                totalSold = _catStats.Values.Sum(s => s.TotalSold);
                totalEarned = _catStats.Values.Sum(s => s.TotalEarnings);
            }
            else
            {
                var cats = GetAllowedCategories(_currentCat);
                foreach (var catId in cats)
                {
                    if (_catStats.TryGetValue(catId, out var s))
                    {
                        totalSold += s.TotalSold;
                        totalEarned += s.TotalEarnings;
                    }
                }
            }

            string summaryStr = $"Category Total: {totalSold} sold | {totalEarned}g earned";
            Utility.drawTextWithShadow(b, summaryStr, Game1.smallFont, new Vector2(_bounds.X, _bounds.Y + 150), Game1.textColor);

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

                DrawItemCell(b, item.Key, item.Value, new Rectangle(x, y, cellWidth, cellHeight));
            }
        }

        private void DrawItemCell(SpriteBatch b, string itemId, ItemSaleStats stats, Rectangle cell)
        {
            IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(384, 396, 15, 15), cell.X, cell.Y, cell.Width, cell.Height, Color.White, 4f, false);

            ParsedItemData itemData = ItemRegistry.GetDataOrErrorItem(itemId);
            b.Draw(itemData.GetTexture(), new Rectangle(cell.X + 15, cell.Y + 30, 64, 64), itemData.GetSourceRect(), Color.White);

            string nameStr = itemData.DisplayName;
            b.DrawString(Game1.smallFont, nameStr, new Vector2(cell.X + 85, cell.Y + 20), Game1.textColor, 0f, Vector2.Zero, 1f, SpriteEffects.None, 1f);

            string statsStr = $"Sold: {stats.TotalSold} | Earned: {stats.TotalEarnings}g";
            b.DrawString(Game1.smallFont, statsStr, new Vector2(cell.X + 85, cell.Y + 52), Game1.textColor * 0.8f, 0f, Vector2.Zero, 0.65f, SpriteEffects.None, 1f);

            // Draw Qualities
            int currentY = cell.Y + 75;
            int curX = cell.X + 85;

            // Empty/Normal (Black Star)
            b.Draw(Game1.mouseCursors, new Rectangle(curX, currentY + 3, 16, 16), new Rectangle(338, 400, 8, 8), Color.Black);
            curX += 20;
            string normalText = $"{stats.TotalRegular} ";
            b.DrawString(Game1.smallFont, normalText, new Vector2(curX, currentY), Game1.textColor * 0.8f, 0f, Vector2.Zero, 0.75f, SpriteEffects.None, 1f);
            curX += (int)(Game1.smallFont.MeasureString(normalText).X * 0.75f);

            // Silver
            b.Draw(Game1.mouseCursors, new Rectangle(curX, currentY + 3, 16, 16), new Rectangle(338, 400, 8, 8), Color.White);
            curX += 20;
            string silverText = $"{stats.TotalSilver} ";
            b.DrawString(Game1.smallFont, silverText, new Vector2(curX, currentY), Game1.textColor * 0.8f, 0f, Vector2.Zero, 0.75f, SpriteEffects.None, 1f);
            curX += (int)(Game1.smallFont.MeasureString(silverText).X * 0.75f);

            // Gold
            b.Draw(Game1.mouseCursors, new Rectangle(curX, currentY + 3, 16, 16), new Rectangle(346, 400, 8, 8), Color.White);
            curX += 20;
            string goldText = $"{stats.TotalGold} ";
            b.DrawString(Game1.smallFont, goldText, new Vector2(curX, currentY), Game1.textColor * 0.8f, 0f, Vector2.Zero, 0.75f, SpriteEffects.None, 1f);
            curX += (int)(Game1.smallFont.MeasureString(goldText).X * 0.75f);

            // Iridium
            b.Draw(Game1.mouseCursors, new Rectangle(curX, currentY + 3, 16, 16), new Rectangle(346, 392, 8, 8), Color.White);
            curX += 20;
            string iriText = $"{stats.TotalIridium}";
            b.DrawString(Game1.smallFont, iriText, new Vector2(curX, currentY), Game1.textColor * 0.8f, 0f, Vector2.Zero, 0.75f, SpriteEffects.None, 1f);
        }
    }
}
