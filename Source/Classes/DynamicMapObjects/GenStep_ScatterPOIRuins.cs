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

            RealRuinsPOIComp poiComp;
            bool skipForcesGeneration = false;
            bool overrideSpawnAsRuins = false;
            Faction faction = null;
            if (PlanetaryRuinsInitData.shared.startingPOI != null) {
                Debug.Log("[MapGen]", "Found starting poi in game init context.");
                RealRuinsPOIWorldObject poi = PlanetaryRuinsInitData.shared.startingPOI;
                poiComp = poi.GetComponent<RealRuinsPOIComp>();
                switch (PlanetaryRuinsInitData.shared.settleMode) {
                    case SettleMode.normal:
                        faction = null;
                        skipForcesGeneration = true;
                        overrideSpawnAsRuins = true;
                        break;
                    case SettleMode.takeover:
                        faction = Faction.OfPlayer;
                        skipForcesGeneration = true;
                        overrideSpawnAsRuins = false;
                        break;
                    case SettleMode.attack:
                        faction = poi.Faction;
                        skipForcesGeneration = false;
                        overrideSpawnAsRuins = false;
                        break;
                }
            } else {
                poiComp = map.Parent.GetComponent<RealRuinsPOIComp>();
                faction = map.ParentFaction;
            }
           
            string filename = SnapshotStoreManager.SnapshotNameFor(poiComp.blueprintName, poiComp.gameName);


            Debug.Log("Spawning POI: Preselected file name is {0}", filename);
            Debug.Log("Location is {0} {1}", poiComp.originX, poiComp.originZ);

            var bp = BlueprintLoader.LoadWholeBlueprintAtPath(filename);

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


            if (poiComp.poiType == (int)POIType.Ruins || faction == null || overrideSpawnAsRuins) {
                currentOptions.shouldAddFilth = true;
                currentOptions.forceFullHitPoints = false;
                currentOptions.enableDeterioration = true;
                currentOptions.overwritesEverything = false;
                currentOptions.costCap = (int)Math.Abs(Rand.Gaussian(0, Math.Max(5000, bp.width * bp.height)));
                currentOptions.itemCostLimit = Rand.Range(50, 500);
            } else {
                currentOptions.shouldAddFilth = false;
                currentOptions.forceFullHitPoints = true;
                currentOptions.enableDeterioration = false;
                currentOptions.overwritesEverything = true;
            }

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

            List<AbstractDefenderForcesGenerator> generators = null;
            if (!skipForcesGeneration) {
                generators = GeneratorsForBlueprint(bp, poiComp, faction);
            }

            ResolveParams resolveParams = default(ResolveParams);
            BaseGen.globalSettings.map = map;
            resolveParams.faction = faction;
            resolveParams.rect = new CellRect(currentOptions.overridePosition.x, currentOptions.overridePosition.z, map.Size.x - currentOptions.overridePosition.x, map.Size.z - currentOptions.overridePosition.z);


            BaseGen.globalSettings.mainRect = resolveParams.rect;

            float uncoveredCost = currentOptions.uncoveredCost;

            if (resolveParams.faction != null && resolveParams.faction != Faction.OfPlayer) {
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
                Debug.Log(Debug.BlueprintTransfer, "Found forces generators, generating {0} starting parties", generators.Count());
                foreach (AbstractDefenderForcesGenerator generator in generators) {
                    generator.GenerateStartingParty(map, resolveParams, currentOptions);
                }
            }

            // Cleanup init data to ensure it won't interefere in future
            PlanetaryRuinsInitData.shared.Cleanup();
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
                if (Rand.Chance(0.2f)) {
                    result.Add(new AnimalInhabitantsForcesGenerator());
                } else if (Rand.Chance(0.2f)) {
                    result.Add(new MechanoidsForcesGenerator(0));
                } else if (Rand.Chance(0.2f)) {
                    result.Add(new CitizenForcesGeneration(Rand.RangeInclusive(300, 1000), Find.FactionManager.RandomEnemyFaction(true, true, false)));
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
