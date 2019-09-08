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
            Debug.Log("Location is {0} {1}", poiComp.originX, poiComp.originZ);

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
                currentOptions.forceFullHitPoints = false;
                currentOptions.enableDeterioration = true;
                currentOptions.overwritesEverything = false;
                currentOptions.costCap = 10000;
                currentOptions.itemCostLimit = Rand.Range(40, 300);
            } else { 
                currentOptions.forceFullHitPoints = true;
                currentOptions.enableDeterioration = false;
                currentOptions.overwritesEverything = true;
            }

            currentOptions.overridePosition = new IntVec3(poiComp.originX, 0, poiComp.originZ);
            currentOptions.centerIfExceedsBounds = true;

            var bp = BlueprintLoader.LoadWholeBlueprintAtPath(filename);

            //FOR DEBUG LOGGING
            var a = new BlueprintAnalyzer(bp, currentOptions);
            a.Analyze();

            Debug.Log(Debug.BlueprintTransfer, "Trying to place POI map at tile {0}, at {1},{2} to {3},{4} ({5}x{6})",
                map.Parent.Tile,
                poiComp.originX, poiComp.originZ,
                poiComp.originX + bp.width, poiComp.originZ + bp.height,
                bp.width, bp.height);

            var generators = GeneratorsForBlueprint(bp, poiComp, map.Parent.Faction);

            ResolveParams resolveParams = default(ResolveParams);
            BaseGen.globalSettings.map = map;
            resolveParams.SetCustom<ScatterOptions>(Constants.ScatterOptions, currentOptions);
            resolveParams.faction = map.ParentFaction;
            resolveParams.SetCustom(Constants.ForcesGenerators, generators);
            resolveParams.rect = new CellRect(currentOptions.overridePosition.x, currentOptions.overridePosition.z, map.Size.x - currentOptions.overridePosition.x, map.Size.z - currentOptions.overridePosition.z);

            BaseGen.symbolStack.Push("scatterRuins", resolveParams);

            BaseGen.globalSettings.mainRect = resolveParams.rect;

            float uncoveredCost = currentOptions.uncoveredCost;

            BaseGen.symbolStack.Push("chargeBatteries", resolveParams);
            BaseGen.symbolStack.Push("refuel", resolveParams);

            BaseGen.Generate();

            List<AbstractDefenderForcesGenerator> f_generators = resolveParams.GetCustom<List<AbstractDefenderForcesGenerator>>(Constants.ForcesGenerators);
            if (f_generators != null) {
                foreach (AbstractDefenderForcesGenerator generator in f_generators) {
                    generator.GenerateStartingParty(map, resolveParams);
                }
            }

        }

        private List<AbstractDefenderForcesGenerator> GeneratorsForBlueprint(Blueprint bp, RealRuinsPOIComp poiComp, Faction faction) {
            List<AbstractDefenderForcesGenerator> result = new List<AbstractDefenderForcesGenerator>();

            if (faction == null && (POIType)poiComp.poiType != POIType.Ruins) {
                
            }

            switch ((POIType)poiComp.poiType) {
                case POIType.MilitaryBaseSmall:
                case POIType.MilitaryBaseLarge:
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
                    if (Rand.Chance(0.3f)) {
                        result.Add(new AnimalInhabitantsForcesGenerator());
                    } else if (Rand.Chance(0.5f)) {
                        result.Add(new MechanoidsForcesGenerator(0));
                    }
                    break;
            }
            return result;
        }
    }
}
