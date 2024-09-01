using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace RealRuins;

internal class Blueprint
{
	public readonly Version version;

	private int snapshotYearInt;

	public readonly int[,] wallMap;

	public readonly TerrainTile[,] terrainMap;

	public readonly bool[,] roofMap;

	public readonly List<ItemTile>[,] itemsMap;

	public int width { get; private set; }

	public int height { get; private set; }

	public int originX { get; private set; }

	public int originZ { get; private set; }

	public float totalCost { get; private set; }

	public float itemsDensity { get; private set; }

	public int itemsCount { get; private set; }

	public int terrainTilesCount { get; private set; }

	public int roomsCount { get; private set; }

	public List<int> roomAreas { get; private set; }

	public int snapshotYear
	{
		get
		{
			return snapshotYearInt;
		}
		set
		{
			snapshotYearInt = value;
			dateShift = -(value - 5500) - Rand.Range(5, 500);
		}
	}

	public int dateShift { get; private set; }

	public Blueprint(int originX, int originZ, int width, int height, Version version)
	{
		this.version = version;
		this.width = width;
		this.height = height;
		this.originX = originX;
		this.originZ = originZ;
		wallMap = new int[width, height];
		roofMap = new bool[width, height];
		itemsMap = new List<ItemTile>[width, height];
		terrainMap = new TerrainTile[width, height];
	}

	public void CutIfExceedsBounds(IntVec3 size)
	{
		if (width > size.x)
		{
			width = size.x;
		}
		if (height > size.z)
		{
			height = size.z;
		}
	}

	public void UpdateBlueprintStats(bool includeCost = false)
	{
		totalCost = 0f;
		terrainTilesCount = 0;
		itemsCount = 0;
		for (int i = 0; i < width; i++)
		{
			for (int j = 0; j < height; j++)
			{
				List<ItemTile> list = itemsMap[i, j];
				if (list == null)
				{
					continue;
				}
				foreach (ItemTile item in list)
				{
					ThingDef named = DefDatabase<ThingDef>.GetNamed(item.defName, errorOnFail: false);
					ThingDef stuffDef = ((item.stuffDef != null) ? DefDatabase<ThingDef>.GetNamed(item.stuffDef, errorOnFail: false) : null);
					if (named == null)
					{
						continue;
					}
					if (includeCost)
					{
						try
						{
							item.cost = named.ThingComponentsMarketCost(stuffDef) * (float)item.stackCount;
							totalCost += item.cost;
						}
						catch (Exception)
						{
						}
					}
					if (item.defName.Contains("Wall"))
					{
						item.weight = 5f;
					}
					item.weight = named.ThingWeight(stuffDef);
					if (item.stackCount != 0)
					{
						item.weight *= item.stackCount;
					}
					if (item.weight == 0f)
					{
						if (item.stackCount != 0)
						{
							item.weight = 0.5f * (float)item.stackCount;
						}
						else
						{
							item.weight = 1f;
						}
					}
					itemsCount++;
				}
				TerrainTile terrainTile = terrainMap[i, j];
				if (terrainTile == null)
				{
					continue;
				}
				TerrainDef named2 = DefDatabase<TerrainDef>.GetNamed(terrainTile.defName, errorOnFail: false);
				if (named2 != null && includeCost)
				{
					try
					{
						terrainTile.cost = named2.ThingComponentsMarketCost();
						totalCost += terrainTile.cost;
					}
					catch (Exception)
					{
					}
				}
				terrainTilesCount++;
			}
		}
		itemsDensity = (float)itemsCount / (float)(width * height);
	}

	public void MarkRoomAsOpenedAt(int posX, int posZ)
	{
		int num = wallMap[posX, posZ];
		if (num < 2)
		{
			return;
		}
		for (int i = 0; i < width; i++)
		{
			for (int j = 0; j < height; j++)
			{
				if (wallMap[i, j] == num)
				{
					wallMap[i, j] = -1;
				}
			}
		}
	}

	public void RemoveWall(int posX, int posZ)
	{
		if (posX < 0 || posZ < 0 || posX >= width || posZ >= height || wallMap[posX, posZ] != -1)
		{
			return;
		}
		int? num = null;
		if (posX == 0 || posX == width - 1 || posZ == 0 || posZ == height - 1)
		{
			num = 1;
		}
		List<int> list = new List<int>();
		if (posX > 0)
		{
			list.Add(wallMap[posX - 1, posZ]);
		}
		if (posX < width - 1)
		{
			list.Add(wallMap[posX + 1, posZ]);
		}
		if (posZ > 0)
		{
			list.Add(wallMap[posX, posZ - 1]);
		}
		if (posZ < height - 1)
		{
			list.Add(wallMap[posX, posZ + 1]);
		}
		list.RemoveAll((int room) => room == -1);
		List<int> list2 = list.Distinct().ToList();
		if (!num.HasValue && list2.Count > 0)
		{
			if (list2.Contains(1))
			{
				list2.Remove(1);
				num = 1;
			}
			else
			{
				num = list2.Pop();
			}
		}
		if (list2.Count <= 0)
		{
			return;
		}
		for (int i = 0; i < width; i++)
		{
			for (int j = 0; j < height; j++)
			{
				if (list2.Contains(wallMap[i, j]))
				{
					wallMap[i, j] = num ?? 1;
				}
			}
		}
	}

	public void FindRooms()
	{
		int currentRoomIndex = 1;
		roomAreas = new List<int> { 0 };
		for (int i = 0; i < height; i++)
		{
			wallMap[0, i] = 1;
			wallMap[width - 1, i] = 1;
		}
		for (int j = 0; j < width; j++)
		{
			wallMap[j, 0] = 1;
			wallMap[j, height - 1] = 1;
		}
		for (int k = 0; k < height; k++)
		{
			for (int l = 0; l < width; l++)
			{
				if (wallMap[l, k] == 0)
				{
					TraverseCells(new List<IntVec3>
					{
						new IntVec3(l, 0, k)
					});
					currentRoomIndex++;
				}
			}
		}
		roomsCount = currentRoomIndex;
		void TraverseCells(List<IntVec3> points)
		{
			int num = 0;
			List<IntVec3> list = new List<IntVec3>();
			foreach (IntVec3 point in points)
			{
				if (point.x >= 0 && point.z >= 0 && point.x < width && point.z < height && wallMap[point.x, point.z] == 0)
				{
					wallMap[point.x, point.z] = currentRoomIndex;
					num++;
					list.Add(new IntVec3(point.x - 1, 0, point.z));
					list.Add(new IntVec3(point.x + 1, 0, point.z));
					list.Add(new IntVec3(point.x, 0, point.z - 1));
					list.Add(new IntVec3(point.x, 0, point.z + 1));
				}
			}
			if (roomAreas.Count == currentRoomIndex)
			{
				roomAreas.Add(0);
			}
			roomAreas[currentRoomIndex] += num;
			if (list.Count > 0)
			{
				TraverseCells(list);
			}
		}
	}

	public Blueprint RandomPartCenteredAtRoom(IntVec3 size)
	{
		if (roomsCount == 0)
		{
			FindRooms();
		}
		if (roomsCount < 3)
		{
			return Part(new IntVec3(Rand.Range(size.x, width - size.x), 0, Rand.Range(size.z, height - size.z)), size);
		}
		int num = 0;
		num = Rand.Range(2, roomsCount);
		int num2 = width;
		int num3 = 0;
		int num4 = height;
		int num5 = 0;
		for (int i = 0; i < width; i++)
		{
			for (int j = 0; j < height; j++)
			{
				if (wallMap[i, j] == num)
				{
					if (i > num3)
					{
						num3 = i;
					}
					if (i < num2)
					{
						num2 = i;
					}
					if (j > num5)
					{
						num5 = j;
					}
					if (j < num4)
					{
						num4 = j;
					}
				}
			}
		}
		IntVec3 randomCell = new CellRect(num2, num4, num3 - num2, num5 - num4).RandomCell;
		return Part(randomCell, size);
	}

	public Blueprint Part(IntVec3 location, IntVec3 size)
	{
		if (width <= size.x && height <= size.z)
		{
			return this;
		}
		int x = location.x;
		int z = location.z;
		int num = Math.Max(0, x - size.x / 2);
		int num2 = Math.Min(width - 1, x + size.x / 2);
		int num3 = Math.Max(0, z - size.z / 2);
		int num4 = Math.Min(height - 1, z + size.z / 2);
		int num5 = 0;
		Blueprint blueprint = new Blueprint(originX, originZ, num2 - num, num4 - num3, version);
		for (int i = num3; i < num4; i++)
		{
			for (int j = num; j < num2; j++)
			{
				IntVec3 location2 = new IntVec3(j - num, 0, i - num3);
				blueprint.roofMap[j - num, i - num3] = roofMap[j, i];
				blueprint.terrainMap[j - num, i - num3] = terrainMap[j, i];
				blueprint.itemsMap[j - num, i - num3] = itemsMap[j, i];
				if (wallMap[j, i] == -1)
				{
					blueprint.wallMap[j - num, i - num3] = -1;
				}
				else
				{
					blueprint.wallMap[j - num, i - num3] = 0;
				}
				if (blueprint.itemsMap[j - num, i - num3] != null)
				{
					foreach (ItemTile item in blueprint.itemsMap[j - num, i - num3])
					{
						item.location = location2;
						num5++;
					}
				}
				if (blueprint.terrainMap[j - num, i - num3] != null)
				{
					blueprint.terrainMap[j - num, i - num3].location = location2;
				}
			}
		}
		blueprint.snapshotYear = snapshotYear;
		return blueprint;
	}
}
