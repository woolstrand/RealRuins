using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

using RimWorld;
using Verse;

namespace RealRuins {
    // Analyzes random cached blueprints under several tech level caps and counts the POI types, the way planetary POIs
    // are classified at game start (RealRuinsPOIFactory.PreparePOI). Runs as an asynchronous long event, nothing is spawned.
    // Same seed every run, so the same blueprints are compared after a classifier change.
    // Logs a type x cap table and writes every analysis with its metrics to RealRuinsPOISample.csv in the save data folder.
    public static class POITypeSampler {
        private const int seed = 20261103;

        private class CapConfig {
            public string label;
            public TechLevel maxTechLevel;
            public bool strict;
        }

        private static readonly CapConfig[] configs = {
            new CapConfig { label = "NoCap", maxTechLevel = TechLevel.Undefined, strict = true },
            new CapConfig { label = "Spacer", maxTechLevel = TechLevel.Spacer, strict = true },
            new CapConfig { label = "Industrial", maxTechLevel = TechLevel.Industrial, strict = true },
            new CapConfig { label = "Medieval", maxTechLevel = TechLevel.Medieval, strict = true },
            new CapConfig { label = "MedievalLimited", maxTechLevel = TechLevel.Medieval, strict = false },
            new CapConfig { label = "Neolithic", maxTechLevel = TechLevel.Neolithic, strict = true },
        };

        private const string skipped = "Skipped";

        public static void Run(int count) {
            LongEventHandler.QueueLongEvent(() => Sample(count), "Sampling POI types", true, e => Log.Error("RealRuins: POI type sampling failed: " + e));
        }

        private static void Sample(int count) {
            DateTime start = DateTime.UtcNow;
            string folder = SnapshotStoreManager.GetSnapshotsFolderPath();
            List<string> files = Directory.GetFiles(folder, "*.bp", SearchOption.AllDirectories).OrderBy(f => f).ToList();
            Random random = new Random(seed);
            List<string> picked = files.OrderBy(f => random.Next()).Take(count).ToList();

            FieldInfo[] metrics = typeof(BlueprintAnalyzerResult).GetFields();
            FieldInfo[] scores = typeof(POIClassification).GetFields().Where(f => f.FieldType == typeof(float)).ToArray();
            StringBuilder csv = new StringBuilder();
            csv.AppendLine(string.Join(",", new[] { "file", "cap", "type", "functionalType", "era" }
                .Concat(scores.Select(f => f.Name)).Concat(metrics.Select(f => f.Name))));

            //type -> cap -> count
            var table = new Dictionary<string, int[]>();
            int failed = 0;
            for (int i = 0; i < picked.Count; i++) {
                LongEventHandler.SetCurrentEventText(string.Format("Sampling POI types {0}/{1}", i + 1, picked.Count));
                for (int c = 0; c < configs.Length; c++) {
                    //the pipeline modifies the blueprint in place: load it again for each cap
                    Blueprint bp = BlueprintLoader.LoadWholeBlueprintAtPath(picked[i]);
                    if (bp == null) {
                        if (c == 0) failed++;
                        break;
                    }
                    ScatterOptions options = ScatterOptionsFactory.ForAnalyzer();
                    options.maxTechLevel = configs[c].maxTechLevel;
                    options.techLimitStrict = configs[c].strict;
                    BlueprintAnalyzer analyzer = new BlueprintAnalyzer(bp, options);
                    try {
                        analyzer.Analyze();
                    } catch (Exception e) {
                        Log.Warning("RealRuins: analysis of " + picked[i] + " failed: " + e);
                        if (c == 0) failed++;
                        break;
                    }

                    //same size filter as PreparePOI
                    bool tooSmall = analyzer.result.occupiedTilesCount < 50 || analyzer.result.totalArea < 200;
                    string type = tooSmall ? skipped : analyzer.determinedType.ToString();
                    if (!table.TryGetValue(type, out int[] row)) table[type] = row = new int[configs.Length];
                    row[c]++;

                    POIClassification cl = analyzer.classification;
                    csv.AppendLine(string.Join(",", new[] { Path.GetFileNameWithoutExtension(picked[i]), configs[c].label, type, cl.functionalType.ToString(), cl.era.ToString() }
                        .Concat(scores.Select(f => Convert.ToString(f.GetValue(cl), System.Globalization.CultureInfo.InvariantCulture)))
                        .Concat(metrics.Select(f => Convert.ToString(f.GetValue(analyzer.result), System.Globalization.CultureInfo.InvariantCulture)))));
                }
            }

            string csvPath = Path.Combine(GenFilePaths.SaveDataFolderPath, "RealRuinsPOISample.csv");
            File.WriteAllText(csvPath, csv.ToString());

            StringBuilder report = new StringBuilder();
            report.AppendFormat("RealRuins: POI types of {0} random blueprints ({1} failed to load or analyze), {2:F0} s. Metrics: {3}\n",
                picked.Count, failed, DateTime.UtcNow.Subtract(start).TotalSeconds, csvPath);
            report.Append(string.Format("{0,-18}", "type"));
            foreach (CapConfig config in configs) report.Append(string.Format("{0,16}", config.label));
            report.Append("\n");
            foreach (var pair in table.OrderBy(p => p.Key == skipped ? 1 : 0).ThenBy(p => p.Key)) {
                report.Append(string.Format("{0,-18}", pair.Key));
                foreach (int n in pair.Value) report.Append(string.Format("{0,16}", n));
                report.Append("\n");
            }
            Log.Message(report.ToString());
        }
    }
}
