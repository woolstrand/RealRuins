using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using RimWorld;
using RimWorld.Planet;
using Verse;

enum POIType {
    MilitaryBaseSmall = 0,
    Outpost,
    Camp,
    City,
    Factory,
    Research,
    PowerPlant,
    MilitaryBaseLarge,
    Storage,
    Communication,
    Ruins
}

namespace RealRuins {
    class RealRuinsPOIComp : WorldObjectComp {
        public string blueprintName = "";
        public string gameName = "";
        public int originX = 0;
        public int originZ = 0;

        public float militaryPower = 1;
        public float approximateSnapshotCost = 1;
        public int bedsCount = 0;
        public int mannableCount = 0;
        public int poiType = 0;

        public override void Initialize(WorldObjectCompProperties props) {
            base.Initialize(props);
        }


        public override void PostExposeData() {
            base.PostExposeData();
            Scribe_Values.Look(ref blueprintName, "blueprintName", "");
            Scribe_Values.Look(ref gameName, "gameName", "");
            Scribe_Values.Look(ref originX, "originX", 0);
            Scribe_Values.Look(ref originZ, "originZ", 0);
            Scribe_Values.Look(ref poiType, "poiType", 0);
            Scribe_Values.Look(ref militaryPower, "militaryPower", 1);
            Scribe_Values.Look(ref approximateSnapshotCost, "approximateSnapshotCost", 1);
            Scribe_Values.Look(ref bedsCount, "bedsCount", 0);
            Scribe_Values.Look(ref mannableCount, "mannableCount", 0);
        }
    }


}
