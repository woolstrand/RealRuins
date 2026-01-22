using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Verse;
using RimWorld.Planet;
using RimWorld;
using UnityEngine;

namespace RealRuins
{
    /// <summary>
    /// Utility class for debug operations in RealRuins mod.
    /// </summary>
    public static class RealRuinsDebugUtils
    {
        /// <summary>
        /// Focuses on (selects) a tile in the world view by its tile ID.
        /// </summary>
        /// <param name="tileId">The ID of the tile to focus on.</param>
        public static void FocusTile(int tileId)
        {
            if (Find.WorldSelector == null)
            {
                Debug.Log("WorldSelector not available");
                return;
            }

            // Validate tile ID
            if (tileId < 0 || tileId >= Find.WorldGrid.TilesCount)
            {
                Messages.Message($"RealRuins: Invalid tile ID {tileId}. Valid range: 0-{Find.WorldGrid.TilesCount - 1}", MessageTypeDefOf.RejectInput);
                return;
            }

            // Select the tile
            Find.WorldSelector.SelectFirstOrNextAt(new PlanetTile(tileId));
            Messages.Message($"RealRuins: Focused on tile {tileId}", MessageTypeDefOf.NeutralEvent);
        }
    }
}
