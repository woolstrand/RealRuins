using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Verse;

namespace RealRuins {
    class ScatterOptions : IExposable {
        public float densityMultiplier = 1.0f;
        public int referenceRadiusAverage = 12;
        public float deteriorationMultiplier = 0.0f;
        public float scavengingMultiplier = 1.0f;

        public int itemCostLimit = 1000;
        public bool disableSpawnItems = false;
        public bool wallsDoorsOnly = false;
        public bool claimableBlocks = true;
        public bool enableProximity = true;

        public float decorationChance = 0.0001f; //probability PER CELL
        public float trapChance = 0.001f; //probability PER CELL
        public float hostileChance = 0.1f; //probability PER CHUNK

        public float uncoveredCost = 0; //output value. consider switching to resolvers and resolver params

        public int minimumSizeRequired = 0;
        public float minimumDensityRequired = 0.1f;
        public int minimumCostRequired = 0;
        public bool shouldKeepDefencesAndPower = false;
        public bool shouldAddSignificantResistance = false;
        public bool shouldCutBlueprint = true;
        public bool shouldAddRaidTriggers = false;


        public bool deleteLowQuality = true;

        public static readonly ScatterOptions Default = new ScatterOptions();


        public void ExposeData() {
            //Regex: public [a-z]* ([a-zA-Z]*) = [^;]*;     =>     Scribe_Values.Look(ref $1, "$1", 0.0f, false);

            Scribe_Values.Look(ref densityMultiplier, "densityMultiplier", 1.0f, false);
            Scribe_Values.Look(ref referenceRadiusAverage, "referenceRadiusAverage", 12, false);
            Scribe_Values.Look(ref deteriorationMultiplier, "deteriorationMultiplier", 0.0f, false);
            Scribe_Values.Look(ref scavengingMultiplier, "scavengingMultiplier", 1.0f, false);

            Scribe_Values.Look(ref itemCostLimit, "itemCostLimit", 1000, false);
            Scribe_Values.Look(ref disableSpawnItems, "disableSpawnItems", false, false);
            Scribe_Values.Look(ref wallsDoorsOnly, "wallsDoorsOnly", false, false);
            Scribe_Values.Look(ref claimableBlocks, "claimableBlocks", true, false);
            Scribe_Values.Look(ref enableProximity, "enableProximity", true, false);

            Scribe_Values.Look(ref decorationChance, "decorationChance", 0.0001f, false);
            Scribe_Values.Look(ref trapChance, "trapChance", 0.001f, false);
            Scribe_Values.Look(ref hostileChance, "hostileChance", 0.1f, false);

        }

        public ScatterOptions Copy() {
            ScatterOptions copy = new ScatterOptions {
                deteriorationMultiplier = deteriorationMultiplier,
                claimableBlocks = claimableBlocks,
                decorationChance = decorationChance,
                densityMultiplier = densityMultiplier,
                hostileChance = hostileChance,
                scavengingMultiplier = scavengingMultiplier,
                trapChance = trapChance,
                disableSpawnItems = disableSpawnItems,
                itemCostLimit = itemCostLimit,
                referenceRadiusAverage = referenceRadiusAverage,
                wallsDoorsOnly = wallsDoorsOnly,
                enableProximity = enableProximity,
                minimumSizeRequired = minimumSizeRequired,
                minimumDensityRequired = minimumDensityRequired,
                minimumCostRequired = minimumCostRequired,
                deleteLowQuality =deleteLowQuality,
                shouldKeepDefencesAndPower = shouldKeepDefencesAndPower,
                shouldAddSignificantResistance = shouldAddSignificantResistance,
                shouldCutBlueprint = shouldCutBlueprint,
                shouldAddRaidTriggers = shouldAddRaidTriggers,
                uncoveredCost = uncoveredCost
            };

            return copy;
        }

    }
}
