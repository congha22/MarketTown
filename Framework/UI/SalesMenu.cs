using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using MarketTown.Framework.Services;

namespace MarketTown.Framework.UI
{
    public class SalesMenu : IClickableMenu
    {
        private readonly SalesTrackingService _salesTrackingService;
        private readonly IModHelper _helper;

        private enum Tab { Today, Yesterday, Items, Customers }
        private Tab _currentTab = Tab.Today;

        private List<ClickableComponent> _tabs = new List<ClickableComponent>();

        private SalesLogView _todayView;
        private SalesLogView _yesterdayView;
        private ItemStatsView _itemsView;
        private CustomerStatsView _customersView;

        public SalesMenu(SalesTrackingService salesTrackingService, IModHelper helper)
        {
            _salesTrackingService = salesTrackingService;
            _helper = helper;

            this.width = 1250;
            this.height = 800;

            // Center on screen
            this.xPositionOnScreen = Game1.uiViewport.Width / 2 - this.width / 2;
            this.yPositionOnScreen = Game1.uiViewport.Height / 2 - this.height / 2;

            this.upperRightCloseButton = new ClickableTextureComponent(
                new Rectangle(this.xPositionOnScreen + this.width - 36, this.yPositionOnScreen + 20, 48, 48),
                Game1.mouseCursors,
                new Rectangle(337, 494, 12, 12), 4f);

            SetupTabs();

            _todayView = new SalesLogView(_salesTrackingService.TodaySales, new Rectangle(xPositionOnScreen + 240, yPositionOnScreen, width - 240, height));
            _yesterdayView = new SalesLogView(_salesTrackingService.SaveData.YesterdaySales, new Rectangle(xPositionOnScreen + 240, yPositionOnScreen, width - 240, height));
            _itemsView = new ItemStatsView(_salesTrackingService.SaveData.ItemStats, _salesTrackingService.SaveData.CategoryStats, new Rectangle(xPositionOnScreen + 240, yPositionOnScreen, width - 240, height));
            _customersView = new CustomerStatsView(_salesTrackingService.SaveData.CustomerStats, new Rectangle(xPositionOnScreen + 240, yPositionOnScreen, width - 240, height));
        }

        private void SetupTabs()
        {
            int tabX = this.xPositionOnScreen + 40;
            int tabY = this.yPositionOnScreen + 390;
            int tabWidth = 180;
            int tabHeight = 80;

            _tabs.Add(new ClickableComponent(new Rectangle(tabX, tabY, tabWidth, tabHeight), "Today"));
            _tabs.Add(new ClickableComponent(new Rectangle(tabX, tabY + tabHeight + 20, tabWidth, tabHeight), "Yesterday"));
            _tabs.Add(new ClickableComponent(new Rectangle(tabX, tabY + (tabHeight + 20) * 2, tabWidth, tabHeight), "Tracking"));
            _tabs.Add(new ClickableComponent(new Rectangle(tabX, tabY + (tabHeight + 20) * 3, tabWidth, tabHeight), "Customers"));
        }

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            base.receiveLeftClick(x, y, playSound);

            if (this.upperRightCloseButton != null && this.upperRightCloseButton.containsPoint(x, y))
            {
                this.exitThisMenu(playSound);
                return;
            }

            for (int i = 0; i < _tabs.Count; i++)
            {
                if (_tabs[i].containsPoint(x, y))
                {
                    _currentTab = (Tab)i;
                    Game1.playSound("smallSelect");
                    return;
                }
            }

            switch (_currentTab)
            {
                case Tab.Today: _todayView.ReceiveLeftClick(x, y); break;
                case Tab.Yesterday: _yesterdayView.ReceiveLeftClick(x, y); break;
                case Tab.Items: _itemsView.ReceiveLeftClick(x, y); break;
                case Tab.Customers: _customersView.ReceiveLeftClick(x, y); break;
            }
        }

        public override void performHoverAction(int x, int y)
        {
            base.performHoverAction(x, y);
            this.upperRightCloseButton?.tryHover(x, y);

            switch (_currentTab)
            {
                case Tab.Today: _todayView.PerformHoverAction(x, y); break;
                case Tab.Yesterday: _yesterdayView.PerformHoverAction(x, y); break;
                case Tab.Items: _itemsView.PerformHoverAction(x, y); break;
                case Tab.Customers: _customersView.PerformHoverAction(x, y); break;
            }
        }

        public override void receiveScrollWheelAction(int direction)
        {
            base.receiveScrollWheelAction(direction);
            switch (_currentTab)
            {
                case Tab.Today: _todayView.ReceiveScrollWheelAction(direction); break;
                case Tab.Yesterday: _yesterdayView.ReceiveScrollWheelAction(direction); break;
                case Tab.Items: _itemsView.ReceiveScrollWheelAction(direction); break;
                case Tab.Customers: _customersView.ReceiveScrollWheelAction(direction); break;
            }
        }

        public override void draw(SpriteBatch b)
        {
            b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.4f);

            // Draw main background
            Game1.drawDialogueBox(this.xPositionOnScreen, this.yPositionOnScreen, this.width, this.height, false, true);

            // Draw Tabs
            for (int i = 0; i < _tabs.Count; i++)
            {
                var tab = _tabs[i];
                bool isSelected = (int)_currentTab == i;

                // Draw tab background
                IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(384, 396, 15, 15), tab.bounds.X, tab.bounds.Y, tab.bounds.Width, tab.bounds.Height, isSelected ? Color.Wheat : Color.White, 4f, false);

                // Draw text
                Utility.drawTextWithShadow(b, tab.name, Game1.smallFont, new Vector2(tab.bounds.X + 20, tab.bounds.Y + 16), Game1.textColor);
            }

            // Draw current view
            switch (_currentTab)
            {
                case Tab.Today: _todayView.Draw(b); break;
                case Tab.Yesterday: _yesterdayView.Draw(b); break;
                case Tab.Items: _itemsView.Draw(b); break;
                case Tab.Customers: _customersView.Draw(b); break;
            }

            this.upperRightCloseButton?.draw(b);
            this.drawMouse(b);
        }

        public override void leftClickHeld(int x, int y)
        {
            base.leftClickHeld(x, y);
            switch (_currentTab)
            {
                case Tab.Today: _todayView.LeftClickHeld(x, y); break;
                case Tab.Yesterday: _yesterdayView.LeftClickHeld(x, y); break;
                case Tab.Items: _itemsView.LeftClickHeld(x, y); break;
                case Tab.Customers: _customersView.LeftClickHeld(x, y); break;
            }
        }

        public override void releaseLeftClick(int x, int y)
        {
            base.releaseLeftClick(x, y);
            _todayView.ReleaseLeftClick(x, y);
            _yesterdayView.ReleaseLeftClick(x, y);
            _itemsView.ReleaseLeftClick(x, y);
            _customersView.ReleaseLeftClick(x, y);
        }
    }
}
