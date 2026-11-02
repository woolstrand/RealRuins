using System.Collections.Generic;

using RimWorld;
using Verse;

namespace RealRuins {
    // Deterioration leaves roofs without the walls that held them. The game doesn't check roofs it didn't see change, so such a
    // roof floats until something nearby is built or destroyed, and then collapses on whoever is under it.
    static class RoofSupportUtility {

        // Removes constructed roofs in rect that the game would collapse: not connected to a roof holder, or out of its range.
        // Natural (thick) roofs are left alone. Returns the number of roof cells removed.
        public static int RemoveUnsupportedRoofs(Map map, CellRect rect) {
            rect = rect.ClipInsideMap(map);
            HashSet<IntVec3> visited = new HashSet<IntVec3>();
            HashSet<IntVec3> unsupported = new HashSet<IntVec3>();
            int removed = 0;

            //removing a cell can cut the only path that kept another cell within range of a holder, so repeat until stable
            do {
                unsupported.Clear();
                visited.Clear();
                //same as vanilla's ensureCanHoldRoof: ConnectsToRoofHolder reports already visited cells as connected,
                //so a whole unconnected group is collected the first time one of its cells is found
                foreach (IntVec3 c in rect) {
                    if (IsConstructedRoof(map, c) && !unsupported.Contains(c) && !RoofCollapseCellsFinder.ConnectsToRoofHolder(c, map, visited)) {
                        map.floodFiller.FloodFill(c, x => x.Roofed(map), x => {
                            if (IsConstructedRoof(map, x)) unsupported.Add(x);
                        });
                    }
                }
                foreach (IntVec3 c in rect) {
                    if (IsConstructedRoof(map, c) && !unsupported.Contains(c) && !RoofCollapseUtility.WithinRangeOfRoofHolder(c, map)) {
                        unsupported.Add(c);
                    }
                }
                foreach (IntVec3 c in unsupported) {
                    map.roofGrid.SetRoof(c, null);
                }
                removed += unsupported.Count;
            } while (unsupported.Count > 0);

            if (removed > 0) {
                Debug.Log(Debug.BlueprintTransfer, "Removed {0} roof cells without support in {1}", removed, rect);
            }
            return removed;
        }

        private static bool IsConstructedRoof(Map map, IntVec3 c) {
            RoofDef roof = map.roofGrid.RoofAt(c);
            return roof != null && !roof.isThickRoof;
        }
    }
}
