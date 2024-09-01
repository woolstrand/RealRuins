using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.BaseGen;
using Verse;
using Verse.AI.Group;

namespace RealRuins;

internal class BattleRoyaleForcesGenerator : AbstractDefenderForcesGenerator
{
	public override void GenerateForces(Map map, ResolveParams rp, ScatterOptions options)
	{
		if (options == null)
		{
			return;
		}
		int num = 0;
		float num2 = 10f;
		float num3 = options.uncoveredCost * (Rand.Value + 0.5f);
		Debug.Log("ForceGen", "Running battle royale force generation with remaining cost of {0} (while uncovered is {1})", num3, options.uncoveredCost);
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
			if (options.allowFriendlyRaids)
			{
				if (Rand.Chance(0.2f))
				{
					raidTrigger.faction = Find.FactionManager.RandomNonHostileFaction();
				}
				else
				{
					raidTrigger.faction = Find.FactionManager.RandomEnemyFaction();
				}
			}
			else
			{
				raidTrigger.faction = Find.FactionManager.RandomEnemyFaction();
			}
			int num6 = (int)(num3 / num2);
			float num7 = Math.Abs(Rand.Gaussian()) * (float)num6 + Rand.Value * (float)num6 + 250f;
			if (num7 > 10000f)
			{
				num7 = Rand.Range(8000, 11000);
			}
			num3 -= num7 * num2;
			raidTrigger.value = ScalePointsToDifficulty(num7);
			GenSpawn.Spawn(raidTrigger, randomCell, map);
			Debug.Log("ForceGen", "Spawned trigger at {0}, {1} for {2} points, autofiring after {3} rare ticks", randomCell.x, randomCell.z, raidTrigger.value, 0);
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
		if (uncoveredCost > 0f || currentOptions.startingPartyPoints > 0)
		{
			float num = 0f;
			if (currentOptions.startingPartyPoints > 0)
			{
				num = currentOptions.startingPartyPoints;
			}
			else
			{
				num = uncoveredCost / 10f;
				FloatRange floatRange = new FloatRange(num * 0.7f, Math.Min(12000f, num * 2f));
				Debug.Log("ForceGen", "Adding starting party. Remaining points: {0}. Used points range: {1}", currentOptions.uncoveredCost, floatRange);
			}
			num = ScalePointsToDifficulty(num);
			ScatterStartingParties((int)num, currentOptions.allowFriendlyRaids, map);
		}
	}

	private void ScatterStartingParties(int points, bool allowFriendly, Map map)
	{
		while (points > 0)
		{
			int num = Rand.Range(200, Math.Min(3000, points / 5));
			IntVec3 root = CellFinder.RandomNotEdgeCell(30, map);
			CellFinder.TryFindRandomSpawnCellForPawnNear(root, map, out var result);
			if (result.IsValid)
			{
				Faction faction = null;
				if (allowFriendly)
				{
					Find.FactionManager.TryGetRandomNonColonyHumanlikeFaction(out faction, tryMedievalOrBetter: false);
				}
				else
				{
					faction = Find.FactionManager.RandomEnemyFaction();
				}
				if (faction == null)
				{
					faction = Find.FactionManager.AllFactions.RandomElement();
				}
				SpawnGroup(num, new CellRect(result.x - 10, result.z - 10, 20, 20), faction, map);
				points -= num;
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
		pawnGroupMakerParms.generateFightersOnly = true;
		pawnGroupMakerParms.raidStrategy = RaidStrategyDefOf.ImmediateAttack;
		pawnGroupMakerParms.seed = Rand.Int;
		List<Pawn> list = PawnGroupMakerUtility.GeneratePawns(pawnGroupMakerParms).ToList();
		CellRect cellRect = locationRect;
		if (list == null)
		{
			Debug.Warning("ForceGen", "Pawns list is null");
		}
		foreach (Pawn item in list)
		{
			if (CellFinder.TryFindRandomSpawnCellForPawnNear(locationRect.RandomCell, map, out var result))
			{
				GenSpawn.Spawn(item, result, map, Rot4.Random);
			}
		}
		LordJob lordJob = null;
		lordJob = new LordJob_AssaultColony(faction, canKidnap: false, Rand.Chance(0.5f));
		if (lordJob != null)
		{
			LordMaker.MakeNewLord(faction, lordJob, map, list);
		}
	}
}
