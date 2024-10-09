using System;
using RimWorld;
using RimWorld.BaseGen;
using Verse;
using Verse.AI.Group;

namespace RealRuins;

internal class AnimalInhabitantsForcesGenerator : AbstractDefenderForcesGenerator
{
	public override void GenerateForces(Map map, ResolveParams rp, ScatterOptions options)
	{
		Debug.Log("ForceGen", "Animal forces generation");
		CellRect rect = rp.rect;
		PawnKindDef pawnKindDef = null;
		pawnKindDef = map.Biome.AllWildAnimals.RandomElementByWeight((PawnKindDef def) => (def.RaceProps.foodType == FoodTypeFlags.CarnivoreAnimal || def.RaceProps.foodType == FoodTypeFlags.OmnivoreAnimal) ? 1 : 0);
		float num = (float)Math.Sqrt(options.uncoveredCost / 10f * ((float)rect.Area / 30f));
		Debug.Log("ForceGen", "Unscaled power is {0} based on cost of {1} and area of {2}", num, options.uncoveredCost, rect.Area);
		num = ScalePointsToDifficulty(num);
		float num2 = Math.Abs(Rand.Gaussian(0.5f)) * num + 1f;
		float num3 = 0f;
		Faction ofAncientsHostile = Faction.OfAncientsHostile;
		LordJob lordJob = new LordJob_DefendPoint(rect.CenterCell);
		Lord lord = LordMaker.MakeNewLord(ofAncientsHostile, lordJob, map);
		int tile = map.Tile;
		Pawn pawn;
		for (; num3 <= num2; num3 += pawn.kindDef.combatPower)
		{
			PawnKindDef kind = pawnKindDef;
			PawnGenerationRequest request = new PawnGenerationRequest(kind, ofAncientsHostile, PawnGenerationContext.NonPlayer, tile, forceGenerateNewPawn: true, allowDead: false, allowDowned: false, canGeneratePawnRelations: true, mustBeCapableOfViolence: true, 1f, forceAddFreeWarmLayerIfNeeded: true);
			IntVec3 result = IntVec3.Invalid;
			if (!CellFinder.TryFindRandomCellInsideWith(rect, (IntVec3 x) => x.Standable(map) && options.roomMap[x.x - rect.minX, x.z - rect.minZ] > 1, out result))
			{
				CellFinder.TryFindRandomSpawnCellForPawnNear(rect.CenterCell, map, out result);
			}
			if (result != IntVec3.Invalid)
			{
				pawn = PawnGenerator.GeneratePawn(request);
				FilthMaker.TryMakeFilth(result, map, ThingDefOf.Filth_Blood, 5);
				GenSpawn.Spawn(pawn, result, map);
				lord.AddPawn(pawn);
				continue;
			}
			break;
		}
	}

	public override void GenerateStartingParty(Map map, ResolveParams rp, ScatterOptions options)
	{
	}
}
