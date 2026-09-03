using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using MarketTown.Framework.Services;

namespace MarketTown.Framework.UI
{
    public class EmployeeSelectionMenu : IClickableMenu
    {
        private readonly StoreEmployeeService _employeeService;
        private readonly GameLocation _location;
        private readonly Vector2 _checkoutTile;
        private readonly List<NPC> _availableNpcs;
        private readonly Action<string> _onHired;
        private readonly List<string> _pendingHires;

        private readonly List<ClickableComponent> _npcButtons = new List<ClickableComponent>();
        private readonly int _buttonsPerPage = 8;
        private int _currentPage = 0;

        public EmployeeSelectionMenu(StoreEmployeeService employeeService, GameLocation location, Vector2 checkoutTile, Action<string> onHired, List<string> pendingHires = null)
        {
            _employeeService = employeeService;
            _location = location;
            _checkoutTile = checkoutTile;
            _onHired = onHired;
            _pendingHires = pendingHires ?? new List<string>();

            this.width = 600;
            this.height = 600;

            this.xPositionOnScreen = Game1.uiViewport.Width / 2 - this.width / 2;
            this.yPositionOnScreen = Game1.uiViewport.Height / 2 - this.height / 2;

            this.upperRightCloseButton = new ClickableTextureComponent(
                new Rectangle(this.xPositionOnScreen + this.width - 36, this.yPositionOnScreen - 8, 48, 48),
                Game1.mouseCursors,
                new Rectangle(337, 494, 12, 12), 4f);

            // Fetch available custom NPCs (requires CAS mod integration, we try to call it via reflection or assume MarketTown has reference)
            // For now, we will find them by name prefix or interface if available.
            _availableNpcs = new List<NPC>();
            foreach (var npc in Utility.getAllCharacters())
            {
                if (npc.Name.StartsWith("d5a1lamdtd.cas.npc", StringComparison.OrdinalIgnoreCase) && !_employeeService.IsEmployee(npc) && !_pendingHires.Contains(npc.Name))
                {
                    _availableNpcs.Add(npc);
                }
            }

            UpdateButtons();
        }

        private void UpdateButtons()
        {
            _npcButtons.Clear();
            int startIdx = _currentPage * _buttonsPerPage;
            int endIdx = Math.Min(startIdx + _buttonsPerPage, _availableNpcs.Count);

            int startY = this.yPositionOnScreen + 100;
            for (int i = startIdx; i < endIdx; i++)
            {
                NPC npc = _availableNpcs[i];
                var btn = new ClickableComponent(new Rectangle(this.xPositionOnScreen + 50, startY, this.width - 100, 50), npc.Name)
                {
                    myID = i,
                    label = npc.displayName
                };
                _npcButtons.Add(btn);
                startY += 55;
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

            foreach (var btn in _npcButtons)
            {
                if (btn.containsPoint(x, y))
                {
                    Game1.playSound("coin");
                    _onHired?.Invoke(btn.name);
                    // this.exitThisMenu(true) will be handled by the parent replacing the menu, 
                    // but we can call it just in case parent doesn't override activeMenu.
                    this.exitThisMenu(true);
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

            Game1.drawDialogueBox(this.xPositionOnScreen, this.yPositionOnScreen, this.width, this.height, false, true);

            string title = "Select Employee to Hire";
            Vector2 titleSize = Game1.dialogueFont.MeasureString(title);
            Utility.drawTextWithShadow(b, title, Game1.dialogueFont, new Vector2(this.xPositionOnScreen + this.width / 2 - titleSize.X / 2, this.yPositionOnScreen + 90), Game1.textColor);

            if (_availableNpcs.Count == 0)
            {
                Utility.drawTextWithShadow(b, "No CAS NPCs available.", Game1.smallFont, new Vector2(this.xPositionOnScreen + 100, this.yPositionOnScreen + 150), Game1.textColor);
            }
            else
            {
                foreach (var btn in _npcButtons)
                {
                    bool isHovered = btn.containsPoint(Game1.getMouseX(), Game1.getMouseY());
                    Color bgColor = isHovered ? Color.Wheat : Color.White;
                    
                    IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(384, 396, 15, 15), btn.bounds.X, btn.bounds.Y, btn.bounds.Width, btn.bounds.Height, bgColor, 4f, false);
                    Utility.drawTextWithShadow(b, btn.label, Game1.smallFont, new Vector2(btn.bounds.X + 20, btn.bounds.Y + 10), Game1.textColor);
                }
            }

            this.upperRightCloseButton?.draw(b);
            this.drawMouse(b);
        }
    }
}
