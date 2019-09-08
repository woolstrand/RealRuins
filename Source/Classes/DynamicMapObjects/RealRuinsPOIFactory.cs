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
        public static bool CreatePOI(PlanetTileInfo tileInfo, string gameName) {

            if (tileInfo.tile >= Find.WorldGrid.TilesCount) {
                return false;
            }

            if (!TileFinder.IsValidTileForNewSettlement(tileInfo.tile)) {
                return false;
            }

            string filename = SnapshotStoreManager.Instance.SnapshotNameFor(tileInfo.mapId, gameName);
            Blueprint bp = BlueprintLoader.LoadWholeBlueprintAtPath(filename);
            BlueprintAnalyzer ba = new BlueprintAnalyzer(bp);
            ba.Analyze();
            var poiType = ba.determinedType;

            Faction faction = null;
            if (Rand.Chance(ba.chanceOfHavingFaction())) {
                Find.FactionManager.TryGetRandomNonColonyHumanlikeFaction(out faction, false, false);
            }

            RealRuinsPOIWorldObject site = CreateWorldObject(tileInfo.tile, faction);

            RealRuinsPOIComp comp = site.GetComponent<RealRuinsPOIComp>();
            if (comp == null) {
                Debug.Message("Component is null!");
            } else {
                comp.blueprintName = tileInfo.mapId;
                comp.gameName = gameName;
                comp.originX = tileInfo.originX;
                comp.originZ = tileInfo.originZ;
                comp.poiType = (int)poiType;
                comp.militaryPower = ba.militaryPower;
                comp.approximateSnapshotCost = ba.result.totalItemsCost;
                comp.bedsCount = ba.result.bedsCount;
            }
            if (site == null) return false;
 
            return true;
        }

        static RealRuinsPOIWorldObject CreateWorldObject(int tile, Faction siteFaction) {

            Debug.Message("Creating site at tile: {0}", tile);
            RealRuinsPOIWorldObject site = (RealRuinsPOIWorldObject)WorldObjectMaker.MakeWorldObject(DefDatabase<WorldObjectDef>.GetNamed("RealRuinsPOI"));
            site.Tile = tile;
            site.SetFaction(siteFaction);

            Find.WorldObjects.Add(site);
            return site;
        }

    }
}
