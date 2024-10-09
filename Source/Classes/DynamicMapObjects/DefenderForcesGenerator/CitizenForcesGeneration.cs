using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.BaseGen;
using Verse;
using Verse.AI.Group;

namespace RealRuins;

internal class CitizenForcesGeneration : AbstractDefenderForcesGenerator
{
	private int bedCount;

	private Faction faction;

	public CitizenForcesGeneration(int bedCount, Faction factionOverride = null)
	{
		this.bedCount = bedCount;
		faction = factionOverride;
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
		Debug.Log("ForceGen", "Running citizen force generation with remaining cost of {0} (while uncovered is {1})", num3, options.uncoveredCost);
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
		Faction faction = rp.faction;
		if (this.faction != null)
		{
			faction = this.faction;
		}
		if (faction == null)
		{
			faction = Find.FactionManager.RandomEnemyFaction(allowHidden: true, allowDefeated: true, allowNonHumanlike: false);
		}
		Debug.Log("ForceGen", "Citizen force gen: uncoveredCost {0}. rect {1}", uncoveredCost, rp.rect);
		int num = Math.Min((int)(uncoveredCost / 10f), 5000);
		SpawnGroup((int)ScalePointsToDifficulty(num), rp.rect, faction, map, Rand.Range(Math.Max(2, (int)((double)bedCount * 0.7)), (int)((double)bedCount * 1.5)));
		currentOptions.uncoveredCost -= num * 10;
	}

	private void SpawnGroup(int points, CellRect locationRect, Faction faction, Map map, int countCap)
	{
		PawnGroupKindDef groupKind = ((faction.def.pawnGroupMakers.Where((PawnGroupMaker gm) => gm.kindDef == PawnGroupKindDefOf.Settlement).Count() <= 0) ? faction.def.pawnGroupMakers.RandomElement().kindDef : PawnGroupKindDefOf.Settlement);
		PawnGroupMakerParms pawnGroupMakerParms = new PawnGroupMakerParms();
		pawnGroupMakerParms.groupKind = groupKind;
		pawnGroupMakerParms.tile = map.Tile;
		pawnGroupMakerParms.points = points;
		pawnGroupMakerParms.faction = faction;
		pawnGroupMakerParms.generateFightersOnly = false;
		pawnGroupMakerParms.seed = Rand.Int;
		List<Pawn> list = PawnGroupMakerUtility.GeneratePawns(pawnGroupMakerParms).ToList();
		CellRect cellRect = locationRect;
		if (list == null)
		{
			Debug.Warning("ForceGen", "Generating starting party: Pawns list is null");
		}
		else
		{
			Debug.Log("ForceGen", "Pawns list contains {0} records. Cap is {1}", list.Count, countCap);
		}
		while (list.Count > countCap)
		{
			list.Remove(list.RandomElement());
		}
		foreach (Pawn item in list)
		{
			if (CellFinder.TryFindRandomCellInsideWith(locationRect, (IntVec3 x) => x.Standable(map), out var result))
			{
				GenSpawn.Spawn(item, result, map, Rot4.Random);
				Debug.Extra("ForceGen", "Spawned pawn {0} at {1}, {2}", item.Name, result.x, result.z);
			}
			else
			{
				Debug.Warning("ForceGen", "Can't find spawning location for defender pawn!");
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
