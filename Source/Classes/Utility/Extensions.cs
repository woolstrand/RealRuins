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

    //Calculates cost of item made of stuff, or default cost if stuff is null
    //Golden wall is a [Wall] made of [Gold], golden tile is a [GoldenTile] made of default material
    static class ThingDefExtension {
        static public float ThingComponentsMarketCost(this BuildableDef buildable, ThingDef stuffDef = null) {
            float num = 0f;

            if (buildable == null) return 0; //can be for missing subcomponents, i.e. bed from alpha-poly. Bed does exist, but alpha poly does not.

            if (buildable.costList != null) {
                foreach (ThingDefCountClass cost in buildable.costList) {
                    num += cost.count * ThingComponentsMarketCost(cost.thingDef);
                }
            }

            if (buildable.costStuffCount > 0) {
                if (stuffDef == null) {
                    stuffDef = GenStuff.DefaultStuffFor(buildable);
                }

                if (stuffDef != null) {
                    num += (float)buildable.costStuffCount * stuffDef.BaseMarketValue * (1.0f / stuffDef.VolumePerUnit);
                }
            }

            if (num == 0) {
                if (buildable is ThingDef) {
                    if (((ThingDef)buildable).recipeMaker == null) {
                        return ((ThingDef)buildable).BaseMarketValue; //on some reason base market value is calculated wrong for, say, golden walls
                    }
                }
            }
            return num;
        }

        static public float ThingWeight(this ThingDef thingDef, ThingDef stuffDef) {
            if (thingDef == null) return 0;

            float weight = thingDef.GetStatValueAbstract(StatDefOf.Mass, stuffDef);
            if (weight != 0 && weight != 1.0f) return weight;
            if (thingDef.costList == null) return 1.0f; //weight is either 1 or 0, no way to calculate real weight, weight 0 is invalid => returning 1.

            //Debug.Message("Weight of {0} was {1}, calculating resursively based on list", thing.defName, weight);
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
