using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace RealRuins;

internal class BlueprintAnalyzer
{
	private Blueprint blueprint;

	public POIType determinedType;

	private float militaryFeatures;

	public float militaryPower;

	public int mannableCount;

	public int approximateDisplayValue;

	public List<string> randomMostValueableItemDefNames;

	public bool shouldSuppressLogging = true;

	private Dictionary<string, ItemStatRecord> itemStats;

	private ScatterOptions options;

	public BlueprintAnalyzerResult result { get; private set; }

	public BlueprintAnalyzer(Blueprint blueprint, ScatterOptions options = null)
	{
		this.blueprint = blueprint;
		this.options = options ?? ScatterOptions.asIs();
		itemStats = new Dictionary<string, ItemStatRecord>();
	}

	private void ProcessTile(ItemTile itemTile, IntVec3 pos)
	{
		ThingDef named = DefDatabase<ThingDef>.GetNamed(itemTile.defName, errorOnFail: false);
		if (named == null)
		{
			return;
		}
		ItemStatRecord itemStatRecord = null;
		if (itemStats.ContainsKey(itemTile.defName))
		{
			itemStatRecord = itemStats[itemTile.defName];
		}
		else
		{
			itemStatRecord = new ItemStatRecord();
			itemStats[itemTile.defName] = itemStatRecord;
		}
		itemStatRecord.stacksCount++;
		itemStatRecord.totalCount += itemTile.stackCount;
		itemStatRecord.cost += itemTile.cost;
		result.totalItemsCost += itemTile.cost;
		result.occupiedTilesCount++;
		result.itemsCount += itemTile.stackCount;
		if (itemTile.isWall)
		{
			result.wallLength++;
		}
		string text = itemTile.defName + " ";
		if (named.alwaysHaulable)
		{
			text += "is haulable ";
			result.haulableStacksCount++;
			result.haulableItemsCount += itemTile.stackCount;
			result.haulableItemsCost += itemTile.cost;
		}
		string text2 = itemTile.defName.ToLower();
		if (named.IsShell || named.IsRangedWeapon || text2.Contains("turret") || text2.Contains("cannon") || text2.Contains("gun"))
		{
			result.militaryItemsCount++;
			text += "military ";
			if (named.building != null)
			{
				text += "non-nul building ";
				result.defensiveItemsCount++;
				if (named.building.IsTurret)
				{
					text += "turret ";
				}
			}
		}
		if (named.HasComp(typeof(CompMannable)))
		{
			result.mannableCount++;
		}
		if (named.IsWorkTable)
		{
			text += "worktable ";
			result.productionItemsCount++;
		}
		if (named.IsBed)
		{
			text += "bed ";
			result.bedsCount++;
		}
		if (blueprint.wallMap[pos.x, pos.z] > 1 || blueprint.roofMap[pos.x, pos.z])
		{
			result.itemsInside++;
		}
		if (!itemTile.isWall && !itemTile.isDoor && !(itemTile.cost > 100f))
		{
		}
	}

	public void Analyze()
	{
		if (!shouldSuppressLogging)
		{
			Debug.Log("Analyzer", "analyzing blueprint {0} with options {1}", blueprint, options);
		}
		blueprint.FindRooms();
		if (!shouldSuppressLogging)
		{
			Debug.Log("Analyzer", "Rooms found");
		}
		BlueprintPreprocessor.ProcessBlueprint(blueprint, options);
		if (!shouldSuppressLogging)
		{
			Debug.Log("Analyzer", "Blueprint processed");
		}
		result = new BlueprintAnalyzerResult();
		result.totalArea = blueprint.width * blueprint.height;
		result.roomsCount = blueprint.roomAreas.Count() - 2;
		result.internalArea = 0;
		for (int i = 2; i < blueprint.roomAreas.Count; i++)
		{
			result.internalArea += blueprint.roomAreas[i];
		}
		blueprint.UpdateBlueprintStats(includeCost: true);
		if (!shouldSuppressLogging)
		{
			Debug.Log("Analyzer", "Analyzing map");
		}
		for (int j = 0; j < blueprint.height; j++)
		{
			for (int k = 0; k < blueprint.width; k++)
			{
				List<ItemTile> list = blueprint.itemsMap[k, j] ?? new List<ItemTile>();
				foreach (ItemTile item in list)
				{
					ProcessTile(item, new IntVec3(k, 0, j));
				}
			}
		}
		mannableCount = result.mannableCount;
		determinedType = supposedType();
		if (!shouldSuppressLogging)
		{
			Debug.Log("Analyzer", "Type is {0} by {1}", determinedType, result.ToString());
		}
	}

	private POIType supposedType()
	{
		float num = 0f;
		float num2 = 0f;
		militaryFeatures = 0f;
		militaryPower = 0f;
		if (result.wallLength < 70 || (result.totalItemsCost < 2000f && result.bedsCount < 1))
		{
			return POIType.Ruins;
		}
		if ((double)result.itemsInside < (double)(result.occupiedTilesCount - result.wallLength) * 0.3)
		{
			return POIType.Ruins;
		}
		if (result.internalArea / result.wallLength < 2 && result.roomsCount < 6)
		{
			return POIType.Ruins;
		}
		if (result.totalItemsCost / (float)result.occupiedTilesCount < 10f)
		{
			return POIType.Ruins;
		}
		militaryFeatures = (float)(result.militaryItemsCount + result.defensiveItemsCount * 10) * 25f / (float)result.internalArea;
		militaryPower = (float)(result.defensiveItemsCount * 250) / (float)result.internalArea + 1f;
		num = result.productionItemsCount * 10 + result.haulableStacksCount * 5 / result.internalArea;
		if (!shouldSuppressLogging)
		{
			Debug.Log("Analyzer", "military features: {0}. power: {1}. prod: {2}.", militaryFeatures, militaryPower, num);
		}
		if (militaryFeatures < 3f && num <= 50f)
		{
			if (result.internalArea < 2000 && result.bedsCount > 0 && result.totalItemsCost < 30000f)
			{
				return POIType.Camp;
			}
			if (result.internalArea >= 2000 && result.roomsCount > 12)
			{
				return POIType.City;
			}
			if ((float)(result.occupiedTilesCount - result.wallLength) / (float)result.haulableStacksCount < 3f || result.bedsCount < 3)
			{
				return POIType.Storage;
			}
			return POIType.Camp;
		}
		if (militaryFeatures >= 3f && militaryPower < 8f && num < 50f)
		{
			if (result.internalArea < 2000)
			{
				return Rand.Chance(0.5f) ? POIType.Outpost : POIType.Communication;
			}
			if (result.internalArea > 2000 && result.internalArea < 6000 && result.roomsCount < 20)
			{
				return POIType.MilitaryBaseSmall;
			}
			if (result.internalArea > 6000 || result.roomsCount > 20)
			{
				return POIType.City;
			}
		}
		if (militaryFeatures < 8f && num >= 50f)
		{
			if (result.haulableItemsCost / (float)result.haulableItemsCount > 30f)
			{
				return POIType.Research;
			}
			return POIType.Factory;
		}
		if (militaryFeatures >= 8f)
		{
			if (result.defensiveItemsCount > 15)
			{
				return POIType.Stronghold;
			}
			if (result.internalArea > 2500)
			{
				return POIType.MilitaryBaseLarge;
			}
			return POIType.MilitaryBaseSmall;
		}
		return POIType.Ruins;
	}

	public float chanceOfHavingFaction()
	{
		switch (determinedType)
		{
		case POIType.Ruins:
			return 0f;
		case POIType.Outpost:
		case POIType.Camp:
		case POIType.Storage:
			return 0.3f;
		case POIType.MilitaryBaseSmall:
		case POIType.Communication:
			return 0.7f;
		case POIType.City:
		case POIType.Factory:
		case POIType.Research:
		case POIType.PowerPlant:
		case POIType.MilitaryBaseLarge:
		case POIType.Stronghold:
			return 0.9f;
		default:
			return 0.5f;
		}
	}
}
