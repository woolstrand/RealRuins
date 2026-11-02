using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

using RimWorld;
using Verse;
using RimWorld.Planet;

namespace RealRuins
{
    class RealRuinsPOIFactory
    {
        // Blueprint loaded and analyzed off the main thread, waiting to be turned into a world object.
        public class PreparedPOI
        {
            public PlanetTileInfo tileInfo;
            public Blueprint blueprint;
            public BlueprintAnalyzer analyzer;
        }

        // Main thread only.
        public static bool IsSuitableTile(PlanetTileInfo tileInfo, bool biomeStrict)
        {
            if (tileInfo.tile >= Find.WorldGrid.TilesCount)
            {
                Debug.Log(Debug.POI, "[3] Skipped: Tile {0} was not found in world (among {1} tiles)", tileInfo.tile, Find.WorldGrid.TilesCount);
                return false;
            }

            if (!TileFinder.IsValidTileForNewSettlement(tileInfo.tile))
            {
                Debug.Log(Debug.POI, "[3] Skipped: Tile {0} is not valid for a new settlement.", tileInfo.tile);
                return false;
            }

            if (biomeStrict && tileInfo.biomeName != Find.WorldGrid.Surface.Tiles[tileInfo.tile].PrimaryBiome.defName)
            {
                Debug.Log(Debug.POI, "[3] Skipped: Filtered by biome (biome filter is ON)");
                return false;
            }

            return true;
        }

        // Safe to call from a background thread: touches only the blueprint file and defs.
        public static PreparedPOI PreparePOI(PlanetTileInfo tileInfo, string gameName, IntVec3 initialMapSize, bool costStrict, bool aggressiveDiscard)
        {
            string filename = SnapshotStoreManager.SnapshotNameFor(tileInfo.mapId, gameName);
            Blueprint bp = BlueprintLoader.LoadWholeBlueprintAtPath(filename);
            if (bp == null)
            {
                Debug.Log(Debug.POI, "[3] Skipped: Blueprint loader failed.");
                return null;
            }

            if (bp.originX + bp.width > initialMapSize.x || bp.originZ + bp.height > initialMapSize.z)
            {
                Debug.Log(Debug.POI, "[3] Skipped: Blueprint doesn't fit onto target map ({0} + {1} > {2} && {3} + {4} > {5})", bp.originX, bp.width, initialMapSize.x, bp.originZ, bp.height, initialMapSize.z);
                return null;
            }

            BlueprintAnalyzer ba = new BlueprintAnalyzer(bp);
            ba.Analyze();
            if (aggressiveDiscard && ba.determinedType == POIType.Ruins)
            {
                Debug.Log(Debug.POI, "[3] Skipped: Aggressive discard is ON and POI is ruins");
                return null;
            }
            if (costStrict && (ba.result.totalItemsCost < 1000))
            {
                Debug.Log(Debug.POI, "[3] Skipped: Low total cost or tiles count (cost/size filtering is ON)");
                return null;
            }

            if (ba.result.occupiedTilesCount < 50 || ba.result.totalArea < 200)
            {
                Debug.Log(Debug.POI, "[3] Skipped: Low area ({0}) and/or items count ({1}). (filtering not related to cost/size setting ON or OFF)", ba.result.totalArea, ba.result.occupiedTilesCount);
                return null;
            }

            return new PreparedPOI { tileInfo = tileInfo, blueprint = bp, analyzer = ba };
        }

        // Main thread only. Tile is re-checked because POIs committed earlier may have taken it or its neighbourhood.
        public static bool CommitPOI(PreparedPOI prepared, string gameName, bool biomeStrict, int abandonedChance = 25, bool disableFriendlyLocations = false)
        {
            PlanetTileInfo tileInfo = prepared.tileInfo;
            Blueprint bp = prepared.blueprint;
            BlueprintAnalyzer ba = prepared.analyzer;

            if (!IsSuitableTile(tileInfo, biomeStrict))
            {
                return false;
            }

            var poiType = ba.determinedType;

            Faction faction = null;
            bool baseChance = Rand.Chance(ba.chanceOfHavingFaction());
            if ((100 - abandonedChance) > 90)
            {
                // in case of high probabilities to have a faction we use abandonedness based purely on input parameter
                baseChance = Rand.Chance((float)(100 - abandonedChance) / 100);
            }
            else
            {
                // otherwise use both POI type based and parametric input.
                baseChance = baseChance & Rand.Chance((float)(100 - abandonedChance) / 100);
            }


            if (baseChance)
            {
                Find.FactionManager.TryGetRandomNonColonyHumanlikeFaction(out faction, false, false, minTechLevel: MinTechLevelForPOIType(poiType), requireHostile: disableFriendlyLocations);
            }

            RealRuinsPOIWorldObject site = TryCreateWorldObject(tileInfo.tile, faction, poiType == POIType.Ruins);
            if (site == null)
            {
                Debug.Log(Debug.POI, "[3] Skipped: Could not create world object.");
                return false;
            }

            RealRuinsPOIComp comp = site.GetComponent<RealRuinsPOIComp>();
            if (comp == null)
            {
                Debug.Error(Debug.BlueprintTransfer, "[3] POI Component is null!");
                return false;
            }
            else
            {
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

        static RealRuinsPOIWorldObject TryCreateWorldObject(int tile, Faction siteFaction, bool unlisted)
        {

            Debug.Log("Creating site at tile: {0}", tile);
            if (Find.WorldObjects.AnyWorldObjectAt(tile))
            {
                return null;
            }

            RealRuinsPOIWorldObject site = null;
            if (unlisted)
            {
                site = (RealRuinsPOIWorldObject)WorldObjectMaker.MakeWorldObject(DefDatabase<WorldObjectDef>.GetNamed("RealRuinsPOI_Unlisted"));
            }
            else
            {
                site = (RealRuinsPOIWorldObject)WorldObjectMaker.MakeWorldObject(DefDatabase<WorldObjectDef>.GetNamed("RealRuinsPOI"));
            }
            site.Tile = tile;
            site.SetFaction(siteFaction);

            Find.WorldObjects.Add(site);
            return site;
        }

        static TechLevel MinTechLevelForPOIType(POIType poiType)
        {
            switch (poiType)
            {
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
