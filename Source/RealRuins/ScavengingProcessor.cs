using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace RealRuins;

internal class ScavengingProcessor
{
	private List<Tile> tilesByCost = new List<Tile>();

	private ScatterOptions options;

	private Blueprint blueprint;

	private float totalCost = 0f;

	public void RaidAndScavenge(Blueprint blueprint, ScatterOptions options)
	{
		this.options = options;
		this.blueprint = blueprint;
		Debug.active = false;
		float num = Rand.Value * options.scavengingMultiplier + options.scavengingMultiplier / 3f;
		float num2 = -blueprint.dateShift;
		int num3 = 0;
		int num4 = 0;
		int num5 = 0;
		int num6 = 0;
		int num7 = 0;
		for (int i = 0; i < blueprint.width; i++)
		{
			for (int j = 0; j < blueprint.height; j++)
			{
				if (blueprint.terrainMap[i, j] != null)
				{
					tilesByCost.Add(blueprint.terrainMap[i, j]);
					totalCost += blueprint.terrainMap[i, j].cost;
					num7++;
				}
				foreach (ItemTile item in blueprint.itemsMap[i, j])
				{
					tilesByCost.Add(item);
					totalCost += item.cost;
					num6++;
				}
			}
		}
		tilesByCost.Sort((Tile t1, Tile t2) => (t1.cost / t1.weight).CompareTo(t2.cost / t2.weight));
		int num8 = blueprint.width * blueprint.height;
		int num9 = (int)(Math.Log(num2 / 10f + 1f) * (double)num);
		if (num9 > 50)
		{
			num9 = 50;
		}
		if (options.scavengingMultiplier > 0.9f && num9 <= 0)
		{
			num9 = 1;
		}
		float num10 = (float)(num8 / 10) * num;
		Debug.Log("BlueprintTransfer", "Performing {0} raids. Base capacity: {1}", num9, num10);
		for (int k = 0; k < num9; k++)
		{
			float num11 = num10 * (float)Math.Pow(1.1, k);
			bool flag = false;
			Debug.Log("BlueprintTransfer", "Performing raid {0} of capacity {1}", k, num11);
			while (tilesByCost.Count > 0 && num11 > 0f && !flag)
			{
				Tile tile = tilesByCost.Pop();
				string text = $"Inspecting tile \"{tile.defName}\" of cost {tile.cost} and weight {tile.weight}. ";
				if (tile.cost / tile.weight < 7f)
				{
					flag = true;
					text += "Too cheap, stopping.";
				}
				else if (Rand.Chance(0.999f))
				{
					num11 -= tile.weight;
					if (tile is TerrainTile)
					{
						blueprint.terrainMap[tile.location.x, tile.location.z] = null;
						num4++;
						totalCost -= tile.cost;
						text += "Terrain removed.";
					}
					else if (tile is ItemTile)
					{
						ItemTile itemTile = tile as ItemTile;
						totalCost -= itemTile.cost;
						blueprint.itemsMap[tile.location.x, tile.location.z].Remove(itemTile);
						if (itemTile.isDoor)
						{
							if (Rand.Chance(0.8f))
							{
								ItemTile itemTile2 = ItemTile.DefaultDoorItemTile(itemTile.location);
								blueprint.itemsMap[tile.location.x, tile.location.z].Add(itemTile2);
								text = text + "Added " + itemTile2.defName + ", original ";
								num5++;
							}
							else
							{
								num3++;
								blueprint.RemoveWall(itemTile.location.x, itemTile.location.z);
							}
						}
						else if (itemTile.isWall)
						{
							ItemTile itemTile3 = ItemTile.WallReplacementItemTile(itemTile.location);
							blueprint.itemsMap[tile.location.x, tile.location.z].Add(itemTile3);
							text = text + "Added " + itemTile3.defName + ", original ";
							num5++;
						}
						else
						{
							num3++;
						}
						text += "Tile removed.";
					}
				}
				Debug.Log("BlueprintTransfer", text);
				if (flag)
				{
					break;
				}
			}
			if (flag)
			{
				break;
			}
		}
		for (int l = 1; l < blueprint.height - 1; l++)
		{
			for (int m = 1; m < blueprint.width - 1; m++)
			{
				ItemTile itemTile4 = null;
				foreach (ItemTile item2 in blueprint.itemsMap[m, l])
				{
					if (item2.isDoor && (!HasWallsIn(blueprint.itemsMap[m - 1, l]) || !HasWallsIn(blueprint.itemsMap[m + 1, l])) && (!HasWallsIn(blueprint.itemsMap[m, l - 1]) || !HasWallsIn(blueprint.itemsMap[m, l + 1])))
					{
						itemTile4 = item2;
						break;
					}
				}
				if (itemTile4 != null)
				{
					blueprint.itemsMap[m, l].Remove(itemTile4);
					totalCost -= itemTile4.cost;
					blueprint.RemoveWall(m, l);
				}
			}
		}
		Debug.active = true;
		if (options.costCap > 0)
		{
			LimitCostToCap();
		}
		static bool HasWallsIn(List<ItemTile> list)
		{
			foreach (ItemTile item3 in list)
			{
				if (item3.isWall)
				{
					return true;
				}
			}
			return false;
		}
	}

	private void LimitCostToCap()
	{
		Debug.Log("BlueprintTransfer", "Capping current cost of {0} to target cost of {1}", totalCost, options.costCap);
		int count = tilesByCost.Count;
		List<Tile> list = tilesByCost.Where((Tile item) => item.cost > 20f).ToList();
		float filteredCost = 0f;
		list.ForEach(delegate(Tile tile)
		{
			filteredCost += tile.cost;
		});
		if (list.Count == 0 || totalCost - filteredCost > (float)options.costCap)
		{
			list = tilesByCost;
		}
		Debug.Log("BlueprintTransfer", "TilesByCost: {0} items, filteredTiles: {1} items of cost {2}", tilesByCost.Count, list.Count, filteredCost);
		while (list.Count > 0 && totalCost > (float)options.costCap)
		{
			Tile tile2 = list.RandomElement();
			totalCost -= tile2.cost;
			list.Remove(tile2);
			if (tile2 is ItemTile)
			{
				ItemTile itemTile = tile2 as ItemTile;
				if (itemTile.isWall && !itemTile.isDoor)
				{
					itemTile.defName = "Wall";
					itemTile.stuffDef = "Wood";
				}
				else if (itemTile.isDoor)
				{
					itemTile.defName = "Door";
					itemTile.stuffDef = "Wood";
				}
				else
				{
					blueprint.itemsMap[tile2.location.x, tile2.location.z].Remove(tile2 as ItemTile);
				}
			}
			if (tile2 is TerrainTile)
			{
				blueprint.terrainMap[tile2.location.x, tile2.location.z] = null;
			}
		}
		Debug.Log("BlueprintTransfer", "Done. Resulting cost is {0}. Items left: {1}/{2}", totalCost, tilesByCost.Count, count);
	}
}
