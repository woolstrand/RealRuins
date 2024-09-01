using System;
using System.Collections.Generic;
using Verse;

namespace RealRuins;

internal class DeteriorationProcessor
{
	private float[,] terrainIntegrity;

	private float[,] itemsIntegrity;

	private Blueprint blueprint;

	private ScatterOptions options;

	private void ConstructFallbackIntegrityMaps()
	{
		IntVec3 intVec = new IntVec3(blueprint.width / 2, 0, blueprint.height / 2);
		int num = Math.Min(blueprint.width / 2, blueprint.height / 2);
		if (num > 8)
		{
			num -= 4;
		}
		for (int i = 0; i < blueprint.width; i++)
		{
			for (int j = 0; j < blueprint.height; j++)
			{
				int num2 = (i - intVec.x) * (i - intVec.x) + (j - intVec.z) * (j - intVec.z);
				if (num2 < num / 2 * (num / 2))
				{
					terrainIntegrity[i, j] = Rand.Value * 0.4f + 0.8f;
					itemsIntegrity[i, j] = Rand.Value * 0.4f + 0.8f;
				}
				else if (num2 < num * num)
				{
					terrainIntegrity[i, j] = Rand.Value * 0.2f + 0.8f;
					itemsIntegrity[i, j] = Rand.Value * 0.2f + 0.7f;
				}
			}
		}
		terrainIntegrity.Blur(10);
		itemsIntegrity.Blur(7);
	}

	private void ConstructRoomBasedIntegrityMap()
	{
		if (blueprint.roomsCount == 1)
		{
			ConstructFallbackIntegrityMaps();
			return;
		}
		IntVec3 intVec = new IntVec3(blueprint.width / 2, 0, blueprint.height / 2);
		int num = Math.Min(intVec.x, intVec.z);
		List<int> list = new List<int>();
		List<int> list2 = new List<int>();
		for (int i = 0; i < blueprint.width; i++)
		{
			for (int j = 0; j < blueprint.height; j++)
			{
				int item = blueprint.wallMap[i, j];
				int num2 = (i - intVec.x) * (i - intVec.x) + (j - intVec.z) * (j - intVec.z);
				if (num2 < (num - 1) * (num - 1))
				{
					if (!list.Contains(item))
					{
						list.Add(item);
					}
				}
				else if (num2 < num * num)
				{
					if (!list.Contains(item))
					{
						list.Add(item);
					}
					if (!list2.Contains(item))
					{
						list2.Add(item);
					}
					blueprint.MarkRoomAsOpenedAt(i, j);
				}
			}
		}
		if (list.Count == list2.Count)
		{
			ConstructFallbackIntegrityMaps();
			return;
		}
		List<int> list3 = list.ListFullCopy();
		foreach (int item3 in list2)
		{
			list3.Remove(item3);
		}
		for (int k = 0; k < blueprint.width; k++)
		{
			for (int l = 0; l < blueprint.height; l++)
			{
				int item2 = blueprint.wallMap[k, l];
				if (list3.Contains(item2))
				{
					terrainIntegrity[k, l] = 20f;
					itemsIntegrity[k, l] = 1f;
				}
			}
		}
		for (int m = 1; m < blueprint.width - 1; m++)
		{
			for (int n = 1; n < blueprint.height - 1; n++)
			{
				if (terrainIntegrity[m, n] == 0f)
				{
					float num3 = terrainIntegrity[m + 1, n + 1] + terrainIntegrity[m, n + 1] + terrainIntegrity[m - 1, n + 1] + terrainIntegrity[m - 1, n] + terrainIntegrity[m + 1, n] + terrainIntegrity[m + 1, n - 1] + terrainIntegrity[m, n - 1] + terrainIntegrity[m - 1, n - 1];
					if (num3 >= 20f)
					{
						terrainIntegrity[m, n] = 1f;
						itemsIntegrity[m, n] = 1f;
					}
				}
			}
		}
		for (int num4 = 0; num4 < blueprint.width; num4++)
		{
			for (int num5 = 0; num5 < blueprint.height; num5++)
			{
				if (terrainIntegrity[num4, num5] > 1f)
				{
					terrainIntegrity[num4, num5] = 1f;
				}
			}
		}
		terrainIntegrity.Blur(7);
		itemsIntegrity.Blur(4);
	}

	private void ConstructUntouchedIntegrityMap()
	{
		for (int i = 0; i < blueprint.width; i++)
		{
			for (int j = 0; j < blueprint.height; j++)
			{
				terrainIntegrity[i, j] = 1f;
				itemsIntegrity[i, j] = 1f;
			}
		}
	}

	private void Deteriorate()
	{
		int num = 0;
		int num2 = 0;
		for (int i = 0; i < blueprint.width; i++)
		{
			for (int j = 0; j < blueprint.height; j++)
			{
				bool flag = false;
				bool flag2 = false;
				List<ItemTile> list = blueprint.itemsMap[i, j];
				num += list.Count;
				if (!Rand.Chance(terrainIntegrity[i, j]))
				{
					blueprint.terrainMap[i, j] = null;
				}
				if (blueprint.terrainMap[i, j] == null)
				{
					blueprint.roofMap[i, j] = false;
				}
				float num3 = itemsIntegrity[i, j] * (1f - options.deteriorationMultiplier);
				List<ItemTile> list2 = new List<ItemTile>();
				if (num3 > 0f && num3 < 1f)
				{
					blueprint.roofMap[i, j] = Rand.Chance(num3 * 0.3f);
					foreach (ItemTile item in list)
					{
						if (options.shouldKeepDefencesAndPower && item.defName.ToLower().Contains("conduit"))
						{
							list2.Add(item);
							continue;
						}
						if (item.isWall)
						{
							flag = true;
						}
						if (Rand.Chance(num3))
						{
							if (item.isWall)
							{
								flag2 = true;
							}
							list2.Add(item);
						}
						ThingDef namedSilentFail = DefDatabase<ThingDef>.GetNamedSilentFail(item.defName);
						if (namedSilentFail != null)
						{
							item.stackCount = Rand.Range(1, Math.Min(namedSilentFail.stackLimit, item.stackCount));
						}
					}
					num2 += list.Count - list2.Count;
				}
				if (num3 < 1f)
				{
					blueprint.itemsMap[i, j] = list2;
					if (num3 <= 0f)
					{
						blueprint.RemoveWall(i, j);
					}
				}
				if (flag && !flag2)
				{
					blueprint.RemoveWall(i, j);
				}
			}
		}
	}

	public static void Process(Blueprint source, ScatterOptions options)
	{
		if (options.enableDeterioration)
		{
			DeteriorationProcessor deteriorationProcessor = new DeteriorationProcessor();
			deteriorationProcessor.options = options;
			deteriorationProcessor.blueprint = source;
			deteriorationProcessor.itemsIntegrity = new float[source.width, source.height];
			deteriorationProcessor.terrainIntegrity = new float[source.width, source.height];
			deteriorationProcessor.ConstructRoomBasedIntegrityMap();
			deteriorationProcessor.Deteriorate();
		}
	}
}
