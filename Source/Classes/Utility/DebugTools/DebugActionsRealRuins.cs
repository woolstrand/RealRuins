using System;
using System.Collections.Generic;
using Verse;
using RimWorld;
using UnityEngine;
using LudeonTK;

namespace RealRuins
{
    /// <summary>
    /// Debug actions menu for RealRuins mod.
    /// Provides debug utilities accessible from the world map debug menu.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class DebugActionsRealRuins
    {
        [DebugAction("RealRuins", "Focus on tile", allowedGameStates = AllowedGameStates.PlayingOnWorld)]
        public static void ShowFocusTileDialog()
        {
            if (Find.World == null)
            {
                Messages.Message("RealRuins: Not in world view", MessageTypeDefOf.RejectInput);
                return;
            }

            // Create a dialog that asks for tile ID input
            Find.WindowStack.Add(new Dialog_FocusTile());
        }
    }

    /// <summary>
    /// Dialog for entering a tile ID to focus on.
    /// </summary>
    public class Dialog_FocusTile : Window
    {
        private string tileIdBuffer = "";
        private int tileId = -1;

        public override Vector2 InitialSize => new Vector2(300f, 250f);

        public Dialog_FocusTile()
        {
            this.doCloseButton = true;
            this.doCloseX = true;
            this.absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);

            listing.Label("Enter tile ID:");
            listing.TextFieldNumeric(ref tileId, ref tileIdBuffer, 0f);

            listing.Gap(10f);

            if (listing.ButtonText("Focus Tile"))
            {
                if (tileId >= 0 && tileId < Find.WorldGrid.TilesCount)
                {
                    RealRuinsDebugUtils.FocusTile(tileId);
                    this.Close();
                }
                else
                {
                    Messages.Message($"RealRuins: Invalid tile ID {tileId}. Valid range: 0-{Find.WorldGrid.TilesCount - 1}", MessageTypeDefOf.RejectInput);
                }
            }

            if (listing.ButtonText("Cancel"))
            {
                this.Close();
            }

            listing.End();
        }
    }
}
