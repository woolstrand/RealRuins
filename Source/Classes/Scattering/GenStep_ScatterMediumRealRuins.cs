using System;
using RimWorld;
using RimWorld.BaseGen;
using Verse;
using Verse.AI.Group;

namespace RealRuins;

internal class GenStep_ScatterMediumRealRuins : GenStep
{
	private ScatterOptions currentOptions;

	public override int SeedPart => 74293948;

	public override void Generate(Map map, GenStepParams parms)
	{
		Debug.Log("Scatter", "Medium generate");
		Find.TickManager.Pause();
		currentOptions = RealRuins_ModSettings.defaultScatterOptions.Copy();
		currentOptions.minRadius = 24;
		currentOptions.maxRadius = 50;
		currentOptions.scavengingMultiplier = 0.1f;
		currentOptions.deteriorationMultiplier = 0.1f;
		currentOptions.hostileChance = 0.8f;
		currentOptions.itemCostLimit = 800;
		currentOptions.minimumCostRequired = 25000;
		currentOptions.minimumDensityRequired = 0.01f;
		currentOptions.minimumAreaRequired = 4000;
		currentOptions.deleteLowQuality = false;
		currentOptions.shouldKeepDefencesAndPower = true;
		ResolveParams resolveParams = default(ResolveParams);
		BaseGen.globalSettings.map = map;
		resolveParams.rect = new CellRect(0, 0, map.Size.x, map.Size.z);
		resolveParams.SetCustom(Constants.ScatterOptions, currentOptions);
		resolveParams.faction = Find.FactionManager.OfAncientsHostile;
		BaseGen.symbolStack.Push("chargeBatteries", resolveParams);
		BaseGen.symbolStack.Push("refuel", resolveParams);
		BaseGen.symbolStack.Push("scatterRuins", resolveParams);
		if (Rand.Chance(0.5f * Find.Storyteller.difficulty.threatScale))
		{
			float points = Math.Abs(Rand.Gaussian()) * 500f * Find.Storyteller.difficulty.threatScale;
			resolveParams.faction = Find.FactionManager.RandomEnemyFaction();
			resolveParams.singlePawnLord = LordMaker.MakeNewLord(resolveParams.faction, new LordJob_AssaultColony(resolveParams.faction, canKidnap: false, canTimeoutOrFlee: false, sappers: true, useAvoidGridSmart: true), map);
			resolveParams.pawnGroupKindDef = resolveParams.pawnGroupKindDef ?? PawnGroupKindDefOf.Settlement;
			if (resolveParams.pawnGroupMakerParams == null)
			{
				resolveParams.pawnGroupMakerParams = new PawnGroupMakerParms();
				resolveParams.pawnGroupMakerParams.tile = map.Tile;
				resolveParams.pawnGroupMakerParams.faction = resolveParams.faction;
				PawnGroupMakerParms pawnGroupMakerParams = resolveParams.pawnGroupMakerParams;
				pawnGroupMakerParams.points = points;
			}
			BaseGen.symbolStack.Push("pawnGroup", resolveParams);
		}
		BaseGen.Generate();
	}
}
