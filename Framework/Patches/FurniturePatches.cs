using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Objects;

namespace MarketTown.Framework.Patches
{
    internal static class FurniturePatches
    {
        public static void IntersectsForCollision_Postfix(Furniture __instance, Rectangle rect, ref bool __result)
        {
            if (!__result) return; // If it doesn't intersect the main box, skip

            if (__instance.ItemId == "d5a1lamdtd.MarketTown_CheckoutSmall")
            {
                // 3x2 bounding box.
                // Walkable tiles:
                // - X=0, Y=1 (relative to top-left of box)
                // - X=2, Y=0 and Y=1 (entire right column)
                
                // Solid tiles:
                // - X=0, Y=0 (top-left)
                // - X=1, Y=0 and Y=1 (middle column)
                Rectangle solidTopLeft = new Rectangle(__instance.boundingBox.X, __instance.boundingBox.Y, 64, 64);
                Rectangle solidMiddleCol = new Rectangle(__instance.boundingBox.X + 64, __instance.boundingBox.Y, 64, 128);
                
                // If it only intersects the walkable tiles, it's not colliding.
                if (!rect.Intersects(solidTopLeft) && !rect.Intersects(solidMiddleCol))
                {
                    __result = false;
                }
            }
            else if (__instance.ItemId == "d5a1lamdtd.MarketTown_CheckoutLarge")
            {
                // 4x3 bounding box.
                // Walkable tiles:
                // - X=1, Y=1 and Y=2
                // - X=3, Y=0, Y=1, Y=2 (entire right column)
                
                // Solid tiles:
                // - X=0, Y=0, Y=1, Y=2 (left column)
                // - X=2, Y=0, Y=1, Y=2 (column 2)
                // - X=1, Y=0 (top of the column 1)
                Rectangle solidLeftCol = new Rectangle(__instance.boundingBox.X, __instance.boundingBox.Y, 64, 192);
                Rectangle solidCol2 = new Rectangle(__instance.boundingBox.X + 128, __instance.boundingBox.Y, 64, 192);
                Rectangle solidRegister = new Rectangle(__instance.boundingBox.X + 64, __instance.boundingBox.Y, 64, 64);
                
                if (!rect.Intersects(solidLeftCol) && !rect.Intersects(solidCol2) && !rect.Intersects(solidRegister))
                {
                    __result = false;
                }
            }
            else if (__instance.ItemId == "d5a1lamdtd.MarketTown_FittingBooth")
            {
                // 3x2 bounding box.
                // Walkable: only bottom-centre tile (X=1, Y=1).
                // Solid: all other 5 tiles.
                //
                //  [X=0,Y=0] [X=1,Y=0] [X=2,Y=0]   <- all solid (top row)
                //  [X=0,Y=1] [X=1,Y=1] [X=2,Y=1]   <- X=1,Y=1 walkable; others solid

                Rectangle solidTopRow   = new Rectangle(__instance.boundingBox.X,       __instance.boundingBox.Y,      192, 64); // entire top row
                Rectangle solidBotLeft  = new Rectangle(__instance.boundingBox.X,       __instance.boundingBox.Y + 64,  64, 64); // bottom-left
                Rectangle solidBotRight = new Rectangle(__instance.boundingBox.X + 128, __instance.boundingBox.Y + 64,  64, 64); // bottom-right

                if (!rect.Intersects(solidTopRow) && !rect.Intersects(solidBotLeft) && !rect.Intersects(solidBotRight))
                {
                    __result = false;
                }
            }
        }

        public static bool Draw_Prefix(Furniture __instance, SpriteBatch spriteBatch, int x, int y, float alpha)
        {
            if (__instance.ItemId == "d5a1lamdtd.MarketTown_CheckoutSmall")
            {
                Texture2D texture = ItemRegistry.GetDataOrErrorItem(__instance.QualifiedItemId).GetTexture();
                if (texture == null) return true;

                Vector2 position;
                if (Furniture.isDrawingLocationFurniture)
                {
                    position = new Vector2(__instance.boundingBox.X, __instance.boundingBox.Bottom);
                }
                else
                {
                    position = new Vector2(x, y) * 64f;
                    position.Y += __instance.boundingBox.Height;
                }
                position = Game1.GlobalToLocal(Game1.viewport, position);

                // For 3x3 tiles, bounding box is 3x2. 
                // Source height is 48 pixels (3 tiles). Bounding box height is 128 (2 tiles).
                // We subtract the drawn height (sourceRect.Height * 4 scale) to find the top-left drawing position.
                Vector2 drawPos = new Vector2(
                    position.X, 
                    position.Y - (__instance.sourceRect.Height * 4f)
                );

                // Small checkout is 3x3 tiles (48x48 pixels source, 192x192 drawn).
                Rectangle col0Source = new Rectangle(__instance.sourceRect.X, __instance.sourceRect.Y, 16, 48);
                Rectangle col1Source = new Rectangle(__instance.sourceRect.X + 16, __instance.sourceRect.Y, 16, 48);
                Rectangle col2Source = new Rectangle(__instance.sourceRect.X + 32, __instance.sourceRect.Y, 16, 48);

                Vector2 col0Pos = drawPos;
                Vector2 col1Pos = new Vector2(drawPos.X + 64f, drawPos.Y);
                Vector2 col2Pos = new Vector2(drawPos.X + 128f, drawPos.Y);

                SpriteEffects effect = __instance.Flipped ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
                
                int baseBottomY = __instance.boundingBox.Bottom;
                
                // Col 0: Register. Draw under NPC.
                float col0Depth = (baseBottomY - 64) / 10000f;
                // Col 1: Solid belt.
                float col1Depth = baseBottomY / 10000f;
                // Col 2: New NPC spot. Passable flat floor, draw under everything.
                float col2Depth = 0.0001f;

                spriteBatch.Draw(texture, col0Pos, col0Source, Color.White * alpha, 0f, Vector2.Zero, 4f, effect, col0Depth);
                spriteBatch.Draw(texture, col1Pos, col1Source, Color.White * alpha, 0f, Vector2.Zero, 4f, effect, col1Depth);
                spriteBatch.Draw(texture, col2Pos, col2Source, Color.White * alpha, 0f, Vector2.Zero, 4f, effect, col2Depth);

                return false; // Skip original draw
            }
            else if (__instance.ItemId == "d5a1lamdtd.MarketTown_CheckoutLarge")
            {
                Texture2D texture = ItemRegistry.GetDataOrErrorItem(__instance.QualifiedItemId).GetTexture();
                if (texture == null) return true;

                Vector2 position;
                if (Furniture.isDrawingLocationFurniture)
                {
                    position = new Vector2(__instance.boundingBox.X, __instance.boundingBox.Bottom);
                }
                else
                {
                    position = new Vector2(x, y) * 64f;
                    position.Y += __instance.boundingBox.Height;
                }
                position = Game1.GlobalToLocal(Game1.viewport, position);

                // For 4x4 tiles, bounding box is 4x3. 
                // Source height is 64 pixels (4 tiles). Bounding box height is 192 (3 tiles).
                // We subtract the drawn height (sourceRect.Height * 4 scale) to find the top-left drawing position.
                Vector2 drawPos = new Vector2(
                    position.X, 
                    position.Y - (__instance.sourceRect.Height * 4f)
                );

                // Large checkout is 4x4 tiles (64x64 pixels source, 256x256 drawn).
                Rectangle col0Source = new Rectangle(__instance.sourceRect.X, __instance.sourceRect.Y, 16, 64);
                Rectangle col1Source = new Rectangle(__instance.sourceRect.X + 16, __instance.sourceRect.Y, 16, 64);
                Rectangle col2Source = new Rectangle(__instance.sourceRect.X + 32, __instance.sourceRect.Y, 16, 64);
                Rectangle col3Source = new Rectangle(__instance.sourceRect.X + 48, __instance.sourceRect.Y, 16, 64);

                Vector2 col0Pos = drawPos;
                Vector2 col1Pos = new Vector2(drawPos.X + 64f, drawPos.Y);
                Vector2 col2Pos = new Vector2(drawPos.X + 128f, drawPos.Y);
                Vector2 col3Pos = new Vector2(drawPos.X + 192f, drawPos.Y);

                SpriteEffects effect = __instance.Flipped ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
                
                int baseBottomY = __instance.boundingBox.Bottom;
                
                // Col 0: Solid.
                float col0Depth = baseBottomY / 10000f;
                // Col 1: Register (X=1).
                float col1Depth = (baseBottomY - 128) / 10000f;
                // Col 2: Solid belt.
                float col2Depth = baseBottomY / 10000f;
                // Col 3: New NPC spot. Passable flat floor, draw under everything.
                float col3Depth = 0.0001f;

                spriteBatch.Draw(texture, col0Pos, col0Source, Color.White * alpha, 0f, Vector2.Zero, 4f, effect, col0Depth);
                spriteBatch.Draw(texture, col1Pos, col1Source, Color.White * alpha, 0f, Vector2.Zero, 4f, effect, col1Depth);
                spriteBatch.Draw(texture, col2Pos, col2Source, Color.White * alpha, 0f, Vector2.Zero, 4f, effect, col2Depth);
                spriteBatch.Draw(texture, col3Pos, col3Source, Color.White * alpha, 0f, Vector2.Zero, 4f, effect, col3Depth);

                return false;
            }
            else if (__instance.ItemId == "d5a1lamdtd.MarketTown_FittingBooth")
            {
                Texture2D texture = ItemRegistry.GetDataOrErrorItem(__instance.QualifiedItemId).GetTexture();
                if (texture == null) return true;

                // Fitting Booth: 3x3 source sprite (48x48 px), 3x2 bounding box.
                // The sprite's top tile-row hangs 1 tile above the bounding box (curtain rod).
                // The whole booth draws at high depth so it renders ABOVE any NPC
                // standing on the walkable bottom-centre slot (X=1, Y=1).

                Vector2 position;
                if (Furniture.isDrawingLocationFurniture)
                {
                    position = new Vector2(__instance.boundingBox.X, __instance.boundingBox.Bottom);
                }
                else
                {
                    position = new Vector2(x, y) * 64f;
                    position.Y += __instance.boundingBox.Height;
                }
                position = Game1.GlobalToLocal(Game1.viewport, position);

                // Top of the sprite = bottom of bounding box minus full sprite height (3 tiles * 4 scale * 16px = 192px)
                Vector2 drawPos = new Vector2(
                    position.X,
                    position.Y - (__instance.sourceRect.Height * 4f)
                );

                SpriteEffects effect = __instance.Flipped ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

                // Draw at depth above the NPC standing inside.
                // NPC depth ≈ (bb.Y + 112) / 10000f; bb.Bottom / 10000f is always greater.
                float aboveNpcDepth = __instance.boundingBox.Bottom / 10000f;

                spriteBatch.Draw(
                    texture, drawPos, __instance.sourceRect.Value,
                    Color.White * alpha, 0f, Vector2.Zero, 4f, effect, aboveNpcDepth);

                return false; // Skip default draw
            }

            return true;
        }
    }
}
