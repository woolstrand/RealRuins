using System;
using System.Linq;

using RimWorld;
using Verse;

namespace RealRuins {
    // Wealth of things already on a map. Blueprint (not yet spawned) costs are ThingDefExtension.AbstractMarketValue,
    // set on tiles by Blueprint.UpdateBlueprintStats; both are the game's market value, so they can be compared.
    static class WealthUtility {
        private static readonly int[] wealthThresholds = { 0, 10000, 100000, 1000000, 10000000 };

        // Market value of a spawned thing with its quality and hit points, plus whatever it holds (bookcase, grave, reactor), at any depth.
        // A minified thing already reports its inner thing's value, so it is not opened; pawns and corpses are not opened either.
        public static float MarketValueWithContents(Thing thing) {
            return MarketValueWithContents(thing, out int _);
        }

        public static float MarketValueWithContents(Thing thing, out int contentsCount) {
            contentsCount = 0;
            if (thing == null) return 0;

            float value = 0;
            try {
                value = thing.MarketValue * thing.stackCount;
            } catch (Exception) { }

            if (!(thing is MinifiedThing) && !(thing is Pawn) && !(thing is Corpse) && thing is IThingHolder holder) {
                ThingOwner contents = holder.GetDirectlyHeldThings();
                if (contents != null) {
                    foreach (Thing inner in contents) {
                        value += MarketValueWithContents(inner, out int innerCount);
                        contentsCount += 1 + innerCount;
                    }
                }
            }
            return value;
        }

        // Total of MarketValueWithContents over the map's things (all of them if filter is null)
        public static float MapWealth(Map map, Func<Thing, bool> filter = null) {
            if (map == null) return 0;
            return map.listerThings.AllThings.Where(t => t.def != null && (filter == null || filter(t))).Sum(t => MarketValueWithContents(t));
        }

        // "RealRuins.RuinsWealth.N" description for a ruin of this value (0-10k, 10k-100k, ...), null for no value
        public static string WealthDescription(float cost) {
            if (cost <= 0) return null;
            for (int i = 0; i < wealthThresholds.Length - 1; i++) {
                if (cost <= wealthThresholds[i + 1]) return ("RealRuins.RuinsWealth." + i).Translate();
            }
            return ("RealRuins.RuinsWealth." + (wealthThresholds.Length - 1)).Translate();
        }
    }
}
