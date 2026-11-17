using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RealRuins {
    // Snapshot of global mod options which the pipeline uses uniformly, regardless of the scatterer.
    // Options which a scatterer may override belong to ScatterOptions instead.
    class PipelineSettings {
        public HashSet<string> spawnBlacklist = new HashSet<string>();
        public HashSet<string> materialBlacklist = new HashSet<string>();
        public string fallbackMaterialName;
        public float forceMultiplier = 1.0f;
        public bool spawnRoofs = true;
        public bool wealthIgnoreStructure = true; //plain walls, doors and floors don't count toward the map's ruins wealth limit
        public bool wealthCountDamage = false; //the limit counts the actual value of spawned things instead of full price

        public static PipelineSettings FromModSettings() {
            return new PipelineSettings {
                spawnBlacklist = ParseList(RealRuins_ModSettings.spawnBlacklist),
                materialBlacklist = ParseList(RealRuins_ModSettings.materialBlacklist),
                fallbackMaterialName = RealRuins_ModSettings.fallbackMaterial,
                forceMultiplier = RealRuins_ModSettings.forceMultiplier,
                spawnRoofs = RealRuins_ModSettings.spawnRoofs,
                wealthIgnoreStructure = RealRuins_ModSettings.ruinsWealthIgnoreStructure,
                wealthCountDamage = RealRuins_ModSettings.ruinsWealthCountDamage,
            };
        }

        private static HashSet<string> ParseList(string list) {
            HashSet<string> result = new HashSet<string>();
            if (string.IsNullOrEmpty(list)) return result;

            foreach (string line in list.Split(new[] { "\r\n", "\r", "\n", "," }, StringSplitOptions.RemoveEmptyEntries)) {
                string trimmed = line.Trim();
                if (!string.IsNullOrEmpty(trimmed)) {
                    result.Add(trimmed);
                }
            }
            return result;
        }
    }
}
