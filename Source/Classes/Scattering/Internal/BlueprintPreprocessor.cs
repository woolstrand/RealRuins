using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace RealRuins;

internal class BlueprintPreprocessor
{
	public static void ProcessBlueprint(Blueprint blueprint, ScatterOptions options)
	{
		if (blueprint == null)
		{
			return;
		}
		for (int i = 0; i < blueprint.width; i++)
		{
			for (int j = 0; j < blueprint.height; j++)
			{
				List<ItemTile> list = blueprint.itemsMap[i, j];
				TerrainTile terrainTile = blueprint.terrainMap[i, j];
				TerrainDef terrainDef = null;
				if (terrainTile != null)
				{
					terrainDef = DefDatabase<TerrainDef>.GetNamed(terrainTile.defName, errorOnFail: false);
					if (terrainDef == null)
					{
						blueprint.terrainMap[i, j] = null;
						terrainTile = null;
					}
				}
				List<ItemTile> list2 = new List<ItemTile>();
				if (list == null)
				{
					continue;
				}
				foreach (ItemTile item in list)
				{
					if (item.defName == "Corpse")
					{
						continue;
					}
					ThingDef named = DefDatabase<ThingDef>.GetNamed(item.defName, errorOnFail: false);
					if (named == null)
					{
						list2.Add(item);
					}
					else if (named == ThingDefOf.Campfire || named == ThingDefOf.TorchLamp)
					{
						list2.Add(item);
					}
					else if (options.wallsDoorsOnly && !named.IsDoor && !item.defName.ToLower().Contains("wall"))
					{
						list2.Add(item);
					}
					else if (options.disableSpawnItems && named.EverHaulable)
					{
						list2.Add(item);
					}
					else if (named.defName.Contains("Animal") || named.defName.Contains("Spot"))
					{
						list2.Add(item);
					}
					else if (named.IsCorpse || named.Equals(ThingDefOf.MinifiedThing))
					{
						List<ItemTile> innerItems = item.innerItems;
						if ((innerItems == null || innerItems.Count() == 0) && item.itemXml == null)
						{
							list2.Add(item);
						}
					}
				}
				foreach (ItemTile item2 in list2)
				{
					if (item2.isWall || item2.isDoor)
					{
						blueprint.RemoveWall(item2.location.x, item2.location.z);
					}
					list.Remove(item2);
				}
			}
		}
		blueprint.UpdateBlueprintStats(includeCost: true);
		for (int k = 0; k < blueprint.width; k++)
		{
			for (int l = 0; l < blueprint.height; l++)
			{
				List<ItemTile> list3 = blueprint.itemsMap[k, l];
				TerrainTile terrainTile2 = blueprint.terrainMap[k, l];
				List<ItemTile> list4 = new List<ItemTile>();
				if (terrainTile2 != null && terrainTile2.cost > (float)options.itemCostLimit)
				{
					blueprint.terrainMap[k, l] = null;
				}
				if (list3 == null)
				{
					continue;
				}
				foreach (ItemTile item3 in list3)
				{
					if (options.itemCostLimit > 0 && options.itemCostLimit < 1000 && item3.cost > (float)options.itemCostLimit)
					{
						list4.Add(item3);
					}
				}
				foreach (ItemTile item4 in list4)
				{
					if (item4.isWall || item4.isDoor)
					{
						blueprint.RemoveWall(item4.location.x, item4.location.z);
					}
					list3.Remove(item4);
				}
			}
		}
	}
}
