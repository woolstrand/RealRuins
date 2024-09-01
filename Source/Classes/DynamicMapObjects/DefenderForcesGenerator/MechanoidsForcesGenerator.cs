using System;
using System.Linq;
using RimWorld;
using RimWorld.BaseGen;
using Verse;
using Verse.AI.Group;

namespace RealRuins;

internal class MechanoidsForcesGenerator : AbstractDefenderForcesGenerator
{
	private float powerMax;

	public MechanoidsForcesGenerator(int power)
	{
		powerMax = power;
	}

	public override void GenerateForces(Map map, ResolveParams rp, ScatterOptions options)
	{
		Debug.Log("ForceGen", "Generating mechanoid forces");
		CellRect rect = rp.rect;
		PawnKindDef pawnKindDef = null;
		if (powerMax == 0f)
		{
			powerMax = (float)rect.Area / 30f;
		}
		powerMax = ScalePointsToDifficulty(powerMax);
		float num = Math.Abs(Rand.Gaussian(0.5f)) * powerMax + 1f;
		float num2 = 0f;
		Faction ofMechanoids = Faction.OfMechanoids;
		LordJob lordJob = new LordJob_DefendPoint(rect.CenterCell);
		Lord lord = LordMaker.MakeNewLord(ofMechanoids, lordJob, map);
		int tile = map.Tile;
		while (num2 <= num)
		{
			PawnKindDef kind2 = DefDatabase<PawnKindDef>.AllDefsListForReading.Where((PawnKindDef kind) => kind.RaceProps.IsMechanoid).RandomElementByWeight((PawnKindDef kind) => 1f / kind.combatPower);
			FloatRange? excludeBiologicalAgeRange = new FloatRange(0f, 16f);
			PawnGenerationRequest request = new PawnGenerationRequest(kind2, ofMechanoids, PawnGenerationContext.NonPlayer, tile, forceGenerateNewPawn: true, allowDead: false, allowDowned: false, canGeneratePawnRelations: true, mustBeCapableOfViolence: true, 1f, forceAddFreeWarmLayerIfNeeded: true, allowGay: true, allowPregnant: false, allowFood: true, allowAddictions: true, inhabitant: false, certainlyBeenInCryptosleep: false, forceRedressWorldPawnIfFormerColonist: false, worldPawnFactionDoesntMatter: false, 0f, 0f, null, 1f, null, null, null, null, null, null, null, null, null, null, null, null, forceNoIdeo: false, forceNoBackstory: false, forbidAnyTitle: false, forceDead: false, null, null, null, null, null, 0f, DevelopmentalStage.Adult, null, excludeBiologicalAgeRange);
			IntVec3 result = IntVec3.Invalid;
			CellFinder.TryFindRandomSpawnCellForPawnNear(rect.CenterCell, map, out result);
			if (result != IntVec3.Invalid)
			{
				Pawn pawn = PawnGenerator.GeneratePawn(request);
				FilthMaker.TryMakeFilth(result, map, ThingDefOf.Filth_Blood, 5);
				GenSpawn.Spawn(pawn, result, map);
				lord.AddPawn(pawn);
				num2 += pawn.kindDef.combatPower;
				Debug.Log("ForceGen", "Spawned mechanoid of faction {0}", ofMechanoids.Name);
				continue;
			}
			break;
		}
	}

	public override void GenerateStartingParty(Map map, ResolveParams rp, ScatterOptions options)
	{
	}
}
