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
        public static bool CreatePOI(PlanetTileInfo tileInfo, string gameName, bool biomeStrict, bool costStrict, bool itemsStrict) {

            if (tileInfo.tile >= Find.WorldGrid.TilesCount) {
                return false;
            }

            if (!TileFinder.IsValidTileForNewSettlement(tileInfo.tile)) {
                return false;
            }

            if (biomeStrict && tileInfo.biomeName != Find.WorldGrid.tiles[tileInfo.tile].biome.defName) {
                Debug.Log(Debug.POI, "Skipped blueprint due to wrong biome");
                return false;
            }

            string filename = SnapshotStoreManager.Instance.SnapshotNameFor(tileInfo.mapId, gameName);
            Blueprint bp = BlueprintLoader.LoadWholeBlueprintAtPath(filename);
            if (bp == null) {
                return false;
            }

            BlueprintAnalyzer ba = new BlueprintAnalyzer(bp);
            ba.Analyze();
            if (costStrict && (ba.result.totalItemsCost < 1000)) {
                Debug.Log(Debug.POI, "Skipped blueprint due to low total cost or tiles count");
                return false;
            }

            if (ba.result.occupiedTilesCount < 100 || ba.result.totalArea < 400) {
                Debug.Log(Debug.POI, "Skipped blueprint due to low area and items count");
                return false;
            }

            var poiType = ba.determinedType;

            Faction faction = null;
            if (Rand.Chance(ba.chanceOfHavingFaction())) {
                Find.FactionManager.TryGetRandomNonColonyHumanlikeFaction(out faction, false, false, minTechLevel: MinTechLevelForPOIType(poiType));
            }

            RealRuinsPOIWorldObject site = TryCreateWorldObject(tileInfo.tile, faction);
            if (site == null) return false;

            RealRuinsPOIComp comp = site.GetComponent<RealRuinsPOIComp>();
            if (comp == null) {
                Debug.Error(Debug.BlueprintTransfer, "POI Component is null!");
            } else {
                comp.blueprintName = tileInfo.mapId;
                comp.gameName = gameName;
                comp.originX = tileInfo.originX;
                comp.originZ = tileInfo.originZ;
                comp.poiType = (int)poiType;
                comp.militaryPower = ba.militaryPower;
                comp.mannableCount = ba.mannableCount;
                comp.approximateSnapshotCost = ba.result.totalItemsCost;
                comp.bedsCount = ba.result.bedsCount;
            }
 
            return true;
        }

        static RealRuinsPOIWorldObject TryCreateWorldObject(int tile, Faction siteFaction) {

            Debug.Log("Creating site at tile: {0}", tile);
            if (Find.WorldObjects.AnyWorldObjectAt(tile)) {
                return null;
            }

            RealRuinsPOIWorldObject site = (RealRuinsPOIWorldObject)WorldObjectMaker.MakeWorldObject(DefDatabase<WorldObjectDef>.GetNamed("RealRuinsPOI"));
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
