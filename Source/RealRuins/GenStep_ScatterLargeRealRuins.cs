using System;
using System.Collections.Generic;
using RimWorld.BaseGen;
using Verse;

namespace RealRuins;

internal class GenStep_ScatterLargeRealRuins : GenStep
{
	private ScatterOptions currentOptions;

	public override int SeedPart => 74293947;

	public override void Generate(Map map, GenStepParams parms)
	{
		Find.TickManager.Pause();
		string text = map.Parent.GetComponent<RuinedBaseComp>()?.blueprintFileName;
		Debug.Log("Scatter", "Large Ruins - Preselected file name is {0}", text);
		currentOptions = RealRuins_ModSettings.defaultScatterOptions.Copy();
		currentOptions.minRadius = 200;
		currentOptions.maxRadius = 200;
		currentOptions.scavengingMultiplier = 0.1f;
		currentOptions.deteriorationMultiplier = 0f;
		currentOptions.hostileChance = 1f;
		currentOptions.blueprintFileName = text;
		currentOptions.costCap = map.Parent.GetComponent<RuinedBaseComp>()?.currentCapCost ?? (-1);
		currentOptions.startingPartyPoints = (int)(map.Parent.GetComponent<RuinedBaseComp>()?.raidersActivity ?? (-1f));
		currentOptions.minimumCostRequired = (int)Math.Min(100000f, RealRuins_ModSettings.ruinsCostCap);
		currentOptions.minimumDensityRequired = 0.015f;
		currentOptions.minimumAreaRequired = 6400;
		currentOptions.deleteLowQuality = false;
		currentOptions.shouldKeepDefencesAndPower = true;
		currentOptions.shouldLoadPartOnly = false;
		currentOptions.shouldAddRaidTriggers = Find.Storyteller.difficulty.allowBigThreats;
		currentOptions.claimableBlocks = false;
		currentOptions.enableDeterioration = false;
		ResolveParams resolveParams = default(ResolveParams);
		BaseGen.globalSettings.map = map;
		resolveParams.faction = Find.FactionManager.OfAncientsHostile;
		resolveParams.rect = new CellRect(0, 0, map.Size.x, map.Size.z);
		List<AbstractDefenderForcesGenerator> list = new List<AbstractDefenderForcesGenerator>
		{
			new BattleRoyaleForcesGenerator()
		};
		BaseGen.globalSettings.mainRect = resolveParams.rect;
		float uncoveredCost = currentOptions.uncoveredCost;
		if (uncoveredCost < 0f && Rand.Chance(0.5f))
		{
			uncoveredCost = 0f - uncoveredCost;
		}
		RuinsScatterer.Scatter(resolveParams, currentOptions, null, list);
		BaseGen.symbolStack.Push("chargeBatteries", resolveParams);
		BaseGen.symbolStack.Push("refuel", resolveParams);
		BaseGen.Generate();
		if (list == null)
		{
			return;
		}
		foreach (AbstractDefenderForcesGenerator item in list)
		{
			item.GenerateStartingParty(map, resolveParams, currentOptions);
		}
	}
}
