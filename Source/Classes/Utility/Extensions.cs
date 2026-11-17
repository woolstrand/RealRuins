using RimWorld;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Verse;

namespace RealRuins {
    static class ArrayExtension {
        public static void Blur(this float[,] map, int stepsCount) {
            int width = map.GetLength(0);
            int height = map.GetLength(1);
            float[,] delta = new float[width, height]; //delta integrity for making blur
            // - then blur the map to create gradient deterioration around the intact area.

            for (int steps = 0; steps < stepsCount; steps++) { //terrain map
                for (int x = 1; x < width - 1; x++) {
                    for (int z = 1; z < height - 1; z++) {
                        delta[x, z] =
                           (map[x - 1, z - 1] + map[x, z - 1] + map[x + 1, z - 1] +
                            map[x - 1, z] + map[x, z] + map[x + 1, z] +
                            map[x - 1, z + 1] + map[x, z + 1] + map[x + 1, z + 1]) / 9.0f - map[x, z];
                    }
                }
                for (int x = 1; x < width - 1; x++) {
                    for (int z = 1; z < height - 1; z++) {
                        if (map[x, z] < 1) {
                            map[x, z] += delta[x, z];
                        }
                    }
                }
            }
        }
    }

    static class StringExtension {
        public static string SanitizeForFileSystem(this string filename) {
            string invalidChars = new string(Path.GetInvalidFileNameChars());
            string escapedInvalidChars = Regex.Escape(invalidChars);
            string invalidRegex = string.Format(@"([{0}]*\.+$)|([{0}]+)", escapedInvalidChars);

            return Regex.Replace(filename, invalidRegex, "_");
        }
    }

    // -------------------- cost and weight related methods --------------------

    static class ThingDefExtension {
        //Market value as the game calculates it for a def: materials plus work. Quality is rolled only when a thing is spawned,
        //so it is not included. A def made from stuff without a stuff in the blueprint is valued with its default stuff.
        //The single source of blueprint costs (Blueprint.UpdateBlueprintStats). Cached: the value of a def never changes in a session,
        //and a blueprint is costed several times per ruin. Locked: the planetary loader analyzes blueprints off the main thread.
        static public float AbstractMarketValue(this BuildableDef buildable, ThingDef stuffDef = null) {
            if (buildable == null) return 0;

            if (buildable is ThingDef thingDef && thingDef.MadeFromStuff) {
                if (stuffDef == null) stuffDef = GenStuff.DefaultStuffFor(thingDef);
            } else {
                stuffDef = null;
            }

            var key = (buildable, stuffDef);
            lock (marketValueCache) {
                if (marketValueCache.TryGetValue(key, out float cached)) return cached;
            }
            float value = buildable.GetStatValueAbstract(StatDefOf.MarketValue, stuffDef);
            lock (marketValueCache) {
                marketValueCache[key] = value;
            }
            return value;
        }

        private static readonly Dictionary<(BuildableDef, ThingDef), float> marketValueCache = new Dictionary<(BuildableDef, ThingDef), float>();

        static public float ThingWeight(this ThingDef thingDef, ThingDef stuffDef) {
            if (thingDef == null) return 0;

            float weight;
            try {
                weight = thingDef.GetStatValueAbstract(StatDefOf.Mass, stuffDef);
                if (weight != 0 && weight != 1.0f) return weight;
            } catch {
                //Debug.Warning(Debug.Generic, "GetStatValue Exception for {0} made of {1}", thingDef, stuffDef);
            }

            if (thingDef.costList == null) return 1.0f; //weight is either 1 or 0, no way to calculate real weight, weight 0 is invalid => returning 1.

            //Debug.Message("Weight of {0} was {1}, calculating recursively based on list", thing.defName, weight);
            //Debug.PrintArray(thing.costList.ToArray());
            weight = 0;
            foreach (ThingDefCountClass part in thingDef.costList) {
                weight += part.count * ThingWeight(part.thingDef, null);
            }
            //Debug.Message("Result: {0}", weight);

            return weight != 0 ? weight : 1.0f;
        }
    }
}
