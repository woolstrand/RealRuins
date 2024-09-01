using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.BaseGen;
using RimWorld.Planet;
using Verse;

namespace RealRuins;

internal class GenStep_ScatterRealRuins : GenStep
{
	private ScatterOptions currentOptions = RealRuins_ModSettings.defaultScatterOptions;

	public override int SeedPart => 74293945;

	public float CalculateDistanceToNearestSettlement(Map map)
	{
		int tile2 = map.Tile;
		int proximityLimit = 16;
		int minDistance = proximityLimit;
		foreach (WorldObject item in Find.World.worldObjects.ObjectsAt(map.Tile))
		{
			if (item.Faction != Faction.OfPlayer && (item is Settlement || item is Site))
			{
				return 1f;
			}
		}
		Find.WorldFloodFiller.FloodFill(tile2, (int x) => !Find.World.Impassable(x), delegate(int tile, int traversalDistance)
		{
			if (traversalDistance > proximityLimit)
			{
				return true;
			}
			if (traversalDistance > 0)
			{
				foreach (WorldObject item2 in Find.World.worldObjects.ObjectsAt(tile))
				{
					if (item2.Faction != Faction.OfPlayer)
					{
						if (item2 is Settlement)
						{
							if (traversalDistance < minDistance)
							{
								minDistance = traversalDistance;
							}
						}
						else if (item2 is Site && traversalDistance * 2 < minDistance)
						{
							minDistance = traversalDistance * 2;
						}
					}
				}
			}
			return false;
		});
		return minDistance;
	}

	public override void Generate(Map map, GenStepParams parms)
	{
		if (RealRuins_ModSettings.preserveStandardRuins)
		{
			GenStep genStep = new GenStep_ScatterRuinsSimple();
			genStep.Generate(map, parms);
		}
		if (SnapshotStoreManager.Instance.StoredSnapshotsCount() < 10)
		{
			Debug.Error("Scatter", "Skipping ruins gerenation due to low blueprints count.");
			return;
		}
		if (RealRuins_ModSettings.startWithoutRuins)
		{
			int num = 0;
			List<Map> maps = Find.Maps;
			for (int i = 0; i < maps.Count; i++)
			{
				Map map2 = maps[i];
				if (map2.IsPlayerHome)
				{
					num++;
				}
			}
			if (num == 1 && Find.TickManager.TicksGame < 10)
			{
				return;
			}
		}
		foreach (WorldObject item in Find.World.worldObjects.ObjectsAt(map.Tile))
		{
			if (item is RealRuinsPOIWorldObject || item is AbandonedBaseWorldObject)
			{
				return;
			}
		}
		if (map.TileInfo.WaterCovered)
		{
			return;
		}
		float num2 = 1f;
		float num3 = 1f;
		float num4 = 0f;
		float num5 = RealRuins_ModSettings.defaultScatterOptions.densityMultiplier;
		currentOptions = RealRuins_ModSettings.defaultScatterOptions.Copy();
		if (RealRuins_ModSettings.defaultScatterOptions.enableProximity)
		{
			num4 = CalculateDistanceToNearestSettlement(map);
			if (num4 >= 16f && Rand.Chance(0.5f))
			{
				num5 = 0f;
			}
			if (num5 > 0f)
			{
				num2 = (float)(Math.Exp(1.0 / ((double)num4 / 10.0 + 0.3)) - 0.7);
				num3 = (float)(Math.Exp(1.0 / ((double)(num4 / 5f) + 0.5)) - 0.3);
			}
			else
			{
				num2 = 0f;
			}
			currentOptions.densityMultiplier *= num2;
			currentOptions.minRadius = Math.Min(60, Math.Max(6, (int)((float)currentOptions.minRadius * num3)));
			currentOptions.maxRadius = Math.Min(60, Math.Max(6, (int)((float)currentOptions.maxRadius * num3)));
			currentOptions.scavengingMultiplier *= num3 * num2;
			currentOptions.deteriorationMultiplier += Math.Min(0.2f, 1f / (num3 * num2 * 3f));
			if (num2 > 20f)
			{
				num2 = 20f;
			}
			while (num2 * (float)currentOptions.maxRadius > 800f)
			{
				num2 *= 0.9f;
			}
		}
		float num6 = (float)(int)((float)map.Area / 10000f) * Rand.Range(1f * num5, 2f * num5);
		Debug.Log("Scatter", "dist {0}, dens {1} (x{2}), scale x{3} ({4}-{5}), scav {6}, deter {7}", num4, currentOptions.densityMultiplier, num2, num3, currentOptions.minRadius, currentOptions.maxRadius, currentOptions.scavengingMultiplier, currentOptions.deteriorationMultiplier);
		Debug.Log("Scatter", "Spawning {0} ruin chunks", num6);
		BaseGen.globalSettings.map = map;
		bool flag = false;
		Find.TickManager.Pause();
		if (!Find.TickManager.Paused)
		{
			Find.TickManager.TogglePaused();
			flag = true;
		}
		CoverageMap coverage = CoverageMap.EmptyCoverageMap(map);
		for (int j = 0; (float)j < num6; j++)
		{
			try
			{
				ResolveParams rp = default(ResolveParams);
				List<AbstractDefenderForcesGenerator> generators = new List<AbstractDefenderForcesGenerator>();
				if (Rand.Chance(currentOptions.hostileChance))
				{
					generators = ((!Rand.Chance(0.8f)) ? new List<AbstractDefenderForcesGenerator>
					{
						new MechanoidsForcesGenerator(0)
					} : new List<AbstractDefenderForcesGenerator>
					{
						new AnimalInhabitantsForcesGenerator()
					});
				}
				rp.faction = Find.FactionManager.OfAncientsHostile;
				IntVec3 intVec = CellFinder.RandomNotEdgeCell(10, map);
				rp.rect = new CellRect(intVec.x, intVec.z, 1, 1);
				RuinsScatterer.Scatter(rp, currentOptions.Copy(), coverage, generators);
			}
			catch
			{
				Debug.Warning("Scatter", "Could not scatter a single ruins chunk.");
			}
		}
		if (flag)
		{
			Find.TickManager.TogglePaused();
		}
	}
}
