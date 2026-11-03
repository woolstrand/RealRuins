using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

using RimWorld;
using Verse;

namespace RealRuins {
    // Debug measurement of what a scattering run actually spawned, to compare the estimated cost of ruins with their real wealth.
    // Collected only when RealRuins_ModSettings.debugLogRuinsWealth is on, logged regardless of the log level.
    class RuinsWealthReport {
        public static bool Enabled => RealRuins_ModSettings.debugLogRuinsWealth;

        public string blueprintName;
        public float allowance = -1; // share of the map's ruins wealth limit this run got, -1 for none
        public float estimate; // tile cost of what was transferred
        public float counted; // what was charged to the map's ruins wealth limit
        public float items; // actual market value of spawned things, contents and quality included
        public float terrain; // actual market value of placed terrain
        public int thingsCount;
        private readonly List<KeyValuePair<string, float>> things = new List<KeyValuePair<string, float>>();
        // estimate minus actual value, per def: where the estimate is off
        private readonly Dictionary<string, float> gaps = new Dictionary<string, float>();

        public float Actual => items + terrain;

        // Map totals collected while RuinsWealthSampler runs, null otherwise.
        public static List<MapWealthSample> samples;

        public void AddThing(Thing thing, float estimated) {
            if (thing is Pawn) return; //not loot
            float value = WealthUtility.MarketValueWithContents(thing, out int contentsCount);
            AddGap(thing.def.defName, estimated - value);
            items += value;
            thingsCount++;
            string label = thing.LabelCap;
            if (contentsCount > 0) label += " (+" + contentsCount + " inside)";
            things.Add(new KeyValuePair<string, float>(label, value));
        }

        public void AddTerrain(TerrainDef def, float estimated) {
            float value = 0;
            try {
                value = def.AbstractMarketValue();
            } catch (Exception) { }
            terrain += value;
            AddGap(def.defName, estimated - value);
        }

        private void AddGap(string defName, float gap) {
            gaps[defName] = gaps.TryGetValue(defName, out float sum) ? sum + gap : gap;
        }

        public string Summary(int topCount = 5) {
            string allowanceStr = allowance >= 0 ? string.Format(", allowance {0:F0}", allowance) : "";
            return string.Format("{0}: estimate {1:F0}{7}, counted {8:F0}, actual {2:F0} (things {3:F0} in {4}, terrain {5:F0}). Top: {6}",
                blueprintName ?? "?", estimate, Actual, items, thingsCount, terrain, TopThings(things, topCount), allowanceStr, counted);
        }

        private static string TopThings(IEnumerable<KeyValuePair<string, float>> things, int count) {
            return string.Join("; ", things.OrderByDescending(p => p.Value).Take(count).Select(p => string.Format("{0} {1:F0}", p.Key, p.Value)));
        }

        // One greppable line per map (prefix "MAP") with the totals, followed by every chunk and the most valuable things of the whole map.
        public static void LogMapSummary(Map map, IList<RuinsWealthReport> reports, string conditions, MapWealthSample timing = null) {
            var builder = new StringBuilder();
            builder.AppendFormat("MAP tile={0} size={1}x{2} chunks={3} estimate={4:F0} counted={10:F0} actual={5:F0} things={6:F0} terrain={7:F0} maxChunk={8:F0} {9}",
                map.Tile, map.Size.x, map.Size.z, reports.Count,
                reports.Sum(r => r.estimate), reports.Sum(r => r.Actual),
                reports.Sum(r => r.items), reports.Sum(r => r.terrain),
                reports.Count > 0 ? reports.Max(r => r.Actual) : 0, conditions, reports.Sum(r => r.counted));
            foreach (RuinsWealthReport report in reports.OrderByDescending(r => r.Actual)) {
                builder.AppendLine();
                builder.Append("  ").Append(report.Summary(3));
            }
            builder.AppendLine();
            builder.Append("  Most valuable on the map: ").Append(TopThings(reports.SelectMany(r => r.things), 10));
            builder.AppendLine();
            var mapGaps = reports.SelectMany(r => r.gaps).GroupBy(p => p.Key).Select(g => new KeyValuePair<string, float>(g.Key, g.Sum(p => p.Value)));
            builder.Append("  Estimate minus actual, by def: ").Append(string.Join("; ", mapGaps.OrderByDescending(p => Math.Abs(p.Value)).Take(8).Select(p => string.Format("{0} {1:+0;-0}", p.Key, p.Value))));
            Debug.Report(Debug.Wealth, "{0}", builder.ToString());

            samples?.Add(new MapWealthSample {
                scatterMs = timing?.scatterMs ?? 0,
                costMs = timing?.costMs ?? 0,
                chunks = reports.Count,
                estimate = reports.Sum(r => r.estimate),
                counted = reports.Sum(r => r.counted),
                actual = reports.Sum(r => r.Actual),
                maxChunk = reports.Count > 0 ? reports.Max(r => r.Actual) : 0,
            });
        }
    }

    // Time spent in blueprint cost calculation. Always counted (two timestamps per tile); blueprints are also
    // analyzed off the main thread, hence Interlocked.
    static class CostTiming {
        public static long ticks;

        public static long Now => System.Diagnostics.Stopwatch.GetTimestamp();

        public static void Add(long started) {
            Interlocked.Add(ref ticks, Now - started);
        }

        public static double Ms(long ticks) => ticks * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
    }

    class MapWealthSample {
        public double scatterMs; // GenStep_ScatterRealRuins, whole
        public double costMs; // blueprint cost calculation, part of scatterMs
        public double mapGenMs; // whole map generation, set by RuinsWealthSampler
        public int chunks;
        public float estimate;
        public float counted;
        public float actual;
        public float maxChunk;
    }
}
