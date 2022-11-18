using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using RimWorld;
using RimWorld.BaseGen;
using RimWorld.Planet;
using Verse.AI;
using Verse.AI.Group;
using Verse;

namespace RealRuins {
    class GenStep_ScatterPOIRuins : GenStep {
        private ScatterOptions currentOptions;

        public override int SeedPart {
            get {
                return 74293949;
            }
        }


        public override void Generate(Map map, GenStepParams parms) {
            Find.TickManager.Pause();
            //Debug.Message("Overridden LARGE generate");

            RealRuinsPOIComp poiComp = map.Parent.GetComponent<RealRuinsPOIComp>();
            string filename = SnapshotStoreManager.Instance.SnapshotNameFor(poiComp.blueprintName, poiComp.gameName);


            Debug.Log("Spawning POI: Preselected file name is {0}", filename);
            Debug.Log("Location (PRESUMABLY WRONG) is {0} {1}", poiComp.originX, poiComp.originZ);

            currentOptions = RealRuins_ModSettings.defaultScatterOptions.Copy(); //store as instance variable to keep accessible on subsequent ScatterAt calls

            currentOptions.minRadius = 400;
            currentOptions.maxRadius = 400;
            currentOptions.scavengingMultiplier = 0.0f;
            currentOptions.deteriorationMultiplier = 0.0f;
            currentOptions.hostileChance = 0.0f;


            currentOptions.blueprintFileName = filename;
            currentOptions.costCap = -1;
            currentOptions.startingPartyPoints = -1;
            currentOptions.minimumCostRequired = 0;
            currentOptions.minimumDensityRequired = 0.0f;
            currentOptions.minimumAreaRequired = 0;
            currentOptions.deleteLowQuality = false;
            currentOptions.shouldKeepDefencesAndPower = true;
            currentOptions.shouldLoadPartOnly = false;
            currentOptions.shouldAddRaidTriggers = false;
            currentOptions.claimableBlocks = false;


            if (poiComp.poiType == (int)POIType.Ruins || map.ParentFaction == null) {
                /*if (Rand.Chance(0.1f)) {
                    currentOptions.wallsDoorsOnly = true;
                } else {
                    currentOptions.deteriorationMultiplier = Math.Abs(Rand.Gaussian(0, 0.15f));
                }*/
                currentOptions.shouldAddFilth = true;
                currentOptions.forceFullHitPoints = false;
                currentOptions.enableDeterioration = true;
                currentOptions.overwritesEverything = false;
                currentOptions.costCap = (int)Math.Abs(Rand.Gaussian(0, 10000));
                currentOptions.itemCostLimit = Rand.Range(50, 300);
            } else {
                currentOptions.shouldAddFilth = false;
                currentOptions.forceFullHitPoints = true;
                currentOptions.enableDeterioration = false;
                currentOptions.overwritesEverything = true;
            }


            var bp = BlueprintLoader.LoadWholeBlueprintAtPath(filename);

            currentOptions.overridePosition = new IntVec3(bp.originX, 0, bp.originZ);
            currentOptions.centerIfExceedsBounds = true;

            //FOR DEBUG LOGGING
            //var a = new BlueprintAnalyzer(bp, currentOptions);
            //a.Analyze();

            Debug.Log(Debug.BlueprintTransfer, "Trying to place POI map at tile {0}, at {1},{2} to {3},{4} ({5}x{6})",
                map.Parent.Tile,
                bp.originX, bp.originZ,
                bp.originX + bp.width, bp.originZ + bp.height,
                bp.width, bp.height);

            var generators = GeneratorsForBlueprint(bp, poiComp, map.Parent.Faction);

            ResolveParams resolveParams = default(ResolveParams);
            BaseGen.globalSettings.map = map;
            resolveParams.faction = map.ParentFaction;
            resolveParams.rect = new CellRect(currentOptions.overridePosition.x, currentOptions.overridePosition.z, map.Size.x - currentOptions.overridePosition.x, map.Size.z - currentOptions.overridePosition.z);


            BaseGen.globalSettings.mainRect = resolveParams.rect;

            float uncoveredCost = currentOptions.uncoveredCost;

            if (resolveParams.faction != null) {
                //Debug.Log("Mannable count: {0}", poiComp.mannableCount);
                ManTurrets((int)(poiComp.mannableCount * 1.25f + 1), resolveParams, map);
            }


            RuinsScatterer.Scatter(resolveParams, currentOptions, null, generators);

            //ok, but why LIFO? Queue looks more suitable for map generation.
            //Looks like it was done for nested symbols resolving, but looks strange anyway.

            BaseGen.symbolStack.Push("chargeBatteries", resolveParams);
            BaseGen.symbolStack.Push("ensureCanHoldRoof", resolveParams);
            BaseGen.symbolStack.Push("refuel", resolveParams);

            BaseGen.Generate();

            if (generators != null) {
                foreach (AbstractDefenderForcesGenerator generator in generators) {
                    generator.GenerateStartingParty(map, resolveParams, currentOptions);
                }
            }

        }

        private void ManTurrets(int count, ResolveParams rp, Map map) {
            for (int i = 0; i < count; i++) {
                Lord singlePawnLord = LordMaker.MakeNewLord(rp.faction, new LordJob_ManTurrets(), map);
                PawnKindDef kind = rp.faction.RandomPawnKind();
                int tile = map.Tile;
                PawnGenerationRequest value = new PawnGenerationRequest(kind, rp.faction, PawnGenerationContext.NonPlayer, tile, forceGenerateNewPawn: false, biologicalAgeRange: new FloatRange(16, 300), allowDead: false, allowDowned: false, canGeneratePawnRelations: true, mustBeCapableOfViolence: true, colonistRelationChanceFactor: 1f, forceAddFreeWarmLayerIfNeeded: false, allowGay: true, allowFood: true, inhabitant: true);  ;
                ResolveParams resolveParams = rp; //copy!
                resolveParams.singlePawnGenerationRequest = value;
                resolveParams.singlePawnLord = singlePawnLord;
                BaseGen.symbolStack.Push("pawn", resolveParams);
                //Debug.Log("Pushed ManTurrets pawn symbol to stack");
            }
        }

        private List<AbstractDefenderForcesGenerator> GeneratorsForBlueprint(Blueprint bp, RealRuinsPOIComp poiComp, Faction faction) {
            List<AbstractDefenderForcesGenerator> result = new List<AbstractDefenderForcesGenerator>();

            Debug.Log(Debug.Scatter, "Selecting force generators");
            //override forces for any kind of POI if no faction selected
            if (faction == null || (POIType)poiComp.poiType == POIType.Ruins) {
                if (Rand.Chance(0.25f)) {
                    result.Add(new AnimalInhabitantsForcesGenerator());
                } else if (Rand.Chance(0.333f)) {
                    result.Add(new MechanoidsForcesGenerator(0));
                } else if (Rand.Chance(0.5f)) {
                    result.Add(new CitizenForcesGeneration(1000, Find.FactionManager.RandomEnemyFaction(true, true, false)));
                }
                Debug.Log(Debug.Scatter, "Selected {0} for abandoned or ruins", result.Count);
                return result;
            }

            switch ((POIType)poiComp.poiType) {
                case POIType.MilitaryBaseSmall:
                case POIType.MilitaryBaseLarge:
                case POIType.Stronghold:
                case POIType.Outpost:
                    result.Add(new MilitaryForcesGenerator(poiComp.militaryPower));
                    break;
                case POIType.Camp:
                case POIType.City:
                case POIType.Factory:
                case POIType.PowerPlant:
                case POIType.Research:
                    result.Add(new CitizenForcesGeneration(poiComp.bedsCount));
                    if (bp.totalCost > 50000 || bp.totalCost > 10000 && (poiComp.bedsCount < 6)) {
                        result.Add(new MilitaryForcesGenerator(3));
                    }
                    break;
                case POIType.Storage:
                    if (bp.totalCost > 30000) {
                        result.Add(new MilitaryForcesGenerator(2));
                    } else {
                        result.Add(new CitizenForcesGeneration(poiComp.bedsCount));
                    }
                    break;
                case POIType.Ruins:
                default:
                    if (Rand.Chance(0.5f)) {
                        result.Add(new MechanoidsForcesGenerator(0));
                    }
                    break;
            }
            Debug.Log(Debug.Scatter, "Selected {0} for POIs", result.Count);
            return result;
        }
    }
}
