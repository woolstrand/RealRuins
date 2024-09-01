using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RealRuins;

internal class RealRuinsPOIFactory
{
	public static bool CreatePOI(PlanetTileInfo tileInfo, string gameName, bool biomeStrict, bool costStrict, bool itemsStrict, int abandonedChance = 25, bool aggressiveDiscard = false)
	{
		if (tileInfo.tile >= Find.WorldGrid.TilesCount)
		{
			Debug.Log("POI", "[3] Skipped: Tile {0} was not found in world (among {1} tiles)", tileInfo.tile, Find.WorldGrid.TilesCount);
			return false;
		}
		if (!TileFinder.IsValidTileForNewSettlement(tileInfo.tile))
		{
			Debug.Log("POI", "[3] Skipped: Tile {0} is not valid for a new settlement.", tileInfo.tile);
			return false;
		}
		if (biomeStrict && tileInfo.biomeName != Find.WorldGrid.tiles[tileInfo.tile].biome.defName)
		{
			Debug.Log("POI", "[3] Skipped: Filtered by biome (biome filter is ON)");
			return false;
		}
		string path = SnapshotStoreManager.SnapshotNameFor(tileInfo.mapId, gameName);
		Blueprint blueprint = BlueprintLoader.LoadWholeBlueprintAtPath(path);
		if (blueprint == null)
		{
			Debug.Log("POI", "[3] Skipped: Blueprint loader failed.");
			return false;
		}
		if (blueprint.originX + blueprint.width > Find.World.info.initialMapSize.x || blueprint.originZ + blueprint.height > Find.World.info.initialMapSize.z)
		{
			Debug.Log("POI", "2");
			Debug.Log("POI", "[3] Skipped: Blueprint doesn't fit onto target map ({0} + {1} > {2} && {3} + {4} > {5})", blueprint.originX, blueprint.width, Find.World.info.initialMapSize.x, blueprint.originZ, blueprint.height, Find.World.info.initialMapSize.z);
			return false;
		}
		BlueprintAnalyzer blueprintAnalyzer = new BlueprintAnalyzer(blueprint);
		blueprintAnalyzer.Analyze();
		if (aggressiveDiscard && blueprintAnalyzer.determinedType == POIType.Ruins)
		{
			Debug.Log("POI", "[3] Skipped: Aggressive discard is ON and POI is ruins");
			return false;
		}
		if (costStrict && blueprintAnalyzer.result.totalItemsCost < 1000f)
		{
			Debug.Log("POI", "[3] Skipped: Low total cost or tiles count (cost/size filtering is ON)");
			return false;
		}
		if (blueprintAnalyzer.result.occupiedTilesCount < 50 || blueprintAnalyzer.result.totalArea < 200)
		{
			Debug.Log("POI", "[3] Skipped: Low area ({0}) and/or items count ({1}). (filtering not related to cost/size setting ON or OFF)", blueprintAnalyzer.result.totalArea, blueprintAnalyzer.result.occupiedTilesCount);
			return false;
		}
		POIType determinedType = blueprintAnalyzer.determinedType;
		Faction faction = null;
		bool flag = Rand.Chance(blueprintAnalyzer.chanceOfHavingFaction());
		if ((100 - abandonedChance <= 90) ? (flag & Rand.Chance((float)(100 - abandonedChance) / 100f)) : Rand.Chance((float)(100 - abandonedChance) / 100f))
		{
			Find.FactionManager.TryGetRandomNonColonyHumanlikeFaction(out faction, tryMedievalOrBetter: false, allowDefeated: false, MinTechLevelForPOIType(determinedType));
		}
		RealRuinsPOIWorldObject realRuinsPOIWorldObject = TryCreateWorldObject(tileInfo.tile, faction, determinedType == POIType.Ruins);
		if (realRuinsPOIWorldObject == null)
		{
			Debug.Log("POI", "[3] Skipped: Could not create world object.");
			return false;
		}
		RealRuinsPOIComp component = realRuinsPOIWorldObject.GetComponent<RealRuinsPOIComp>();
		if (component == null)
		{
			Debug.Error("BlueprintTransfer", "[3] POI Component is null!");
			return false;
		}
		component.blueprintName = tileInfo.mapId;
		component.gameName = gameName;
		component.originX = blueprint.originX;
		component.originZ = blueprint.originZ;
		component.poiType = (int)determinedType;
		component.militaryPower = blueprintAnalyzer.militaryPower;
		component.mannableCount = blueprintAnalyzer.mannableCount;
		component.approximateSnapshotCost = blueprintAnalyzer.result.totalItemsCost;
		component.bedsCount = blueprintAnalyzer.result.bedsCount;
		return true;
	}

	private static RealRuinsPOIWorldObject TryCreateWorldObject(int tile, Faction siteFaction, bool unlisted)
	{
		Debug.Log("Creating site at tile: {0}", tile);
		if (Find.WorldObjects.AnyWorldObjectAt(tile))
		{
			return null;
		}
		RealRuinsPOIWorldObject realRuinsPOIWorldObject = null;
		realRuinsPOIWorldObject = ((!unlisted) ? ((RealRuinsPOIWorldObject)WorldObjectMaker.MakeWorldObject(DefDatabase<WorldObjectDef>.GetNamed("RealRuinsPOI"))) : ((RealRuinsPOIWorldObject)WorldObjectMaker.MakeWorldObject(DefDatabase<WorldObjectDef>.GetNamed("RealRuinsPOI_Unlisted"))));
		realRuinsPOIWorldObject.Tile = tile;
		realRuinsPOIWorldObject.SetFaction(siteFaction);
		Find.WorldObjects.Add(realRuinsPOIWorldObject);
		return realRuinsPOIWorldObject;
	}

	private static TechLevel MinTechLevelForPOIType(POIType poiType)
	{
		switch (poiType)
		{
		case POIType.Camp:
			return TechLevel.Neolithic;
		case POIType.Outpost:
		case POIType.Storage:
			return TechLevel.Medieval;
		case POIType.MilitaryBaseSmall:
		case POIType.City:
		case POIType.Factory:
		case POIType.Research:
		case POIType.MilitaryBaseLarge:
		case POIType.Communication:
		case POIType.Stronghold:
			return TechLevel.Industrial;
		default:
			return TechLevel.Undefined;
		}
	}
}
