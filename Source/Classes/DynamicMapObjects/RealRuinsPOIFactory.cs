using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

using RimWorld;
using Verse;
using RimWorld.Planet;

namespace RealRuins {
    class RealRuinsPOIFactory {
        public static bool CreatePOI(PlanetTileInfo tileInfo, string gameName, bool biomeStrict, bool costStrict, bool itemsStrict, int abandonedChance = 25, bool aggressiveDiscard = false) {

            if (tileInfo.tile >= Find.WorldGrid.TilesCount) {
                Debug.Log(Debug.POI, "[3] Skipped: Tile {0} was not found in world (among {1} tiles)", tileInfo.tile, Find.WorldGrid.TilesCount);
                return false;
            }

            if (!TileFinder.IsValidTileForNewSettlement(tileInfo.tile)) {
                Debug.Log(Debug.POI, "[3] Skipped: Tile {0} is not valid for a new settlement.", tileInfo.tile);
                return false;
            }

            if (biomeStrict && tileInfo.biomeName != Find.WorldGrid.tiles[tileInfo.tile].biome.defName) {
                Debug.Log(Debug.POI, "[3] Skipped: Filtered by biome (biome filter is ON)");
                return false;
            }

            string filename = SnapshotStoreManager.SnapshotNameFor(tileInfo.mapId, gameName);
            Blueprint bp = BlueprintLoader.LoadWholeBlueprintAtPath(filename);
            if (bp == null) {
                Debug.Log(Debug.POI, "[3] Skipped: Blueprint loader failed.");
                return false;
            }

            if (bp.originX + bp.width > Find.World.info.initialMapSize.x || bp.originZ + bp.height > Find.World.info.initialMapSize.z) {
                Debug.Log(Debug.POI, "2");
                Debug.Log(Debug.POI, "[3] Skipped: Blueprint doesn't fit onto target map ({0} + {1} > {2} && {3} + {4} > {5})", bp.originX, bp.width, Find.World.info.initialMapSize.x, bp.originZ, bp.height, Find.World.info.initialMapSize.z);
                return false;
            }

            BlueprintAnalyzer ba = new BlueprintAnalyzer(bp);
            ba.Analyze();
            if (aggressiveDiscard && ba.determinedType == POIType.Ruins) {
                Debug.Log(Debug.POI, "[3] Skipped: Aggressive discard is ON and POI is ruins");
                return false;
            }
            if (costStrict && (ba.result.totalItemsCost < 1000)) {
                Debug.Log(Debug.POI, "[3] Skipped: Low total cost or tiles count (cost/size filtering is ON)");
                return false;
            }

            if (ba.result.occupiedTilesCount < 50 || ba.result.totalArea < 200) {
                Debug.Log(Debug.POI, "[3] Skipped: Low area ({0}) and/or items count ({1}). (filtering not related to cost/size setting ON or OFF)", ba.result.totalArea, ba.result.occupiedTilesCount);
                return false;
            }

            var poiType = ba.determinedType;

            Faction faction = null;
            bool baseChance = Rand.Chance(ba.chanceOfHavingFaction());
            if ((100 - abandonedChance) > 90) {
                // in case of high probabilities to have a faction we use abandonedness based purely on input parameter
                baseChance = Rand.Chance((float)(100 - abandonedChance) / 100);
            } else {
                // otherwise use both POI type based and parametric input.
                baseChance = baseChance & Rand.Chance((float)(100 - abandonedChance) / 100);
            }

                            
            if (baseChance) {
                Find.FactionManager.TryGetRandomNonColonyHumanlikeFaction(out faction, false, false, minTechLevel: MinTechLevelForPOIType(poiType));
            }

            RealRuinsPOIWorldObject site = TryCreateWorldObject(tileInfo.tile, faction, poiType == POIType.Ruins);
            if (site == null) {
                Debug.Log(Debug.POI, "[3] Skipped: Could not create world object.");
                return false;
            }

            RealRuinsPOIComp comp = site.GetComponent<RealRuinsPOIComp>();
            if (comp == null) {
                Debug.Error(Debug.BlueprintTransfer, "[3] POI Component is null!");
                return false;
            } else {
                comp.blueprintName = tileInfo.mapId;
                comp.gameName = gameName;
                comp.originX = bp.originX;
                comp.originZ = bp.originZ;
                comp.poiType = (int)poiType;
                comp.militaryPower = ba.militaryPower;
                comp.mannableCount = ba.mannableCount;
                comp.approximateSnapshotCost = ba.result.totalItemsCost;
                comp.bedsCount = ba.result.bedsCount;
            }
 
            return true;
        }

        static RealRuinsPOIWorldObject TryCreateWorldObject(int tile, Faction siteFaction, bool unlisted) {

            Debug.Log("Creating site at tile: {0}", tile);
            if (Find.WorldObjects.AnyWorldObjectAt(tile)) {
                return null;
            }

            RealRuinsPOIWorldObject site = null;
            if (unlisted) {
                site = (RealRuinsPOIWorldObject)WorldObjectMaker.MakeWorldObject(DefDatabase<WorldObjectDef>.GetNamed("RealRuinsPOI_Unlisted"));
            } else {
                site = (RealRuinsPOIWorldObject)WorldObjectMaker.MakeWorldObject(DefDatabase<WorldObjectDef>.GetNamed("RealRuinsPOI"));
            }
            site.Tile = tile;
            site.SetFaction(siteFaction);

            Find.WorldObjects.Add(site);
            return site;
        }

        static TechLevel MinTechLevelForPOIType(POIType poiType) {
            switch (poiType) {
                case POIType.Camp:
                    return TechLevel.Neolithic;
                case POIType.Outpost:
                case POIType.Storage:
                    return TechLevel.Medieval;
                case POIType.Factory:
                case POIType.Research:
                case POIType.City:
                case POIType.Communication:
                case POIType.Stronghold:
                case POIType.MilitaryBaseLarge:
                case POIType.MilitaryBaseSmall:
                    return TechLevel.Industrial;
                default:
                    return TechLevel.Undefined;
            }
        }

    }
}
