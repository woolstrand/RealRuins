using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.BaseGen;
using Verse;
using Verse.AI.Group;

namespace RealRuins;

internal class MilitaryForcesGenerator : AbstractDefenderForcesGenerator
{
	private float militaryPower = 1f;

	private int minTriggerTimeout = 0;

	public MilitaryForcesGenerator(float militaryPower, int minimalTriggerFiringTimeout = 0)
	{
		if (militaryPower > 1f)
		{
			this.militaryPower = militaryPower;
			minTriggerTimeout = minimalTriggerFiringTimeout;
		}
	}

	public override void GenerateForces(Map map, ResolveParams rp, ScatterOptions options)
	{
		if (options == null)
		{
			return;
		}
		int num = 0;
		float num2 = 10f;
		float num3 = options.uncoveredCost * (Rand.Value + 0.5f);
		Debug.Log("ForceGen", "Running military force generation with remaining cost of {0} (while uncovered is {1})", num3, options.uncoveredCost);
		float num4 = num3;
		int num5 = 100;
		while (num3 > 0f)
		{
			IntVec3 randomCell = rp.rect.RandomCell;
			if (!randomCell.InBounds(map))
			{
				continue;
			}
			ThingDef def = ThingDef.Named("RaidTrigger");
			RaidTrigger raidTrigger = ThingMaker.MakeThing(def) as RaidTrigger;
			raidTrigger.faction = rp.faction;
			int num6 = (int)(num3 / num2);
			float num7 = Math.Abs(Rand.Gaussian()) * (float)num6 + Rand.Value * (float)num6 + 250f;
			if (num7 > 10000f)
			{
				num7 = Rand.Range(8000, 11000);
			}
			num3 -= num7 * num2;
			int num8 = (int)Math.Abs(Rand.Gaussian(0f, 75f));
			raidTrigger.value = ScalePointsToDifficulty(num7);
			raidTrigger.SetTimeouts(num8);
			GenSpawn.Spawn(raidTrigger, randomCell, map);
			Debug.Log("ForceGen", "Spawned trigger at {0}, {1} for {2} points, autofiring after {3} rare ticks", randomCell.x, randomCell.z, raidTrigger.value, num8);
			num++;
			options.uncoveredCost = Math.Abs(num3);
			if (num <= num5 || !(num3 < num4 * 0.2f) || !Rand.Chance(0.1f))
			{
				continue;
			}
			if (num3 > 100000f)
			{
				num3 = Rand.Range(80000, 110000);
			}
			break;
		}
	}

	public override void GenerateStartingParty(Map map, ResolveParams rp, ScatterOptions currentOptions)
	{
		float uncoveredCost = currentOptions.uncoveredCost;
		int num = (int)(uncoveredCost / (10f * militaryPower));
		int num2 = 0;
		num2 = ((num <= 10000) ? num : Rand.Range(5000, 10000));
		Debug.Log("ForceGen", "Military gen: uncoveredCost {0}, military power: {1}, total points allowed: {2}", uncoveredCost, militaryPower, num);
		num -= num2;
		SpawnGroup((int)ScalePointsToDifficulty(num2), rp.rect, rp.faction, map);
		Debug.Log("ForceGen", "Initial group of {0} spawned, {1} points left for triggers", num2, num);
		while (num > 0)
		{
			IntVec3 randomCell = rp.rect.RandomCell;
			if (randomCell.InBounds(map))
			{
				ThingDef def = ThingDef.Named("RaidTrigger");
				RaidTrigger raidTrigger = ThingMaker.MakeThing(def) as RaidTrigger;
				raidTrigger.faction = rp.faction;
				raidTrigger.SetTimeouts(0, 300);
				int num3 = (int)(10000.0 / Math.Max(Math.Sqrt(militaryPower), 1.0));
				float num4 = Math.Abs(Rand.Gaussian()) * (float)num3 + Rand.Value * (float)num3 + 250f;
				if (num4 > 10000f)
				{
					num4 = Rand.Range(8000, 11000);
				}
				num -= (int)num4;
				raidTrigger.value = ScalePointsToDifficulty(num);
				GenSpawn.Spawn(raidTrigger, randomCell, map);
				Debug.Log("ForceGen", "Spawned trigger at {0}, {1} for {2} points, autofiring after {3} rare ticks", randomCell.x, randomCell.z, raidTrigger.value, 0);
			}
		}
	}

	private void SpawnGroup(int points, CellRect locationRect, Faction faction, Map map)
	{
		PawnGroupMakerParms pawnGroupMakerParms = new PawnGroupMakerParms();
		pawnGroupMakerParms.groupKind = PawnGroupKindDefOf.Combat;
		pawnGroupMakerParms.tile = map.Tile;
		pawnGroupMakerParms.points = points;
		pawnGroupMakerParms.faction = faction;
		pawnGroupMakerParms.generateFightersOnly = false;
		pawnGroupMakerParms.seed = Rand.Int;
		List<Pawn> list = PawnGroupMakerUtility.GeneratePawns(pawnGroupMakerParms).ToList();
		CellRect cellRect = locationRect;
		if (list == null)
		{
			Debug.Warning("Pawns list is null");
		}
		else
		{
			Debug.Log("Pawns list contains {0} records", list.Count);
		}
		foreach (Pawn item in list)
		{
			if (CellFinder.TryFindRandomCellInsideWith(locationRect, (IntVec3 x) => x.Standable(map), out var result))
			{
				GenSpawn.Spawn(item, result, map, Rot4.Random);
			}
			else
			{
				Debug.Warning("Can't find location!");
			}
		}
		LordJob lordJob = null;
		lordJob = new LordJob_DefendBase(faction, cellRect.CenterCell);
		if (lordJob != null)
		{
			LordMaker.MakeNewLord(faction, lordJob, map, list);
		}
	}
}
