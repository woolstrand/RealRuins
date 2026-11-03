using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using RimWorld;
using Verse;

namespace RealRuins {
    // Debug measurement of what a scattering run actually spawned, to compare the estimated cost of ruins with their real wealth.
    // Collected only when RealRuins_ModSettings.debugLogRuinsWealth is on, logged regardless of the log level.
    class RuinsWealthReport {
        public static bool Enabled => RealRuins_ModSettings.debugLogRuinsWealth;

        public string blueprintName;
        public float estimate; // tile cost of what was transferred, new scheme
        public float legacyEstimate; // same, legacy scheme
        public float items; // actual market value of spawned things, contents and quality included
        public float terrain; // actual market value of placed terrain
        public int thingsCount;
        private readonly List<KeyValuePair<string, float>> things = new List<KeyValuePair<string, float>>();

        public float Actual => items + terrain;

        public void AddThing(Thing thing) {
            float value = ActualValue(thing, out int contentsCount);
            items += value;
            thingsCount++;
            string label = thing.LabelCap;
            if (contentsCount > 0) label += " (+" + contentsCount + " inside)";
            things.Add(new KeyValuePair<string, float>(label, value));
        }

        public void AddTerrain(TerrainDef def) {
            try {
                terrain += def.GetStatValueAbstract(StatDefOf.MarketValue);
            } catch (Exception) { }
        }

        // Market value of a spawned thing with its quality and hit points, plus whatever it holds (bookcase, grave, reactor).
        // A minified thing already reports its inner thing's value, so it is not opened. Pawns are not loot and are skipped.
        public static float ActualValue(Thing thing, out int contentsCount) {
            contentsCount = 0;
            if (thing == null || thing is Pawn) return 0;

            float value = 0;
            try {
                value = thing.MarketValue * thing.stackCount;
            } catch (Exception) { }

            if (!(thing is MinifiedThing) && thing is IThingHolder holder) {
                ThingOwner contents = holder.GetDirectlyHeldThings();
                if (contents != null) {
                    foreach (Thing inner in contents) {
                        value += ActualValue(inner, out int innerCount);
                        contentsCount += 1 + innerCount;
                    }
                }
            }
            return value;
        }

        public string Summary(int topCount = 5) {
            return string.Format("{0}: estimate {1:F0}, legacy {2:F0}, actual {3:F0} (things {4:F0} in {5}, terrain {6:F0}). Top: {7}",
                blueprintName ?? "?", estimate, legacyEstimate, Actual, items, thingsCount, terrain, TopThings(things, topCount));
        }

        private static string TopThings(IEnumerable<KeyValuePair<string, float>> things, int count) {
            return string.Join("; ", things.OrderByDescending(p => p.Value).Take(count).Select(p => string.Format("{0} {1:F0}", p.Key, p.Value)));
        }

        // One greppable line per map (prefix "MAP") with the totals, followed by every chunk and the most valuable things of the whole map.
        public static void LogMapSummary(Map map, IList<RuinsWealthReport> reports, string conditions) {
            var builder = new StringBuilder();
            builder.AppendFormat("MAP tile={0} size={1}x{2} chunks={3} estimate={4:F0} legacy={5:F0} actual={6:F0} things={7:F0} terrain={8:F0} maxChunk={9:F0} {10}",
                map.Tile, map.Size.x, map.Size.z, reports.Count,
                reports.Sum(r => r.estimate), reports.Sum(r => r.legacyEstimate), reports.Sum(r => r.Actual),
                reports.Sum(r => r.items), reports.Sum(r => r.terrain),
                reports.Count > 0 ? reports.Max(r => r.Actual) : 0, conditions);
            foreach (RuinsWealthReport report in reports.OrderByDescending(r => r.Actual)) {
                builder.AppendLine();
                builder.Append("  ").Append(report.Summary(3));
            }
            builder.AppendLine();
            builder.Append("  Most valuable on the map: ").Append(TopThings(reports.SelectMany(r => r.things), 10));
            Debug.Report(Debug.Wealth, "{0}", builder.ToString());
        }
    }
}
