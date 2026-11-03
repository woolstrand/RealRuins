using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

using RimWorld;
using Verse;

namespace RealRuins {
    // Tech level cap (ScatterOptions.maxTechLevel). Used by the preprocessor, so the analyzer and every scatterer see the
    // same filtered blueprint. Safe to call from a background thread (the analyzer runs there).
    static class TechLevelFilter {
        // Limited strategy: chance for a thing one tech level above the cap to stay. Each further level multiplies it again.
        public const float limitedKeepChance = 0.1f;

        private static readonly ConcurrentDictionary<BuildableDef, TechLevel> techLevels = new ConcurrentDictionary<BuildableDef, TechLevel>();

        public static bool IsActive(ScatterOptions options) => options != null && options.maxTechLevel != TechLevel.Undefined;

        // Most things have no tech level of their own (buildings almost never do), so it is taken from what it takes to make one:
        // own tech level, research needed to build or craft it, and tech level of the ingredients. Undefined if none of them has one.
        public static TechLevel TechLevelOf(BuildableDef def) {
            if (def == null) return TechLevel.Undefined;
            return techLevels.GetOrAdd(def, Calculate);
        }

        private static TechLevel Calculate(BuildableDef def) {
            ThingDef thingDef = def as ThingDef;
            TechLevel result = thingDef?.techLevel ?? TechLevel.Undefined;
            result = Max(result, def.researchPrerequisites);
            if (thingDef?.recipeMaker != null) {
                result = Max(result, thingDef.recipeMaker.researchPrerequisite);
                result = Max(result, thingDef.recipeMaker.researchPrerequisites);
            }
            if (def.costList != null) {
                //one level deep: an ingredient's own tech level only, which also keeps recipe cycles out
                foreach (ThingDefCountClass cost in def.costList) {
                    if (cost.thingDef != null && cost.thingDef.techLevel > result) result = cost.thingDef.techLevel;
                }
            }
            return result;
        }

        private static TechLevel Max(TechLevel level, ResearchProjectDef research) {
            return research != null && research.techLevel > level ? research.techLevel : level;
        }

        private static TechLevel Max(TechLevel level, List<ResearchProjectDef> research) {
            if (research == null) return level;
            foreach (ResearchProjectDef project in research) level = Max(level, project);
            return level;
        }

        // False if the thing has to go. With the limited strategy it is a roll, so the same blueprint gives different results.
        public static bool Allows(ScatterOptions options, BuildableDef def, ThingDef stuff = null) {
            if (!IsActive(options)) return true;
            TechLevel level = TechLevelOf(def);
            TechLevel stuffLevel = TechLevelOf(stuff);
            if (stuffLevel > level) level = stuffLevel;

            int levelsAbove = (int)level - (int)options.maxTechLevel;
            if (levelsAbove <= 0) return true;
            if (options.techLimitStrict) return false;
            return ThreadSafeRand.Chance((float)Math.Pow(limitedKeepChance, levelsAbove));
        }

        public static bool Allows(ScatterOptions options, ItemTile item) {
            if (!IsActive(options)) return true;
            ThingDef def = DefDatabase<ThingDef>.GetNamed(item.defName, false);
            ThingDef stuff = item.stuffDef != null ? DefDatabase<ThingDef>.GetNamed(item.stuffDef, false) : null;
            return Allows(options, def, stuff);
        }

        // Takes out contents above the cap, at any depth. Empty minified things are not spawned (BlueprintTransferUtility).
        public static void RemoveContents(ScatterOptions options, ItemTile container) {
            if (container.innerItems == null) return;
            foreach (ItemTile inner in container.innerItems.ToArray()) {
                RemoveContents(options, inner);
                if (!Allows(options, inner)) inner.TakeOutOfContainer();
            }
        }
    }
}
