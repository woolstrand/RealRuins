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

        public float decorationChance = 0.0001f; //probability PER CELL
        public float trapChance = 0.001f; //probability PER CELL
        public float hostileChance = 0.1f; //probability PER CHUNK

        public static ScatterOptions Default = new ScatterOptions();


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

            Scribe_Values.Look(ref decorationChance, "decorationChance", 0.0001f, false);
            Scribe_Values.Look(ref trapChance, "trapChance", 0.001f, false);
            Scribe_Values.Look(ref hostileChance, "hostileChance", 0.1f, false);

        }

    }
}
