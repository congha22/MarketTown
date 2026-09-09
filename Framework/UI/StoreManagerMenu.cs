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
using MarketTown.Framework.UI.Panels;

namespace MarketTown.Framework.UI
{
    /// <summary>
    /// The main Store Manager menu opened by interacting with the Store Register.
    ///
    /// Layout (800 x 600):
    ///   Left column  (x+60,  y+150) — Theme dropdown + custom IShopMenuPanel content
    ///   Right column (x+450, y+150) — Employees, hours (always visible)
    /// </summary>
    public class StoreManagerMenu : IClickableMenu
    {
        // ── Services ──────────────────────────────────────────────────────────
        private readonly StoreStatsService _storeStatsService;
        private readonly IndoorVisitorService _visitorService;
        private readonly StoreEmployeeService _employeeService;
        private readonly ShopBehaviorService _shopBehaviorService;
        private readonly GameLocation _location;
        private readonly IModHelper _helper;

        // ── Data ──────────────────────────────────────────────────────────────
        private StoreStatRecord _storeStats;

        // ── Right-column: checkouts / employees ───────────────────────────────
        private readonly List<Furniture> _checkouts;
        private readonly Dictionary<ClickableComponent, Vector2> _hireButtons = new();

        // ── Right-column: hours ───────────────────────────────────────────────
        private ClickableTextureComponent _openLeftArrow;
        private ClickableTextureComponent _openRightArrow;
        private ClickableTextureComponent _closeLeftArrow;
        private ClickableTextureComponent _closeRightArrow;

        // ── Theme dropdown (left column) ──────────────────────────────────────
        private readonly List<string> _themeKeys;      // e.g. ["General", "Fashion"]
        private readonly List<string> _themeNames;     // e.g. ["General Store", "Fashion Boutique"]
        private int _selectedThemeIndex;

        private ClickableTextureComponent _themeLeftArrow;
        private ClickableTextureComponent _themeRightArrow;

        // ── Active panel ──────────────────────────────────────────────────────
        private IShopMenuPanel _currentPanel;
        private Texture2D _currentThemeImage;
        private Texture2D _managerBg;
        private Texture2D _photoFrameBg;

        // Panel drawing area (left column, below dropdown)
        private int _panelX;
        private int _panelY;
        private int _panelWidth;
        private int _panelHeight;

        // ── Pending Changes ───────────────────────────────────────────────────
        private int _tempOpenHour;
        private int _tempCloseHour;
        private string _tempThemeKey;
        private Dictionary<Vector2, string> _tempHiredNpcs = new();
        private string _hoverText = "";

        public ClickableTextureComponent okButton;
        public ClickableTextureComponent cancelButton;

        // ── Constructor ───────────────────────────────────────────────────────

        public StoreManagerMenu(
            StoreStatsService storeStatsService,
            IndoorVisitorService visitorService,
            StoreEmployeeService employeeService,
            ShopBehaviorService shopBehaviorService,
            GameLocation location,
            IModHelper helper)
        {
            _storeStatsService = storeStatsService;
            _visitorService = visitorService;
            _employeeService = employeeService;
            _shopBehaviorService = shopBehaviorService;
            _location = location;
            _helper = helper;

            this.width = 1250;
            this.height = 800;
            this.xPositionOnScreen = Game1.uiViewport.Width / 2 - this.width / 2;
            this.yPositionOnScreen = Game1.uiViewport.Height / 2 - this.height / 2;

            this.okButton = new ClickableTextureComponent(
                new Rectangle(this.xPositionOnScreen + this.width - 160, this.yPositionOnScreen + this.height - 100, 64, 64),
                Game1.mouseCursors,
                Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, 46), 1f);

            this.cancelButton = new ClickableTextureComponent(
                new Rectangle(this.xPositionOnScreen + this.width - 90, this.yPositionOnScreen + this.height - 100, 64, 64),
                Game1.mouseCursors,
                Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, 47), 1f);

            // ── Fetch saved stats ──────────────────────────────────────────
            _storeStats = _storeStatsService.StoreStats.TryGetValue(_location.NameOrUniqueName, out var stats)
                ? stats
                : new StoreStatRecord();

            _tempOpenHour = _storeStats.OpenHour;
            _tempCloseHour = _storeStats.CloseHour;
            _tempThemeKey = _storeStats.ShopTheme ?? "General";

            try
            {
                _managerBg = _helper.ModContent.Load<Texture2D>("assets/manager_bg.png");
                _photoFrameBg = _helper.ModContent.Load<Texture2D>("assets/photo_frame.png");
            }
            catch { }

            // ── Build theme list from registry ────────────────────────────
            _themeKeys = _shopBehaviorService.AllBehaviors.Keys.OrderBy(k => k).ToList();
            _themeNames = _themeKeys.Select(k => _shopBehaviorService.AllBehaviors[k].DisplayName).ToList();

            _selectedThemeIndex = Math.Max(0, _themeKeys.IndexOf(_tempThemeKey));

            // ── Build checkout hire buttons (right column) ────────────────
            _checkouts = _location.furniture
                .Where(f => f.ItemId == "d5a1lamdtd.MarketTown_CheckoutSmall" || f.ItemId == "d5a1lamdtd.MarketTown_CheckoutLarge")
                .ToList();

            // ── Hours arrows (right column) ───────────────────────────────
            int settingsX = this.xPositionOnScreen + 650;
            int settingsY = this.yPositionOnScreen + 315;
            int hourOffset = 20;

            _openLeftArrow = new ClickableTextureComponent(new Rectangle(settingsX + hourOffset + 70, settingsY + 40, 30, 28), Game1.mouseCursors, new Rectangle(352, 495, 12, 11), 2.5f);
            _openRightArrow = new ClickableTextureComponent(new Rectangle(settingsX + hourOffset + 220, settingsY + 40, 30, 28), Game1.mouseCursors, new Rectangle(365, 495, 12, 11), 2.5f);
            _closeLeftArrow = new ClickableTextureComponent(new Rectangle(settingsX + hourOffset + 70, settingsY + 75, 30, 28), Game1.mouseCursors, new Rectangle(352, 495, 12, 11), 2.5f);
            _closeRightArrow = new ClickableTextureComponent(new Rectangle(settingsX + hourOffset + 220, settingsY + 75, 30, 28), Game1.mouseCursors, new Rectangle(365, 495, 12, 11), 2.5f);

            // ── Build checkout hire buttons (right column) ────────────────
            int rightYTracker = settingsY + 120;
            int checkoutOffset = 20;
            if (_checkouts.Count > 0)
            {
                rightYTracker += 35; // "Employees:" label
                foreach (var checkout in _checkouts)
                {
                    var btn = new ClickableComponent(new Rectangle(this.xPositionOnScreen + 870 + checkoutOffset, rightYTracker, 150, 40), "HireBtn");
                    _hireButtons[btn] = checkout.TileLocation;
                    _tempHiredNpcs[checkout.TileLocation] = _employeeService.GetHiredEmployee(_location, checkout.TileLocation);
                    rightYTracker += 45;
                }
            }

            // ── Theme arrows (left column) ────────────────────────────────
            int dropdownX = this.xPositionOnScreen + 150;
            int dropdownY = this.yPositionOnScreen + 190;

            _themeLeftArrow = new ClickableTextureComponent(new Rectangle(dropdownX, dropdownY, 44, 48), Game1.mouseCursors, new Rectangle(352, 495, 12, 11), 4f);
            _themeRightArrow = new ClickableTextureComponent(new Rectangle(dropdownX + 350, dropdownY, 44, 48), Game1.mouseCursors, new Rectangle(365, 495, 12, 11), 4f);

            // Panel area starts just below the dropdown row
            _panelX = dropdownX;
            _panelY = dropdownY + 45;
            _panelWidth = 450;
            _panelHeight = this.height - (_panelY - this.yPositionOnScreen) - 60;

            RebuildPanel();
        }

        // ── Panel management ──────────────────────────────────────────────────

        private void RebuildPanel()
        {
            string key = _themeKeys.Count > 0 ? _themeKeys[_selectedThemeIndex] : "General";
            var behavior = _shopBehaviorService.AllBehaviors.TryGetValue(key, out var b) ? b : null;
            _currentPanel = behavior?.GetMenuPanel(_location, _storeStatsService, _visitorService);

            string imageKey = "example_general";
            string themeName = _themeNames.Count > 0 ? _themeNames[_selectedThemeIndex] : "";
            if (themeName.Contains("Discovery")) imageKey = "example_discovery";
            else if (themeName.Contains("Fashion")) imageKey = "example_fashion";

            try
            {
                _currentThemeImage = _helper.ModContent.Load<Texture2D>($"assets/{imageKey}.png");
            }
            catch
            {
                _currentThemeImage = null;
            }
        }

        // ── Input ─────────────────────────────────────────────────────────────

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            base.receiveLeftClick(x, y, playSound);

            bool isStoreOpen = Game1.timeOfDay >= _storeStats.OpenHour && Game1.timeOfDay < _storeStats.CloseHour;

            if (this.okButton != null && this.okButton.containsPoint(x, y))
            {
                Game1.playSound("bigDeSelect");

                string originalTheme = _storeStats.ShopTheme ?? "General";
                if (_tempThemeKey != originalTheme)
                {
                    _storeStatsService.ResetStoreProgress(_location);
                }

                _shopBehaviorService.SetTheme(_location, _tempThemeKey);
                _storeStatsService.UpdateStoreHours(_location, _tempOpenHour, _tempCloseHour);

                // Apply pending hires / fires
                foreach (var kvp in _tempHiredNpcs)
                {
                    string currentHired = _employeeService.GetHiredEmployee(_location, kvp.Key);
                    string pendingHired = kvp.Value;

                    if (currentHired != pendingHired)
                    {
                        if (!string.IsNullOrEmpty(currentHired))
                        {
                            _employeeService.FireEmployee(_location, kvp.Key);
                        }
                        if (!string.IsNullOrEmpty(pendingHired))
                        {
                            _employeeService.HireEmployee(_location, kvp.Key, pendingHired);
                        }
                    }
                }

                this.exitThisMenu(playSound);
                return;
            }

            if (this.cancelButton != null && this.cancelButton.containsPoint(x, y))
            {
                Game1.playSound("bigDeSelect");
                this.exitThisMenu(playSound);
                return;
            }

            // ── Theme arrows ──────────────────────────────────────────────
            bool clickedThemeArrow = _themeLeftArrow.containsPoint(x, y) || _themeRightArrow.containsPoint(x, y);

            if (clickedThemeArrow)
            {
                if (isStoreOpen)
                {
                    Game1.addHUDMessage(new HUDMessage("Cannot change theme while the store is open.", 3));
                    return;
                }

                if (_themeLeftArrow.containsPoint(x, y))
                {
                    Game1.playSound("drumkit6");
                    _selectedThemeIndex = (_selectedThemeIndex - 1 + _themeKeys.Count) % _themeKeys.Count;
                    ApplySelectedTheme();
                    return;
                }
                if (_themeRightArrow.containsPoint(x, y))
                {
                    Game1.playSound("drumkit6");
                    _selectedThemeIndex = (_selectedThemeIndex + 1) % _themeKeys.Count;
                    ApplySelectedTheme();
                    return;
                }
            }

            // ── Hire buttons ──────────────────────────────────────────────
            foreach (var kvp in _hireButtons)
            {
                if (kvp.Key.containsPoint(x, y))
                {
                    Game1.playSound("drumkit6");

                    string hiredNpc = _tempHiredNpcs[kvp.Value];
                    if (!string.IsNullOrEmpty(hiredNpc))
                    {
                        if (isStoreOpen)
                        {
                            Game1.addHUDMessage(new HUDMessage("Cannot fire employee while the store is open.", 3));
                            return;
                        }

                        // Mark as fired in temp state
                        _tempHiredNpcs[kvp.Value] = null;
                        return;
                    }

                    var parentMenu = this;
                    var pendingList = _tempHiredNpcs.Values.Where(v => !string.IsNullOrEmpty(v)).ToList();

                    var pendingFires = new List<string>();
                    foreach (var checkout in _checkouts)
                    {
                        string currentlyHired = _employeeService.GetHiredEmployee(_location, checkout.TileLocation);
                        if (!string.IsNullOrEmpty(currentlyHired) && !_tempHiredNpcs.Values.Contains(currentlyHired))
                        {
                            pendingFires.Add(currentlyHired);
                        }
                    }

                    Game1.activeClickableMenu = new EmployeeSelectionMenu(_employeeService, _location, kvp.Value, selectedNpc =>
                    {
                        parentMenu._tempHiredNpcs[kvp.Value] = selectedNpc;
                        Game1.activeClickableMenu = parentMenu;
                    }, pendingList, pendingFires);
                    return;
                }
            }

            // ── Hours arrows ──────────────────────────────────────────────
            bool clickedHourArrow = _openLeftArrow.containsPoint(x, y) || _openRightArrow.containsPoint(x, y) || _closeLeftArrow.containsPoint(x, y) || _closeRightArrow.containsPoint(x, y);

            if (clickedHourArrow)
            {
                if (isStoreOpen)
                {
                    Game1.addHUDMessage(new HUDMessage("Cannot change hours while the store is open.", 3));
                    return;
                }

                if (_openLeftArrow.containsPoint(x, y))
                {
                    Game1.playSound("drumkit6");
                    _tempOpenHour = Math.Max(600, _tempOpenHour - 100);
                    _tempOpenHour = Math.Min(_tempOpenHour, _tempCloseHour - 100);
                }
                else if (_openRightArrow.containsPoint(x, y))
                {
                    Game1.playSound("drumkit6");
                    _tempOpenHour = Math.Min(2400, _tempOpenHour + 100);
                    _tempOpenHour = Math.Min(_tempOpenHour, _tempCloseHour - 100);
                }
                else if (_closeLeftArrow.containsPoint(x, y))
                {
                    Game1.playSound("drumkit6");
                    _tempCloseHour = Math.Max(600, _tempCloseHour - 100);
                    _tempCloseHour = Math.Max(_tempCloseHour, _tempOpenHour + 100);
                }
                else if (_closeRightArrow.containsPoint(x, y))
                {
                    Game1.playSound("drumkit6");
                    _tempCloseHour = Math.Min(2400, _tempCloseHour + 100);
                }
            }

            // ── Delegate to current panel ─────────────────────────────────
            _currentPanel?.ReceiveLeftClick(x, y);
        }

        private void ApplySelectedTheme()
        {
            _tempThemeKey = _themeKeys[_selectedThemeIndex];
            RebuildPanel();
        }

        public override void performHoverAction(int x, int y)
        {
            base.performHoverAction(x, y);
            this.okButton?.tryHover(x, y);
            this.cancelButton?.tryHover(x, y);
            _currentPanel?.PerformHoverAction(x, y);

            _hoverText = "";
            int rightX = this.xPositionOnScreen + 650;
            int rightY = this.yPositionOnScreen + 160;
            Rectangle progressArea = new Rectangle(rightX, rightY, 300, 160);
            if (progressArea.Contains(x, y))
            {
                int shopLevel = _storeStats.GetShopLevel();
                if (shopLevel < 5)
                {
                    _storeStats.GetNextLevelRequirements(out int reqVisitors, out int reqItems, out int reqEarnings);
                    _hoverText = $"Next Level Requirements:\nVisitors: {reqVisitors}\nItems Sold: {reqItems}\nEarnings: {reqEarnings}g";
                }
                else
                {
                    _hoverText = "Shop is at Maximum Level!";
                }
            }
            else if (_currentPanel != null && !string.IsNullOrEmpty(_currentPanel.HoverText))
            {
                _hoverText = _currentPanel.HoverText;
            }
        }

        // ── Draw ──────────────────────────────────────────────────────────────

        public override void draw(SpriteBatch b)
        {
            // Dim background
            b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.4f);

            // Main window
            if (_managerBg != null)
            {
                b.Draw(_managerBg, new Rectangle(this.xPositionOnScreen, this.yPositionOnScreen, this.width, this.height), Color.White);
            }
            else
            {
                Game1.drawDialogueBox(this.xPositionOnScreen, this.yPositionOnScreen, this.width, this.height, false, true);
            }

            // ── Title ─────────────────────────────────────────────────────
            string title = $"Store Manager - {_location.Name}";
            Vector2 titleSize = Game1.dialogueFont.MeasureString(title);
            Utility.drawTextWithShadow(b, title, Game1.dialogueFont,
                new Vector2(this.xPositionOnScreen + this.width / 2 - titleSize.X / 2, this.yPositionOnScreen + 95),
                Game1.textColor);

            // ── LEFT COLUMN: Theme dropdown + panel ───────────────────────
            int dropdownX = _panelX;
            int dropdownY = _panelY - 45;

            b.DrawString(Game1.smallFont, "Shop Theme:", new Vector2(dropdownX, dropdownY - 35), Game1.textColor);

            _themeLeftArrow.draw(b);
            _themeRightArrow.draw(b);

            string displayName = _themeNames.Count > 0 ? _themeNames[_selectedThemeIndex] : "General Store";
            Vector2 nameSize = Game1.smallFont.MeasureString(displayName);
            b.DrawString(Game1.smallFont, displayName, new Vector2(dropdownX + 197 - nameSize.X / 2, dropdownY + 5), Color.DarkSlateBlue);

            // Delegate to theme panel
            int panelBottomY = _panelY;
            if (_currentPanel != null)
            {
                panelBottomY = _currentPanel.Draw(b, _panelX, _panelY, _panelWidth, _panelHeight);
            }

            string originalTheme = _storeStats.ShopTheme ?? "General";
            if (_tempThemeKey != originalTheme)
            {
                string parsedWarning = Game1.parseText("Warning: Changing theme will reset store progress!", Game1.smallFont, _panelWidth);
                b.DrawString(Game1.smallFont, parsedWarning, new Vector2(_panelX, panelBottomY + 10), Color.Red);
            }

            // ── RIGHT COLUMN: Store stats summary ─────────────────────────
            int rightX = this.xPositionOnScreen + 650;
            int rightY = this.yPositionOnScreen + 160;
            Color statsColor = new Color(20, 140, 40);

            // Draw example image
            if (_currentThemeImage != null)
            {
                int targetSize = 290;
                int imgX = this.xPositionOnScreen + this.width - 90;
                int imgY = this.yPositionOnScreen + 180;
                float rotation = (float)(7 * Math.PI / 180);

                if (_photoFrameBg != null)
                {
                    // Frame's inner photo is 64x64, starting at 6,6. Center is at 38,38.
                    float frameScale = targetSize / 64f;
                    Vector2 originBg = new Vector2(38f, 38f);
                    b.Draw(_photoFrameBg, new Vector2(imgX, imgY), null, Color.White, rotation, originBg, frameScale, SpriteEffects.None, 0f);
                }

                Vector2 originImg = new Vector2(_currentThemeImage.Width / 2f, _currentThemeImage.Height / 2f);
                Vector2 imgScale = new Vector2(targetSize / (float)_currentThemeImage.Width, targetSize / (float)_currentThemeImage.Height);
                b.Draw(_currentThemeImage, new Vector2(imgX, imgY), null, Color.White, rotation, originImg, imgScale, SpriteEffects.None, 0f);
            }

            b.DrawString(Game1.smallFont, "Shop Level:", new Vector2(rightX, rightY), Game1.textColor);

            // Draw Star Icon
            int shopLevel = _storeStats.GetShopLevel();
            Rectangle starRect = new Rectangle(338, 400, 8, 8);
            Color starColor = Color.White;
            bool drawStar = true;

            switch (shopLevel)
            {
                case 1: drawStar = false; break; // No star
                case 2: starRect = new Rectangle(338, 400, 8, 8); starColor = Color.Black; break; // Silver tinted Black
                case 3: starRect = new Rectangle(338, 400, 8, 8); break; // Silver
                case 4: starRect = new Rectangle(346, 400, 8, 8); break; // Gold
                case 5: starRect = new Rectangle(346, 392, 8, 8); break; // Iridium
            }

            if (drawStar)
            {
                b.Draw(Game1.mouseCursors, new Vector2(rightX + Game1.smallFont.MeasureString("Shop Level: ").X, rightY - 5), starRect, starColor, 0f, Vector2.Zero, 4f, SpriteEffects.None, 1f);
            }

            rightY += 35;

            _storeStats.GetNextLevelRequirements(out int reqVisitors, out int reqItems, out int reqEarnings);

            string visitorText = $"{_storeStats.TotalVisitors}";
            string itemsText = $"{_storeStats.TotalSoldItems}";
            string earningsText = $"{_storeStats.TotalEarnings}g";

            int offset = 140;
            DrawKeyValue(b, "Visitors:", visitorText, rightX + 20, rightY, offset, statsColor);
            rightY += 35;
            DrawKeyValue(b, "Items Sold:", itemsText, rightX + 20, rightY, offset, statsColor);
            rightY += 35;
            DrawKeyValue(b, "Earnings:", earningsText, rightX + 20, rightY, offset, statsColor);
            rightY += 50;

            // ── RIGHT COLUMN: Hours ───────────────────────────────────────
            int settingsX = this.xPositionOnScreen + 650;
            int settingsY = this.yPositionOnScreen + 315;
            int hourOffset = 20;

            b.DrawString(Game1.smallFont, "Store Hours:", new Vector2(settingsX, settingsY), Game1.textColor);

            b.DrawString(Game1.smallFont, "Open:", new Vector2(settingsX + hourOffset, settingsY + 35), Game1.textColor);
            _openLeftArrow.draw(b);
            string openTimeStr = Game1.getTimeOfDayString(_tempOpenHour);
            Vector2 openStrSize = Game1.smallFont.MeasureString(openTimeStr);
            b.DrawString(Game1.smallFont, openTimeStr, new Vector2(settingsX + hourOffset + 160 - openStrSize.X / 2, settingsY + 35), Game1.textColor);
            _openRightArrow.draw(b);

            b.DrawString(Game1.smallFont, "Close:", new Vector2(settingsX + hourOffset, settingsY + 70), Game1.textColor);
            _closeLeftArrow.draw(b);
            string closeTimeStr = Game1.getTimeOfDayString(_tempCloseHour);
            Vector2 closeStrSize = Game1.smallFont.MeasureString(closeTimeStr);
            b.DrawString(Game1.smallFont, closeTimeStr, new Vector2(settingsX + hourOffset + 160 - closeStrSize.X / 2, settingsY + 70), Game1.textColor);
            _closeRightArrow.draw(b);

            // ── RIGHT COLUMN: Employees ───────────────────────────────────
            int employeesY = settingsY + 120;
            int checkoutOffset = 20;
            if (_checkouts.Count > 0)
            {
                b.DrawString(Game1.smallFont, "Employees:", new Vector2(rightX, employeesY), Game1.textColor);
                employeesY += 35;

                foreach (var checkout in _checkouts)
                {
                    string type = checkout.ItemId == "d5a1lamdtd.MarketTown_CheckoutSmall" ? "Small" : "Large";
                    b.DrawString(Game1.smallFont, $"{type} Checkout:", new Vector2(rightX + checkoutOffset, employeesY), Game1.textColor);

                    var btn = _hireButtons.FirstOrDefault(kvp => kvp.Value == checkout.TileLocation).Key;
                    if (btn != null)
                    {
                        string hiredNpc = _tempHiredNpcs[checkout.TileLocation];
                        string btnText = "Hire";
                        bool isHovered = btn.containsPoint(Game1.getMouseX(), Game1.getMouseY());

                        if (!string.IsNullOrEmpty(hiredNpc))
                        {
                            bool isStoreOpen = Game1.timeOfDay >= _storeStats.OpenHour && Game1.timeOfDay < _storeStats.CloseHour;
                            NPC npc = Game1.getCharacterFromName(hiredNpc);
                            string employeeDisplayName = npc != null ? npc.displayName : hiredNpc;

                            if (isHovered && !isStoreOpen)
                            {
                                btnText = "Fire";
                            }
                            else
                            {
                                btnText = employeeDisplayName;
                            }
                        }

                        Color bgColor = isHovered ? Color.Wheat : Color.White;

                        IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(384, 396, 15, 15),
                            btn.bounds.X, btn.bounds.Y, btn.bounds.Width, btn.bounds.Height, bgColor, 4f, false);
                        b.DrawString(Game1.smallFont, btnText, new Vector2(btn.bounds.X + 25, btn.bounds.Y + 5), Game1.textColor);
                    }

                    employeesY += 45;
                }
            }

            // ── Overlay elements ──────────────────────────────────────────
            this.okButton?.draw(b);
            this.cancelButton?.draw(b);

            if (!string.IsNullOrEmpty(_hoverText))
            {
                IClickableMenu.drawHoverText(b, _hoverText, Game1.smallFont);
            }

            this.drawMouse(b);
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
