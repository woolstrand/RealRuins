using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

using RimWorld;
using Verse;

namespace RealRuins {

    class BlueprintAnalyzerResult {
        public int totalArea;
        public int itemsCount;
        public int occupiedTilesCount;
        public int haulableItemsCount;
        public int haulableStacksCount;
        public int itemsInside;
        public int militaryItemsCount;
        public float totalItemsCost;
        public float haulableItemsCost;
        public int wallLength;
        public int roomsCount;
        public int internalArea;
        public int defensiveItemsCount;
        public int mannableCount;
        public int productionItemsCount;
        public int bedsCount;

        public override string ToString() {
            string message = "Analyzer result:\r\n";

            FieldInfo[] fields = GetType().GetFields();

            foreach (FieldInfo fi in fields) {
                message += (fi.Name + ": " + fi.GetValue(this) + "\r\n");
            }

            return message;
        }
    }

    class ItemStatRecord {
        public int stacksCount;
        public int totalCount;
        public float cost;
    }

    // This class analyzes provided blueprint and decides which category does it belongs to.
    // Also it generates some metadata and description of the most valuable things
    class BlueprintAnalyzer {

        private Blueprint blueprint;
        public POIType determinedType;
        private float militaryFeatures;
        public float militaryPower;
        public int mannableCount;
        public int approximateDisplayValue;
        public List<string> randomMostValueableItemDefNames;

        public BlueprintAnalyzerResult result { get; private set; }

        private Dictionary<string, ItemStatRecord> itemStats;
        private ScatterOptions options;

        public BlueprintAnalyzer(Blueprint blueprint, ScatterOptions options = null) {
            this.blueprint = blueprint;
            this.options = options ?? ScatterOptions.asIs();
            itemStats = new Dictionary<string, ItemStatRecord>();
        }

        private void ProcessTile(ItemTile itemTile, IntVec3 pos) {

            ThingDef itemDef = DefDatabase<ThingDef>.GetNamed(itemTile.defName, false);
            if (itemDef == null) return;

            ItemStatRecord stats = null;
            if (itemStats.ContainsKey(itemTile.defName)) {
                stats = itemStats[itemTile.defName];
            } else {
                stats = new ItemStatRecord();
                itemStats[itemTile.defName] = stats;
            }

            stats.stacksCount++;
            stats.totalCount += itemTile.stackCount;
            stats.cost += itemTile.cost;

            result.totalItemsCost += itemTile.cost;
            result.occupiedTilesCount++;
            result.itemsCount += itemTile.stackCount;

            if (itemTile.isWall) {
                result.wallLength++;
            }

            var message = itemTile.defName + " ";

            if (itemDef.alwaysHaulable) {
                message += "is haulable ";
                result.haulableStacksCount++;
                result.haulableItemsCount += itemTile.stackCount;
                result.haulableItemsCost += itemTile.cost;
            }

            string lowerName = itemTile.defName.ToLower();

            if (itemDef.IsShell || itemDef.IsRangedWeapon || lowerName.Contains("turret") || lowerName.Contains("cannon") || lowerName.Contains("gun")
               /* || lowerName.Contains("laser") || lowerName.Contains("marauder") and obelisk and punisher when RimAtomics will add faction split*/) {
                result.militaryItemsCount++;
                message += "military ";
                if (itemDef.building != null) {
                    message += "non-nul building ";
                    result.defensiveItemsCount++;
                    if (itemDef.building.IsTurret) {
                        message += "turret ";
                    }
                }
            }

            if (itemDef.HasComp(typeof(CompMannable))) {
                result.mannableCount++;
            }

            if (itemDef.IsWorkTable) {
                message += "worktable ";
                result.productionItemsCount++;
            }

            if (itemDef.IsBed) {
                message += "bed ";
                result.bedsCount++;
            }

            if (blueprint.wallMap[pos.x, pos.z] > 1 || blueprint.roofMap[pos.x, pos.z] == true) {
                result.itemsInside++;
            }


            if (!itemTile.isWall && !itemTile.isDoor && itemTile.cost > 100) {
               // Debug.Message(message);
            }
        }
  
        public void Analyze() {
            Debug.Log("analyzing blueprint {0} with options {1}", blueprint, options);
            blueprint.FindRooms();
            Debug.Log("Rooms found");
            BlueprintPreprocessor.ProcessBlueprint(blueprint, options);
            Debug.Log("Blueprint processed");

            result = new BlueprintAnalyzerResult();
            result.totalArea = blueprint.width * blueprint.height;
            result.roomsCount = blueprint.roomAreas.Count() - 2;

            result.internalArea = 0;
            for (int index = 2; index < blueprint.roomAreas.Count; index ++) {
                result.internalArea += blueprint.roomAreas[index];
            }

            blueprint.UpdateBlueprintStats(includeCost: true);
            Debug.Log(Debug.Analyzer, "Analyzing map");
            for (int y = 0; y < blueprint.height; y++) {
                for (int x = 0; x < blueprint.width; x++) {
                    List<ItemTile> tiles = blueprint.itemsMap[x, y] ?? new List<ItemTile>();
                    foreach (ItemTile itemTile in tiles) {
                        ProcessTile(itemTile, new IntVec3(x, 0, y));
                    }
                }
            }

            this.mannableCount = result.mannableCount;
            determinedType = supposedType();
            Debug.Log(Debug.Analyzer, "Type is {0} by {1}", determinedType, result.ToString());
        }

        private POIType supposedType() {
            float prodScore = 0;
            float researchScore = 0;

            militaryFeatures = 0;
            militaryPower = 0;

            //many walls, liitle internal space => ruins
            if (result.wallLength < 70 || (result.totalItemsCost < 2000 && result.bedsCount < 1)) {
                return POIType.Ruins;
            }

            //if 2/3 of all items (except walls) are outside (outside of room and unroofed), consider it a ruin. Caveat: enclosed rooms formed by mountains are not considered closed rooms.
            if (result.itemsInside < (result.occupiedTilesCount - result.wallLength) * 0.3) {
                return POIType.Ruins;
            }

            if (result.internalArea / result.wallLength < 2 && result.roomsCount < 6) {
                return POIType.Ruins;
            }

            //internal area here is definitely > 0, so occupiedTilesCount too
            if (result.totalItemsCost / result.occupiedTilesCount < 10) {
                return POIType.Ruins;
            }

            militaryFeatures = (float)(result.militaryItemsCount + (result.defensiveItemsCount * 10)) * 25 / result.internalArea;
            militaryPower = (float)(result.defensiveItemsCount * 250) / result.internalArea + 1;
            prodScore = result.productionItemsCount * 10 + (result.haulableStacksCount * 5 / result.internalArea);

            Debug.Log(Debug.Analyzer, "military features: {0}. power: {1}. prod: {2}.", militaryFeatures, militaryPower, prodScore);

            if (militaryFeatures < 3 && prodScore <= 50) {
                if (result.internalArea < 2000 && result.bedsCount > 0 && result.totalItemsCost < 30000) {
                    return POIType.Camp;
                }
                if (result.internalArea >= 2000 && result.roomsCount > 12) {
                    return POIType.City;
                }
                if ((float)(result.occupiedTilesCount - result.wallLength) / result.haulableStacksCount < 3 || result.bedsCount < 3) {
                    return POIType.Storage;
                }
                return POIType.Camp;
            }

            if (militaryFeatures >= 3 && militaryPower < 8 && prodScore < 50) {
                if (result.internalArea < 2000) {
                    return Rand.Chance(0.5f)?POIType.Outpost:POIType.Communication;
                }
                if (result.internalArea > 2000 && result.internalArea < 6000 && result.roomsCount < 20) {
                    return POIType.MilitaryBaseSmall;
                }
                if (result.internalArea > 6000 || result.roomsCount > 20) {
                    return POIType.City;
                }
            }

            if (militaryFeatures < 8 && prodScore >= 50) {
                if (result.haulableItemsCost / result.haulableItemsCount > 30) {
                    return POIType.Research;
                } else {
                    return POIType.Factory;
                }
            }

            if (militaryFeatures >= 8) {
                if (result.defensiveItemsCount > 15) {
                    return POIType.Stronghold;
                } else {
                    if (result.internalArea > 2500) {
                        return POIType.MilitaryBaseLarge;
                    } else {
                        return POIType.MilitaryBaseSmall;
                    }
                }
            }

            return POIType.Ruins;
        }

        public float chanceOfHavingFaction() {
            switch (determinedType) {
                case POIType.Ruins:
                    return 0.0f;

                case POIType.Camp:
                case POIType.Outpost:
                case POIType.Storage:
                    return 0.3f;

                case POIType.MilitaryBaseSmall:
                case POIType.Communication:
                    return 0.7f;

                case POIType.City:
                case POIType.Stronghold:
                case POIType.MilitaryBaseLarge:
                case POIType.Research:
                case POIType.PowerPlant:
                case POIType.Factory:
                    return 0.9f;

                default:
                    return 0.5f;
            }
        }
    }
}
