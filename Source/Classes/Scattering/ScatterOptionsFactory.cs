using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using RimWorld;
using Verse;

namespace RealRuins {
    // The single place where each scatterer turns mod options into its ScatterOptions.
    // Every method returns a fresh object which is never linked to the mod options store.
    static class ScatterOptionsFactory {

        // Regular ruins chunks scattered over a generic map. Honors all map generation mod options.
        public static ScatterOptions ForGenericRuins() {
            ScatterOptions options = FromModOptions();
            ApplyContentFilters(options);
            ApplyTechLimit(options, RealRuins_ModSettings.techLimitRuins);

            options.doorOwnershipProbability = 0.05f;
            options.allowFriendlyRaids = !RealRuins_ModSettings.disableFriendlyRaids;
            options.avoidUsedRects = true;
            return options;
        }

        // Single medium-sized ruin spotted by a caravan.
        public static ScatterOptions ForCaravanSmallRuins() {
            ScatterOptions options = FromModOptions();
            ApplyContentFilters(options);
            ApplyTechLimit(options, RealRuins_ModSettings.techLimitEvents);

            options.minRadius = 24;
            options.maxRadius = 50;
            options.scavengingMultiplier = 0.1f;
            options.deteriorationMultiplier = 0.1f;
            options.hostileChance = 0.8f;
            options.itemCostLimit = 800;

            float costLimit = RealRuins_ModSettings.CaravanRuinsCostLimit;
            options.costCap = costLimit >= 0 ? Math.Max(1, (int)costLimit) : -1; //a cap of 0 would mean no cap
            options.cutDismantlesWalls = true;
            options.minimumCostRequired = costLimit >= 0 ? (int)Math.Min(15000.0f, costLimit) : 15000;
            options.minimumDensityRequired = 0.01f;
            options.minimumAreaRequired = 4000;
            options.deleteLowQuality = false; //do not delete since we have much higher requirements for base ruins
            options.shouldKeepDefencesAndPower = true;
            return options;
        }

        // Large abandoned base event. Content filters (walls & doors only, no items) are deliberately not applied.
        public static ScatterOptions ForAbandonedBase(string blueprintFileName, int costCap, float raidersActivity) {
            ScatterOptions options = FromModOptions();
            ApplyTechLimit(options, RealRuins_ModSettings.techLimitEvents);

            options.minRadius = 200;
            options.maxRadius = 200;
            options.scavengingMultiplier = 0.1f;
            options.deteriorationMultiplier = 0.0f;
            options.hostileChance = 1.0f;
            options.doorOwnershipProbability = 0.05f;

            options.blueprintFileName = blueprintFileName;
            options.costCap = costCap;
            options.cutDismantlesWalls = true; //scavenged base: a half-dismantled golden room fits the story
            options.startingPartyPoints = (int)raidersActivity;
            options.minimumCostRequired = (int)Math.Min(100000.0f, RealRuins_ModSettings.ruinsCostCap);
            options.minimumDensityRequired = 0.015f;
            options.minimumAreaRequired = 6400;
            options.deleteLowQuality = false; //do not delete since we have much higher requirements for base ruins
            options.shouldKeepDefencesAndPower = true;
            options.shouldLoadPartOnly = false;
            options.shouldAddRaidTriggers = Find.Storyteller.difficulty.allowBigThreats && RealRuins_ModSettings.forceMultiplier != 0;
            options.claimableBlocks = false;
            options.enableDeterioration = false;
            options.allowFriendlyRaids = !RealRuins_ModSettings.disableFriendlyRaids;
            return options;
        }

        // Planetary POI. Spawns either as ruins (content filters apply) or as an intact base (they don't).
        // takenOver: the player starts the game in the intact base and owns all of it.
        public static ScatterOptions ForPOI(string blueprintFileName, Blueprint blueprint, POIType poiType, bool hasFaction, bool forceSpawnAsRuins, bool takenOver = false) {
            ScatterOptions options = FromModOptions();
            ApplyTechLimit(options, RealRuins_ModSettings.techLimitPOIs); //intact bases too: the cap is about the era, not about ruins

            options.minRadius = 400;
            options.maxRadius = 400;
            options.scavengingMultiplier = 0.0f;
            options.deteriorationMultiplier = 0.0f;
            options.hostileChance = 0.0f;

            options.blueprintFileName = blueprintFileName;
            options.doorOwnershipProbability = 1.0f;
            options.costCap = -1;
            options.startingPartyPoints = -1;
            options.minimumCostRequired = 0;
            options.minimumDensityRequired = 0.0f;
            options.minimumAreaRequired = 0;
            options.deleteLowQuality = false;
            options.shouldKeepDefencesAndPower = true;
            options.shouldLoadPartOnly = false;
            options.shouldAddRaidTriggers = false;
            options.claimableBlocks = false;

            bool spawnAsRuins = poiType == POIType.Ruins || !hasFaction || forceSpawnAsRuins;
            if (spawnAsRuins) {
                ApplyContentFilters(options);
                if (hasFaction) {
                    options.doorOwnershipProbability = 0.0f;
                }
                options.shouldAddFilth = true;
                options.forceFullHitPoints = false;
                options.enableDeterioration = true;
                options.overwritesEverything = false;
                options.costCap = (int)Math.Abs(Rand.Gaussian(0, Math.Max(5000, blueprint.width * blueprint.height)));
                options.cutDismantlesWalls = true;
                options.itemCostLimit = Rand.Range(50, 500);
            } else {
                options.shouldAddFilth = false;
                options.forceFullHitPoints = true;
                options.enableDeterioration = false;
                options.overwritesEverything = true;
                options.claimEverything = takenOver;
            }

            options.overridePosition = new IntVec3(blueprint.originX, 0, blueprint.originZ);
            options.centerIfExceedsBounds = true;
            return options;
        }

        // Blueprint analysis (planetary POIs). Sees the full, untouched blueprint: it has to know what the blueprint could be in
        // theory, so no mod options apply here. The exception is the tech level cap: what is above it never spawns, so a base
        // full of turrets which can't exist must not be classified as a military one.
        public static ScatterOptions ForAnalyzer() {
            ScatterOptions options = new ScatterOptions {
                overwritesEverything = true,
                canHaveFood = true,
                scavengingMultiplier = 0.0f,
                decorationChance = 0.0f,
                enableDeterioration = false,
                forceFullHitPoints = true,
                shouldAddFilth = false,
                trapChance = 0.0f,

                startingPartyPoints = -1,
                minimumCostRequired = 0,
                minimumDensityRequired = 0.0f,
                minimumAreaRequired = 0,
            };
            ApplyTechLimit(options, RealRuins_ModSettings.techLimitPOIs);
            return options;
        }

        // Generation tuning mod options every scatterer starts from. Scatterers override what they need.
        private static ScatterOptions FromModOptions() {
            ScatterOptions mod = RealRuins_ModSettings.defaultScatterOptions ?? new ScatterOptions();
            return new ScatterOptions {
                densityMultiplier = mod.densityMultiplier,
                minRadius = mod.minRadius,
                maxRadius = mod.maxRadius,
                deteriorationMultiplier = mod.deteriorationMultiplier,
                scavengingMultiplier = mod.scavengingMultiplier,
                itemCostLimit = mod.itemCostLimit,
                claimableBlocks = mod.claimableBlocks,
                enableProximity = mod.enableProximity,
                decorationChance = mod.decorationChance,
                trapChance = mod.trapChance,
                hostileChance = mod.hostileChance,
            };
        }

        // Tech level cap mod options, for scatterers of the given group (regular ruins, POIs, events).
        private static void ApplyTechLimit(ScatterOptions options, bool groupEnabled) {
            if (!RealRuins_ModSettings.techLimitEnabled || !groupEnabled) return;
            options.maxTechLevel = RealRuins_ModSettings.techLimitMaxLevel;
            options.techLimitStrict = RealRuins_ModSettings.techLimitStrict;
        }

        // Content filter mod options. Applied only to scatterers which produce ruined structures.
        private static void ApplyContentFilters(ScatterOptions options) {
            ScatterOptions mod = RealRuins_ModSettings.defaultScatterOptions ?? new ScatterOptions();
            options.wallsDoorsOnly = mod.wallsDoorsOnly;
            options.disableSpawnItems = mod.disableSpawnItems;
        }
    }
}
