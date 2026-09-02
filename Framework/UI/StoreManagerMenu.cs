using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Objects;
using MarketTown.Framework.Services;
using MarketTown.Framework.Models;

namespace MarketTown.Framework.UI
{
    public class StoreManagerMenu : IClickableMenu
    {
        private readonly StoreStatsService _storeStatsService;
        private readonly IndoorVisitorService _visitorService;
        private readonly StoreEmployeeService _employeeService;
        private readonly GameLocation _location;
        private readonly IModHelper _helper;

        private StoreStatRecord _storeStats;
        private StoreCapacityScores _capacityScores;
        
        private readonly List<Furniture> _checkouts;
        private readonly Dictionary<ClickableComponent, Vector2> _hireButtons = new Dictionary<ClickableComponent, Vector2>();

        public StoreManagerMenu(StoreStatsService storeStatsService, IndoorVisitorService visitorService, StoreEmployeeService employeeService, GameLocation location, IModHelper helper)
        {
            _storeStatsService = storeStatsService;
            _visitorService = visitorService;
            _employeeService = employeeService;
            _location = location;
            _helper = helper;

            this.width = 800;
            this.height = 600;

            this.xPositionOnScreen = Game1.uiViewport.Width / 2 - this.width / 2;
            this.yPositionOnScreen = Game1.uiViewport.Height / 2 - this.height / 2;

            this.upperRightCloseButton = new ClickableTextureComponent(
                new Rectangle(this.xPositionOnScreen + this.width - 36, this.yPositionOnScreen - 8, 48, 48),
                Game1.mouseCursors,
                new Rectangle(337, 494, 12, 12), 4f);

            // Fetch data
            if (_storeStatsService.StoreStats.TryGetValue(_location.NameOrUniqueName, out var stats))
            {
                _storeStats = stats;
            }
            else
            {
                _storeStats = new StoreStatRecord(); // Empty default if no stats yet
            }

            _capacityScores = _visitorService.GetStoreScores(_location);

            // Fetch checkouts
            _checkouts = _location.furniture.Where(f => f.ItemId == "d5a1lamdtd.MarketTown_CheckoutSmall" || f.ItemId == "d5a1lamdtd.MarketTown_CheckoutLarge").ToList();
            
            // Create buttons
            int startY = this.yPositionOnScreen + 160;
            foreach (var checkout in _checkouts)
            {
                var btn = new ClickableComponent(new Rectangle(this.xPositionOnScreen + 500, startY, 150, 40), "HireBtn");
                _hireButtons[btn] = checkout.TileLocation;
                startY += 60;
            }
        }

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            base.receiveLeftClick(x, y, playSound);

            if (this.upperRightCloseButton != null && this.upperRightCloseButton.containsPoint(x, y))
            {
                this.exitThisMenu(playSound);
                return;
            }

            foreach (var kvp in _hireButtons)
            {
                if (kvp.Key.containsPoint(x, y))
                {
                    Game1.playSound("drumkit6");
                    Game1.activeClickableMenu = new EmployeeSelectionMenu(_employeeService, _location, kvp.Value, (hiredNpc) =>
                    {
                        // Re-open this menu after hiring
                        Game1.activeClickableMenu = new StoreManagerMenu(_storeStatsService, _visitorService, _employeeService, _location, _helper);
                    });
                    return;
                }
            }
        }

        public override void performHoverAction(int x, int y)
        {
            base.performHoverAction(x, y);
            this.upperRightCloseButton?.tryHover(x, y);
        }

        public override void draw(SpriteBatch b)
        {
            b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.4f);

            // Draw main background
            Game1.drawDialogueBox(this.xPositionOnScreen, this.yPositionOnScreen, this.width, this.height, false, true);

            // Title
            string title = $"Store Manager - {_location.Name}";
            Vector2 titleSize = Game1.dialogueFont.MeasureString(title);
            Utility.drawTextWithShadow(b, title, Game1.dialogueFont, new Vector2(this.xPositionOnScreen + this.width / 2 - titleSize.X / 2, this.yPositionOnScreen + 90), Game1.textColor);

            int startX = this.xPositionOnScreen + 60;
            int startY = this.yPositionOnScreen + 160;
            int lineHeight = 40;

            // Store Statistics Section
            Utility.drawTextWithShadow(b, "Store Statistics:", Game1.smallFont, new Vector2(startX, startY), Game1.textColor);
            startY += lineHeight;

            DrawKeyValue(b, "Total Visitors:", _storeStats.TotalVisitors.ToString(), startX + 20, startY);
            startY += lineHeight;
            DrawKeyValue(b, "Total Items Sold:", _storeStats.TotalSoldItems.ToString(), startX + 20, startY);
            startY += lineHeight;
            DrawKeyValue(b, "Total Earnings:", $"{_storeStats.TotalEarnings}g", startX + 20, startY);
            startY += lineHeight + 20;

            // Capacity Scores Section
            Utility.drawTextWithShadow(b, "Capacity Scores:", Game1.smallFont, new Vector2(startX, startY), Game1.textColor);
            startY += lineHeight;

            DrawKeyValue(b, "Shop Level Score:", $"{_capacityScores.ShopLevelScore * 100:0}% (Max 50%)", startX + 20, startY);
            startY += lineHeight;
            DrawKeyValue(b, "Selling Nodes Score:", $"{_capacityScores.SellingScore * 100:0}% (Max 30%)", startX + 20, startY);
            startY += lineHeight;
            DrawKeyValue(b, "Decoration Score:", $"{_capacityScores.DecorationScore * 100:0}% (Max 20%)", startX + 20, startY);
            startY += lineHeight;

            DrawKeyValue(b, "Max Customers:", _capacityScores.MaxCapacity.ToString(), startX + 20, startY, Color.DarkBlue);

            // Employees Section
            int rightX = this.xPositionOnScreen + 450;
            int rightY = this.yPositionOnScreen + 160;
            
            if (_checkouts.Count > 0)
            {
                foreach (var checkout in _checkouts)
                {
                    string type = checkout.ItemId == "d5a1lamdtd.MarketTown_CheckoutSmall" ? "Small" : "Large";
                    Utility.drawTextWithShadow(b, $"{type} Checkout:", Game1.smallFont, new Vector2(rightX, rightY), Game1.textColor);
                    
                    // Find button
                    var btn = _hireButtons.FirstOrDefault(kvp => kvp.Value == checkout.TileLocation).Key;
                    if (btn != null)
                    {
                        string hiredNpc = _employeeService.GetHiredEmployee(_location, checkout.TileLocation);
                        string btnText = string.IsNullOrEmpty(hiredNpc) ? "Hire" : hiredNpc;
                        
                        bool isHovered = btn.containsPoint(Game1.getMouseX(), Game1.getMouseY());
                        Color bgColor = isHovered ? Color.Wheat : Color.White;
                        
                        IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(384, 396, 15, 15), btn.bounds.X, btn.bounds.Y, btn.bounds.Width, btn.bounds.Height, bgColor, 4f, false);
                        Utility.drawTextWithShadow(b, btnText, Game1.smallFont, new Vector2(btn.bounds.X + 15, btn.bounds.Y + 5), Game1.textColor);
                    }
                    
                    rightY += 60;
                }
            }

            this.upperRightCloseButton?.draw(b);
            this.drawMouse(b);
        }

        private void DrawKeyValue(SpriteBatch b, string key, string value, int x, int y, Color? valueColor = null)
        {
            Utility.drawTextWithShadow(b, key, Game1.smallFont, new Vector2(x, y), Game1.textColor);
            Vector2 keySize = Game1.smallFont.MeasureString(key);

            Color vColor = valueColor ?? Game1.textShadowColor;
            b.DrawString(Game1.smallFont, value, new Vector2(x + keySize.X + 10, y), vColor);
        }
    }
}
