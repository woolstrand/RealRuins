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
        public int militaryItemsCount;
        public float totalItemsCost;
        public float haulableItemsCost;
        public int wallLength;
        public int roomsCount;
        public int internalArea;
        public int defensiveItemsCount;
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
        public int approximateDisplayValue;
        public List<string> randomMostValueableItemDefNames;

        public BlueprintAnalyzerResult result { get; private set; }

        private Dictionary<string, ItemStatRecord> itemStats;
        private ScatterOptions options;

        public BlueprintAnalyzer(Blueprint blueprint, ScatterOptions options) {
            this.blueprint = blueprint;
            this.options = options;
            itemStats = new Dictionary<string, ItemStatRecord>();
        }

        private void ProcessTile(ItemTile itemTile) {
            if (!itemTile.isWall) {
                //Debug.Message("processing item {0}", itemTile.defName);
            }

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

            if (itemDef.IsShell || itemDef.IsWeapon || lowerName.Contains("turret") || lowerName.Contains("cannon") || lowerName.Contains("gun")) {
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

            if (itemDef.IsWorkTable) {
                message += "worktable ";
                result.productionItemsCount++;
            }

            if (itemDef.IsBed) {
                message += "bed ";
                result.bedsCount++;
            }

            if (!itemTile.isWall && !itemTile.isDoor && itemTile.cost > 100) {
                Debug.Message(message);
            }
        }
  
        public void Analyze() {
            Debug.Message("analyzing blueprint {0} with options {1}", blueprint, options);
            blueprint.FindRooms();
            Debug.Message("Rooms found");
            BlueprintPreprocessor.ProcessBlueprint(blueprint, options);
            Debug.Message("Blueprint processed");

            result = new BlueprintAnalyzerResult();
            result.totalArea = blueprint.width * blueprint.height;
            result.roomsCount = blueprint.roomAreas.Count() - 2;

            result.internalArea = 0;
            for (int index = 2; index < blueprint.roomAreas.Count; index ++) {
                result.internalArea += blueprint.roomAreas[index];
            }

            blueprint.UpdateBlueprintStats(includeCost: true);
            Debug.Message("Analyzing map");
            for (int y = 0; y < blueprint.height; y++) {
                for (int x = 0; x < blueprint.width; x++) {
                    List<ItemTile> tiles = blueprint.itemsMap[x, y] ?? new List<ItemTile>();
                    foreach (ItemTile itemTile in tiles) {
                        ProcessTile(itemTile);
                    }
                }
            }

            Debug.Message(result.ToString());
        }
    }
}
