using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.BaseGen;
using Verse;
using Verse.AI.Group;

namespace RealRuins;

internal class GenStep_ScatterPOIRuins : GenStep
{
	private ScatterOptions currentOptions;

	public override int SeedPart => 74293949;

	public override void Generate(Map map, GenStepParams parms)
	{
		Find.TickManager.Pause();
		bool flag = false;
		bool flag2 = false;
		Faction faction = null;
		RealRuinsPOIComp component;
		if (PlanetaryRuinsInitData.shared.startingPOI != null)
		{
			Debug.Log("[MapGen]", "Found starting poi in game init context.");
			RealRuinsPOIWorldObject startingPOI = PlanetaryRuinsInitData.shared.startingPOI;
			component = startingPOI.GetComponent<RealRuinsPOIComp>();
			switch (PlanetaryRuinsInitData.shared.settleMode)
			{
			case SettleMode.normal:
				faction = null;
				flag = true;
				flag2 = true;
				break;
			case SettleMode.takeover:
				faction = Faction.OfPlayer;
				flag = true;
				flag2 = false;
				break;
			case SettleMode.attack:
				faction = startingPOI.Faction;
				flag = false;
				flag2 = false;
				break;
			}
		}
		else
		{
			component = map.Parent.GetComponent<RealRuinsPOIComp>();
			faction = map.ParentFaction;
		}
		string text = SnapshotStoreManager.SnapshotNameFor(component.blueprintName, component.gameName);
		Debug.Log("Spawning POI: Preselected file name is {0}", text);
		Debug.Log("Location is {0} {1}", component.originX, component.originZ);
		Blueprint blueprint = BlueprintLoader.LoadWholeBlueprintAtPath(text);
		currentOptions = RealRuins_ModSettings.defaultScatterOptions.Copy();
		currentOptions.minRadius = 400;
		currentOptions.maxRadius = 400;
		currentOptions.scavengingMultiplier = 0f;
		currentOptions.deteriorationMultiplier = 0f;
		currentOptions.hostileChance = 0f;
		currentOptions.blueprintFileName = text;
		currentOptions.costCap = -1;
		currentOptions.startingPartyPoints = -1;
		currentOptions.minimumCostRequired = 0;
		currentOptions.minimumDensityRequired = 0f;
		currentOptions.minimumAreaRequired = 0;
		currentOptions.deleteLowQuality = false;
		currentOptions.shouldKeepDefencesAndPower = true;
		currentOptions.shouldLoadPartOnly = false;
		currentOptions.shouldAddRaidTriggers = false;
		currentOptions.claimableBlocks = false;
		if (component.poiType == 10 || faction == null || flag2)
		{
			currentOptions.shouldAddFilth = true;
			currentOptions.forceFullHitPoints = false;
			currentOptions.enableDeterioration = true;
			currentOptions.overwritesEverything = false;
			currentOptions.costCap = (int)Math.Abs(Rand.Gaussian(0f, Math.Max(5000, blueprint.width * blueprint.height)));
			currentOptions.itemCostLimit = Rand.Range(50, 500);
		}
		else
		{
			currentOptions.shouldAddFilth = false;
			currentOptions.forceFullHitPoints = true;
			currentOptions.enableDeterioration = false;
			currentOptions.overwritesEverything = true;
		}
		currentOptions.overridePosition = new IntVec3(blueprint.originX, 0, blueprint.originZ);
		currentOptions.centerIfExceedsBounds = true;
		Debug.Log("BlueprintTransfer", "Trying to place POI map at tile {0}, at {1},{2} to {3},{4} ({5}x{6})", map.Parent.Tile, blueprint.originX, blueprint.originZ, blueprint.originX + blueprint.width, blueprint.originZ + blueprint.height, blueprint.width, blueprint.height);
		List<AbstractDefenderForcesGenerator> list = null;
		if (!flag)
		{
			list = GeneratorsForBlueprint(blueprint, component, faction);
		}
		ResolveParams resolveParams = default(ResolveParams);
		BaseGen.globalSettings.map = map;
		resolveParams.faction = faction;
		resolveParams.rect = new CellRect(currentOptions.overridePosition.x, currentOptions.overridePosition.z, map.Size.x - currentOptions.overridePosition.x, map.Size.z - currentOptions.overridePosition.z);
		BaseGen.globalSettings.mainRect = resolveParams.rect;
		float uncoveredCost = currentOptions.uncoveredCost;
		if (resolveParams.faction != null && resolveParams.faction != Faction.OfPlayer)
		{
			ManTurrets((int)((float)component.mannableCount * 1.25f + 1f), resolveParams, map);
		}
		RuinsScatterer.Scatter(resolveParams, currentOptions, null, list);
		BaseGen.symbolStack.Push("chargeBatteries", resolveParams);
		BaseGen.symbolStack.Push("ensureCanHoldRoof", resolveParams);
		BaseGen.symbolStack.Push("refuel", resolveParams);
		BaseGen.Generate();
		if (list != null)
		{
			Debug.Log("BlueprintTransfer", "Found forces generators, generating {0} starting parties", list.Count());
			foreach (AbstractDefenderForcesGenerator item in list)
			{
				item.GenerateStartingParty(map, resolveParams, currentOptions);
			}
		}
		PlanetaryRuinsInitData.shared.Cleanup();
	}

	private void ManTurrets(int count, ResolveParams rp, Map map)
	{
		for (int i = 0; i < count; i++)
		{
			Lord singlePawnLord = LordMaker.MakeNewLord(rp.faction, new LordJob_ManTurrets(), map);
			PawnKindDef kind = rp.faction.RandomPawnKind();
			int tile = map.Tile;
			Faction faction = rp.faction;
			FloatRange? biologicalAgeRange = new FloatRange(16f, 300f);
			PawnGenerationRequest value = new PawnGenerationRequest(kind, faction, PawnGenerationContext.NonPlayer, tile, forceGenerateNewPawn: false, allowDead: false, allowDowned: false, canGeneratePawnRelations: true, mustBeCapableOfViolence: true, 1f, forceAddFreeWarmLayerIfNeeded: false, allowGay: true, allowPregnant: false, allowFood: true, allowAddictions: true, inhabitant: true, certainlyBeenInCryptosleep: false, forceRedressWorldPawnIfFormerColonist: false, worldPawnFactionDoesntMatter: false, 0f, 0f, null, 1f, null, null, null, null, null, null, null, null, null, null, null, null, forceNoIdeo: false, forceNoBackstory: false, forbidAnyTitle: false, forceDead: false, null, null, null, null, null, 0f, DevelopmentalStage.Adult, null, null, biologicalAgeRange);
			ResolveParams resolveParams = rp;
			resolveParams.singlePawnGenerationRequest = value;
			resolveParams.singlePawnLord = singlePawnLord;
			BaseGen.symbolStack.Push("pawn", resolveParams);
		}
	}

	private List<AbstractDefenderForcesGenerator> GeneratorsForBlueprint(Blueprint bp, RealRuinsPOIComp poiComp, Faction faction)
	{
		List<AbstractDefenderForcesGenerator> list = new List<AbstractDefenderForcesGenerator>();
		Debug.Log("Scatter", "Selecting force generators");
		if (faction == null || poiComp.poiType == 10)
		{
			if (Rand.Chance(0.2f))
			{
				list.Add(new AnimalInhabitantsForcesGenerator());
			}
			else if (Rand.Chance(0.2f))
			{
				list.Add(new MechanoidsForcesGenerator(0));
			}
			else if (Rand.Chance(0.2f))
			{
				list.Add(new CitizenForcesGeneration(Rand.RangeInclusive(300, 1000), Find.FactionManager.RandomEnemyFaction(allowHidden: true, allowDefeated: true, allowNonHumanlike: false)));
			}
			Debug.Log("Scatter", "Selected {0} for abandoned or ruins", list.Count);
			return list;
		}
		switch ((POIType)poiComp.poiType)
		{
		case POIType.MilitaryBaseSmall:
		case POIType.Outpost:
		case POIType.MilitaryBaseLarge:
		case POIType.Stronghold:
			list.Add(new MilitaryForcesGenerator(poiComp.militaryPower));
			break;
		case POIType.Camp:
		case POIType.City:
		case POIType.Factory:
		case POIType.Research:
		case POIType.PowerPlant:
			list.Add(new CitizenForcesGeneration(poiComp.bedsCount));
			if (bp.totalCost > 50000f || (bp.totalCost > 10000f && poiComp.bedsCount < 6))
			{
				list.Add(new MilitaryForcesGenerator(3f));
			}
			break;
		case POIType.Storage:
			if (bp.totalCost > 30000f)
			{
				list.Add(new MilitaryForcesGenerator(2f));
			}
			else
			{
				list.Add(new CitizenForcesGeneration(poiComp.bedsCount));
			}
			break;
		default:
			if (Rand.Chance(0.5f))
			{
				list.Add(new MechanoidsForcesGenerator(0));
			}
			break;
		}
		Debug.Log("Scatter", "Selected {0} for POIs", list.Count);
		return list;
	}
}
