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

            this.width = 800;
            this.height = 600;
            this.xPositionOnScreen = Game1.uiViewport.Width / 2 - this.width / 2;
            this.yPositionOnScreen = Game1.uiViewport.Height / 2 - this.height / 2;

            this.upperRightCloseButton = new ClickableTextureComponent(
                new Rectangle(this.xPositionOnScreen + this.width - 36, this.yPositionOnScreen - 8, 48, 48),
                Game1.mouseCursors,
                new Rectangle(337, 494, 12, 12), 4f);

            this.okButton = new ClickableTextureComponent(
                new Rectangle(this.xPositionOnScreen + this.width - 128, this.yPositionOnScreen + this.height - 64, 64, 64),
                Game1.mouseCursors,
                Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, 46), 1f);

            this.cancelButton = new ClickableTextureComponent(
                new Rectangle(this.xPositionOnScreen + this.width - 64, this.yPositionOnScreen + this.height - 64, 64, 64),
                Game1.mouseCursors,
                Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, 47), 1f);

            // ── Fetch saved stats ──────────────────────────────────────────
            _storeStats = _storeStatsService.StoreStats.TryGetValue(_location.NameOrUniqueName, out var stats)
                ? stats
                : new StoreStatRecord();

            _tempOpenHour = _storeStats.OpenHour;
            _tempCloseHour = _storeStats.CloseHour;
            _tempThemeKey = _storeStats.ShopTheme ?? "General";

            // ── Build theme list from registry ────────────────────────────
            _themeKeys = _shopBehaviorService.AllBehaviors.Keys.OrderBy(k => k).ToList();
            _themeNames = _themeKeys.Select(k => _shopBehaviorService.AllBehaviors[k].DisplayName).ToList();

            _selectedThemeIndex = Math.Max(0, _themeKeys.IndexOf(_tempThemeKey));

            // ── Build checkout hire buttons (right column) ────────────────
            _checkouts = _location.furniture
                .Where(f => f.ItemId == "d5a1lamdtd.MarketTown_CheckoutSmall" || f.ItemId == "d5a1lamdtd.MarketTown_CheckoutLarge")
                .ToList();

            int btnY = this.yPositionOnScreen + 160;
            foreach (var checkout in _checkouts)
            {
                var btn = new ClickableComponent(new Rectangle(this.xPositionOnScreen + 500, btnY, 150, 40), "HireBtn");
                _hireButtons[btn] = checkout.TileLocation;
                _tempHiredNpcs[checkout.TileLocation] = _employeeService.GetHiredEmployee(_location, checkout.TileLocation);
                btnY += 60;
            }

            // ── Hours arrows (right column) ───────────────────────────────
            int settingsX = this.xPositionOnScreen + 450;
            int settingsY = this.yPositionOnScreen + 360;

            _openLeftArrow  = new ClickableTextureComponent(new Rectangle(settingsX + 60,  settingsY + 40,  44, 48), Game1.mouseCursors, new Rectangle(352, 495, 12, 11), 4f);
            _openRightArrow = new ClickableTextureComponent(new Rectangle(settingsX + 170, settingsY + 40,  44, 48), Game1.mouseCursors, new Rectangle(365, 495, 12, 11), 4f);
            _closeLeftArrow = new ClickableTextureComponent(new Rectangle(settingsX + 60,  settingsY + 100, 44, 48), Game1.mouseCursors, new Rectangle(352, 495, 12, 11), 4f);
            _closeRightArrow= new ClickableTextureComponent(new Rectangle(settingsX + 170, settingsY + 100, 44, 48), Game1.mouseCursors, new Rectangle(365, 495, 12, 11), 4f);

            // ── Theme arrows (left column) ────────────────────────────────
            int dropdownX = this.xPositionOnScreen + 60;
            int dropdownY = this.yPositionOnScreen + 160;

            _themeLeftArrow  = new ClickableTextureComponent(new Rectangle(dropdownX,       dropdownY, 44, 48), Game1.mouseCursors, new Rectangle(352, 495, 12, 11), 4f);
            _themeRightArrow = new ClickableTextureComponent(new Rectangle(dropdownX + 280, dropdownY, 44, 48), Game1.mouseCursors, new Rectangle(365, 495, 12, 11), 4f);

            // Panel area starts just below the dropdown row
            _panelX = dropdownX;
            _panelY = dropdownY + 60;
            _panelWidth = 360;
            _panelHeight = this.height - (_panelY - this.yPositionOnScreen) - 60;

            RebuildPanel();
        }

        // ── Panel management ──────────────────────────────────────────────────

        private void RebuildPanel()
        {
            string key = _themeKeys.Count > 0 ? _themeKeys[_selectedThemeIndex] : "General";
            var behavior = _shopBehaviorService.AllBehaviors.TryGetValue(key, out var b) ? b : null;
            _currentPanel = behavior?.GetMenuPanel(_location, _storeStatsService, _visitorService);
        }

        // ── Input ─────────────────────────────────────────────────────────────

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            base.receiveLeftClick(x, y, playSound);

            bool isStoreOpen = Game1.timeOfDay >= _storeStats.OpenHour && Game1.timeOfDay < _storeStats.CloseHour;

            if (this.upperRightCloseButton != null && this.upperRightCloseButton.containsPoint(x, y))
            {
                this.exitThisMenu(playSound);
                return;
            }

            if (this.okButton != null && this.okButton.containsPoint(x, y))
            {
                Game1.playSound("bigDeSelect");
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
                    
                    Game1.activeClickableMenu = new EmployeeSelectionMenu(_employeeService, _location, kvp.Value, selectedNpc =>
                    {
                        parentMenu._tempHiredNpcs[kvp.Value] = selectedNpc;
                        Game1.activeClickableMenu = parentMenu;
                    }, pendingList);
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
            this.upperRightCloseButton?.tryHover(x, y);
            this.okButton?.tryHover(x, y);
            this.cancelButton?.tryHover(x, y);
            _currentPanel?.PerformHoverAction(x, y);
        }

        // ── Draw ──────────────────────────────────────────────────────────────

        public override void draw(SpriteBatch b)
        {
            // Dim background
            b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.4f);

            // Main window
            Game1.drawDialogueBox(this.xPositionOnScreen, this.yPositionOnScreen, this.width, this.height, false, true);

            // ── Title ─────────────────────────────────────────────────────
            string title = $"Store Manager - {_location.Name}";
            Vector2 titleSize = Game1.dialogueFont.MeasureString(title);
            Utility.drawTextWithShadow(b, title, Game1.dialogueFont,
                new Vector2(this.xPositionOnScreen + this.width / 2 - titleSize.X / 2, this.yPositionOnScreen + 90),
                Game1.textColor);

            // ── LEFT COLUMN: Theme dropdown + panel ───────────────────────
            int dropdownX = _panelX;
            int dropdownY = _panelY - 60;

            Utility.drawTextWithShadow(b, "Shop Theme:", Game1.smallFont,
                new Vector2(dropdownX, dropdownY - 5), Game1.textColor);

            _themeLeftArrow.draw(b);
            _themeRightArrow.draw(b);

            string displayName = _themeNames.Count > 0 ? _themeNames[_selectedThemeIndex] : "General Store";
            Vector2 nameSize = Game1.smallFont.MeasureString(displayName);
            Utility.drawTextWithShadow(b, displayName, Game1.smallFont,
                new Vector2(dropdownX + 50 + (_panelWidth - 100) / 2 - nameSize.X / 2, dropdownY),
                Color.DarkSlateBlue);

            // Divider line under dropdown
            b.Draw(Game1.fadeToBlackRect,
                new Rectangle(dropdownX, dropdownY + 45, _panelWidth, 2),
                Color.SlateGray * 0.5f);

            // Delegate to theme panel
            _currentPanel?.Draw(b, _panelX, _panelY, _panelWidth, _panelHeight);

            // ── RIGHT COLUMN: Store stats summary ─────────────────────────
            int rightX = this.xPositionOnScreen + 450;
            int rightY = this.yPositionOnScreen + 160;

            DrawKeyValue(b, "Total Visitors:", _storeStats.TotalVisitors.ToString(), rightX, rightY);
            rightY += 35;
            DrawKeyValue(b, "Items Sold:", _storeStats.TotalSoldItems.ToString(), rightX, rightY);
            rightY += 35;
            DrawKeyValue(b, "Total Earnings:", $"{_storeStats.TotalEarnings}g", rightX, rightY);
            rightY += 50;

            // ── RIGHT COLUMN: Employees ───────────────────────────────────
            if (_checkouts.Count > 0)
            {
                Utility.drawTextWithShadow(b, "Employees:", Game1.smallFont, new Vector2(rightX, rightY), Game1.textColor);
                rightY += 35;

                foreach (var checkout in _checkouts)
                {
                    string type = checkout.ItemId == "d5a1lamdtd.MarketTown_CheckoutSmall" ? "Small" : "Large";
                    Utility.drawTextWithShadow(b, $"{type} Checkout:", Game1.smallFont, new Vector2(rightX, rightY), Game1.textColor);

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
                        Utility.drawTextWithShadow(b, btnText, Game1.smallFont,
                            new Vector2(btn.bounds.X + 15, btn.bounds.Y + 5), Game1.textColor);
                    }

                    rightY += 60;
                }
            }

            // ── RIGHT COLUMN: Hours ───────────────────────────────────────
            int settingsX = this.xPositionOnScreen + 450;
            int settingsY = this.yPositionOnScreen + 360;

            Utility.drawTextWithShadow(b, "Store Hours:", Game1.smallFont, new Vector2(settingsX, settingsY), Game1.textColor);

            Utility.drawTextWithShadow(b, "Open:", Game1.smallFont, new Vector2(settingsX, settingsY + 45), Game1.textColor);
            _openLeftArrow.draw(b);
            string openTimeStr = Game1.getTimeOfDayString(_tempOpenHour);
            Vector2 openStrSize = Game1.smallFont.MeasureString(openTimeStr);
            Utility.drawTextWithShadow(b, openTimeStr, Game1.smallFont,
                new Vector2(settingsX + 117 - openStrSize.X / 2, settingsY + 45), Game1.textColor);
            _openRightArrow.draw(b);

            Utility.drawTextWithShadow(b, "Close:", Game1.smallFont, new Vector2(settingsX, settingsY + 105), Game1.textColor);
            _closeLeftArrow.draw(b);
            string closeTimeStr = Game1.getTimeOfDayString(_tempCloseHour);
            Vector2 closeStrSize = Game1.smallFont.MeasureString(closeTimeStr);
            Utility.drawTextWithShadow(b, closeTimeStr, Game1.smallFont,
                new Vector2(settingsX + 117 - closeStrSize.X / 2, settingsY + 105), Game1.textColor);
            _closeRightArrow.draw(b);

            // ── Overlay elements ──────────────────────────────────────────
            this.okButton?.draw(b);
            this.cancelButton?.draw(b);
            this.upperRightCloseButton?.draw(b);
            this.drawMouse(b);
        }

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
