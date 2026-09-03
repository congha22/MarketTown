using Microsoft.Xna.Framework.Graphics;

namespace MarketTown.Framework.UI.Panels
{
    /// <summary>
    /// Defines the contract for a pluggable UI panel rendered inside the Store Manager menu.
    /// Each shop theme returns its own implementation of this interface from
    /// <see cref="Behaviors.ShopCategories.IShopCategoryBehavior.GetMenuPanel"/>.
    /// </summary>
    public interface IShopMenuPanel
    {
        /// <summary>
        /// Draw this panel's content within the given bounds.
        /// </summary>
        /// <param name="b">The sprite batch to draw to.</param>
        /// <param name="x">Left edge of the panel's allowed drawing area (screen coords).</param>
        /// <param name="y">Top edge of the panel's allowed drawing area (screen coords).</param>
        /// <param name="width">Total width of the allowed drawing area.</param>
        /// <param name="height">Total height of the allowed drawing area.</param>
        void Draw(SpriteBatch b, int x, int y, int width, int height);

        /// <summary>Handle a left-click inside the panel area.</summary>
        void ReceiveLeftClick(int x, int y);

        /// <summary>Handle mouse hover so the panel can show tooltips etc.</summary>
        void PerformHoverAction(int x, int y);
    }
}
