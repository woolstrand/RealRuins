
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RealRuins {
    //Ordinals are persisted in saves and used in texture and translation keys: append new values only.
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
        Ruins,
        Stronghold,
        Village,
        Castle,
        Workshop
    }

    static class POITypeUtility {
        // Least advanced faction which can own a POI of the type. Also the era the type belongs to: a type is not used
        // below it (FitToEra).
        public static TechLevel MinTechLevel(POIType poiType) {
            switch (poiType) {
                case POIType.Camp:
                case POIType.Village:
                    return TechLevel.Neolithic;
                case POIType.Outpost:
                case POIType.Storage:
                case POIType.Castle:
                case POIType.Workshop:
                    return TechLevel.Medieval;
                case POIType.Factory:
                case POIType.Research:
                case POIType.City:
                case POIType.Communication:
                case POIType.Stronghold:
                case POIType.MilitaryBaseLarge:
                case POIType.MilitaryBaseSmall:
                case POIType.PowerPlant:
                    return TechLevel.Industrial;
                default:
                    return TechLevel.Undefined;
            }
        }

        // The same kind of place one era earlier: settlement, production or military.
        private static POIType LowerEraCounterpart(POIType poiType) {
            switch (poiType) {
                case POIType.City:
                    return POIType.Village;
                case POIType.Factory:
                case POIType.Research:
                case POIType.PowerPlant:
                    return POIType.Workshop;
                case POIType.MilitaryBaseSmall:
                case POIType.MilitaryBaseLarge:
                case POIType.Stronghold:
                    return POIType.Castle;
                case POIType.Communication:
                    return POIType.Outpost;
                case POIType.Castle:
                case POIType.Workshop:
                    return POIType.Village;
                case POIType.Outpost:
                case POIType.Storage:
                    return POIType.Camp;
                default:
                    return POIType.Ruins;
            }
        }

        // Steps the type down to its counterparts until it belongs to the era. Undefined era: no limit.
        public static POIType FitToEra(POIType poiType, TechLevel era) {
            if (era == TechLevel.Undefined) return poiType;
            while (MinTechLevel(poiType) > era) {
                poiType = LowerEraCounterpart(poiType);
            }
            return poiType;
        }
    }

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
