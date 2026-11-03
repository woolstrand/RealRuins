using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

using RimWorld;
using Verse;

namespace RealRuins {

    // Metrics of a blueprint which POIClassifier decides on. Filled by Collect from a blueprint after FindRooms and preprocessing.
    class BlueprintAnalyzerResult {
        public int totalArea;
        public int itemsCount;
        public int occupiedTilesCount;
        public int haulableItemsCount;
        public int haulableStacksCount;
        public int itemsInside;
        public int militaryItemsCount; //turrets included
        public float totalItemsCost;
        public float haulableItemsCost;
        public int wallLength;
        public int roomsCount;
        public int internalArea;
        public int defensiveItemsCount; //turrets
        public int mannableCount;
        public int productionItemsCount;
        public int bedsCount;
        public int bedsBeforeTechLimit; //a settlement stays one when the tech level cap takes its beds (beds are medieval)

        // Buildings except walls, doors, fences and conduits: what the owners built, so what they could research.
        // Haulables say little about the era, they are traded and looted.
        public int buildingsCount;
        public int aboveMedievalBuildingsCount;
        public int highTechBuildingsCount; //spacer and above, except furniture and recreation
        public int researchBenchesCount;
        public int researchFacilitiesCount;
        public int generatorsCount;
        public int batteriesCount;
        public float powerOutput;
        public int commsCount;

        // Fortification: enclosed unroofed area (a courtyard), and the walls between it and the outside (up to 2 cells thick)
        public int enclosedOpenArea;
        public int perimeterWallLength;
        public float perimeterWallStrength; //sum of DefRoles.WallStrength over the perimeter walls

        public override string ToString() {
            string message = "Analyzer result:\r\n";

            FieldInfo[] fields = GetType().GetFields();

            foreach (FieldInfo fi in fields) {
                message += (fi.Name + ": " + fi.GetValue(this) + "\r\n");
            }

            return message;
        }

        public static int CountBeds(Blueprint blueprint) {
            int beds = 0;
            for (int z = 0; z < blueprint.height; z++) {
                for (int x = 0; x < blueprint.width; x++) {
                    List<ItemTile> tiles = blueprint.itemsMap[x, z];
                    if (tiles == null) continue;
                    foreach (ItemTile itemTile in tiles) {
                        ThingDef def = DefDatabase<ThingDef>.GetNamed(itemTile.defName, false);
                        if (def != null && DefRoles.Of(def).bed) beds++;
                    }
                }
            }
            return beds;
        }

        public static BlueprintAnalyzerResult Collect(Blueprint blueprint) {
            BlueprintAnalyzerResult result = new BlueprintAnalyzerResult();
            result.totalArea = blueprint.width * blueprint.height;
            result.roomsCount = blueprint.roomAreas.Count() - 2;

            result.internalArea = 0;
            for (int index = 2; index < blueprint.roomAreas.Count; index++) {
                result.internalArea += blueprint.roomAreas[index];
            }

            for (int z = 0; z < blueprint.height; z++) {
                for (int x = 0; x < blueprint.width; x++) {
                    List<ItemTile> tiles = blueprint.itemsMap[x, z];
                    if (tiles != null) {
                        foreach (ItemTile itemTile in tiles) {
                            result.AddTile(blueprint, itemTile, x, z);
                        }
                    }
                    if (blueprint.wallMap[x, z] >= 2 && !blueprint.roofMap[x, z]) {
                        result.enclosedOpenArea++;
                    }
                }
            }
            return result;
        }

        private void AddTile(Blueprint blueprint, ItemTile itemTile, int x, int z) {
            ThingDef itemDef = DefDatabase<ThingDef>.GetNamed(itemTile.defName, false);
            if (itemDef == null) return;
            DefRole role = DefRoles.Of(itemDef);

            totalItemsCost += itemTile.cost;
            occupiedTilesCount++;
            itemsCount += itemTile.stackCount;

            if (itemTile.isWall) {
                wallLength++;
                if (IsPerimeterWall(blueprint, x, z)) {
                    perimeterWallLength++;
                    ThingDef stuff = itemTile.stuffDef != null ? DefDatabase<ThingDef>.GetNamed(itemTile.stuffDef, false) : null;
                    perimeterWallStrength += DefRoles.WallStrength(itemDef, stuff);
                }
            } else if (role.isBuilding) {
                AddBuilding(itemTile, role);
            }

            if (itemDef.alwaysHaulable) {
                haulableStacksCount++;
                haulableItemsCount += itemTile.stackCount;
                haulableItemsCost += itemTile.cost;
            }

            if (role.military) militaryItemsCount++;
            if (role.turret) defensiveItemsCount++;
            if (role.mannable) mannableCount++;
            if (role.worktable) productionItemsCount++;
            if (role.bed) bedsCount++;

            if (blueprint.wallMap[x, z] > 1 || blueprint.roofMap[x, z] == true) {
                itemsInside++;
            }
        }

        private void AddBuilding(ItemTile itemTile, DefRole role) {
            TechLevel level = role.techLevel;
            if (itemTile.stuffDef != null) {
                TechLevel stuffLevel = TechLevelFilter.TechLevelOf(DefDatabase<ThingDef>.GetNamed(itemTile.stuffDef, false));
                if (stuffLevel > level) level = stuffLevel;
            }

            buildingsCount++;
            if (level > TechLevel.Medieval) aboveMedievalBuildingsCount++;
            if (level >= TechLevel.Spacer && !role.amenity) highTechBuildingsCount++;
            if (role.researchBench) researchBenchesCount++;
            if (role.researchFacility) researchFacilitiesCount++;
            if (role.powerOutput > 0) {
                generatorsCount++;
                powerOutput += role.powerOutput;
            }
            if (role.battery) batteriesCount++;
            if (role.comms) commsCount++;
        }

        // A wall with the outside (room 1) next to it and a courtyard cell (enclosed, unroofed) within 2 cells: the outer
        // layer of a compound wall. Walls of buildings standing in the open have roofed rooms behind them instead.
        private static bool IsPerimeterWall(Blueprint blueprint, int x, int z) {
            bool outside = (x > 0 && blueprint.wallMap[x - 1, z] == 1)
                || (x < blueprint.width - 1 && blueprint.wallMap[x + 1, z] == 1)
                || (z > 0 && blueprint.wallMap[x, z - 1] == 1)
                || (z < blueprint.height - 1 && blueprint.wallMap[x, z + 1] == 1);
            if (!outside) return false;
            for (int dx = -2; dx <= 2; dx++) {
                for (int dz = -2; dz <= 2; dz++) {
                    int cx = x + dx, cz = z + dz;
                    if (cx < 0 || cz < 0 || cx >= blueprint.width || cz >= blueprint.height) continue;
                    if (blueprint.wallMap[cx, cz] >= 2 && !blueprint.roofMap[cx, cz]) return true;
                }
            }
            return false;
        }
    }
}
