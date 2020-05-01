using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;

using System.Reflection;

using RimWorld;
using RimWorld.Planet;
using Verse.AI;
using Verse.AI.Group;
using Verse;
using RimWorld.BaseGen;
using System.Runtime.CompilerServices;

namespace RealRuins
{


    class GenStep_ScatterRealRuins : GenStep {
        public override int SeedPart {
            get {
                return 74293945;
            }
        }

        private ScatterOptions currentOptions = RealRuins_ModSettings.defaultScatterOptions;

        public float CalculateDistanceToNearestSettlement(Map map)
        {
            int rootTile = map.Tile;
            int proximityLimit = 16;
            int minDistance = proximityLimit;

            foreach (WorldObject wo in Find.World.worldObjects.ObjectsAt(map.Tile)) {
                if (wo.Faction != Faction.OfPlayer && (wo is Settlement || wo is Site)) return 1.0f; //some default proximity index for bases and sites. not too much, but not flat area.
            }
            
            Find.WorldFloodFiller.FloodFill(rootTile, (int x) => !Find.World.Impassable(x), delegate(int tile, int traversalDistance)
            {
                if (traversalDistance > proximityLimit)
                {
                    return true;
                }

                //TODO: Check how traversing is done. If it's some kind of BFS, probably I should stop at the first settlement reached.
                if (traversalDistance > 0) {
                    foreach (WorldObject wo in Find.World.worldObjects.ObjectsAt(tile)) {
                        if (wo.Faction != Faction.OfPlayer) {
                            if (wo is Settlement) {
                                if (traversalDistance < minDistance) minDistance = traversalDistance;
                            } else if (wo is Site) {
                                if (traversalDistance * 2 < minDistance) minDistance = traversalDistance * 2; //site has twice less influence
                            }
                        }
                    }
                }

                return false;
            }, 2147483647, null);

            return minDistance;
        }


        public override void Generate(Map map, GenStepParams parms) {
            //add standard ruins along with real.
            if (RealRuins_ModSettings.preserveStandardRuins) {
                GenStep scatterOriginalRuins = new GenStep_ScatterRuinsSimple();
                scatterOriginalRuins.Generate(map, parms);
            }
            //skip generation due to low blueprints count
            if (SnapshotStoreManager.Instance.StoredSnapshotsCount() < 10) {
                Debug.Error(Debug.Scatter, "Skipping ruins gerenation due to low blueprints count.");
                return;
            }

            //Skip generation for starting tile if corresponding settings flag is set
            if (RealRuins_ModSettings.startWithoutRuins) {
                int homes = 0;
                var allMaps = Find.Maps;
                for (int i = 0; i < allMaps.Count; i++) {
                    Map someMap = allMaps[i];
                    if (someMap.IsPlayerHome) {
                        homes++;
                    }
                }

                if (homes == 1 && Find.TickManager.TicksGame < 10) return; //single home => we're generating that single home => that's starting map => no ruins here if this option is selected.
            }

            //Skip generation on other ruins world objects
            foreach (WorldObject wo in Find.World.worldObjects.ObjectsAt(map.Tile))
            {
                if (wo is RealRuinsPOIWorldObject || wo is AbandonedBaseWorldObject) return;
            }
            
            if (!map.TileInfo.WaterCovered) {

                float densityMultiplier = 1.0f;
                float scaleMultiplier = 1.0f;
                float distanceToSettlement = 0.0f;
                float totalDensity = RealRuins_ModSettings.defaultScatterOptions.densityMultiplier;
                currentOptions = RealRuins_ModSettings.defaultScatterOptions.Copy(); //store as instance variable to keep accessible on subsequent ScatterAt calls

                if (RealRuins_ModSettings.defaultScatterOptions.enableProximity) {
                        
                    distanceToSettlement = CalculateDistanceToNearestSettlement(map);
                    if (distanceToSettlement >= 16 && Rand.Chance(0.5f)) {
                        totalDensity = 0;
                    }
                    
                    if (totalDensity > 0) {
                        densityMultiplier = (float)(Math.Exp(1.0 / (distanceToSettlement / 10.0 + 0.3)) - 0.7);
                        scaleMultiplier = (float)(Math.Exp(1 / (distanceToSettlement / 5 + 0.5)) - 0.3);
                    } else {
                        densityMultiplier = 0.0f;
                    }

                    currentOptions.densityMultiplier *= densityMultiplier;
                    currentOptions.minRadius = Math.Min(60, Math.Max(6, (int)(currentOptions.minRadius * scaleMultiplier))); //keep between 6 and 60
                    currentOptions.maxRadius = Math.Min(60, Math.Max(6, (int)(currentOptions.maxRadius * scaleMultiplier))); //keep between 6 and 60
                    currentOptions.scavengingMultiplier *= scaleMultiplier * densityMultiplier;
                    currentOptions.deteriorationMultiplier += Math.Min(0.2f, (1.0f / (scaleMultiplier * densityMultiplier * 3)));


                    if (densityMultiplier > 20.0f) densityMultiplier = 20.0f;
                    while (densityMultiplier * currentOptions.maxRadius > 800) {
                        densityMultiplier *= 0.9f; //WHAT? Why not 800/radius?
                    }

                }

                //number of ruins based on density settings
                var num = (int)((float)map.Area / 10000.0f) * Rand.Range(1 * totalDensity, 2 * totalDensity);

                Debug.Log(Debug.Scatter, "dist {0}, dens {1} (x{2}), scale x{3} ({4}-{5}), scav {6}, deter {7}", distanceToSettlement, currentOptions.densityMultiplier, densityMultiplier, scaleMultiplier, currentOptions.minRadius, currentOptions.maxRadius, currentOptions.scavengingMultiplier, currentOptions.deteriorationMultiplier);
                Debug.Log(Debug.Scatter, "Spawning {0} ruin chunks", num);
                BaseGen.globalSettings.map = map;

                bool shouldUnpause = false;
                Find.TickManager.Pause();
                if (!Find.TickManager.Paused) {
                    Find.TickManager.TogglePaused();
                    shouldUnpause = true;
                }

                CoverageMap coverageMap = CoverageMap.EmptyCoverageMap(map);

                for (int i = 0; i < num; i++) {
                    try {
                        //We use copy of scatteroptions because each scatteroptions represents separate chunk with separate location, size, maps, etc.
                        //should use struct instead? is it compatible with IExposable?
                        ResolveParams rp = default(ResolveParams);

                        List<AbstractDefenderForcesGenerator> generators = new List<AbstractDefenderForcesGenerator>();
                        if (Rand.Chance(currentOptions.hostileChance)) {
                            if (Rand.Chance(0.8f)) {
                                generators = new List<AbstractDefenderForcesGenerator> { new AnimalInhabitantsForcesGenerator() };
                            } else {
                                generators = new List<AbstractDefenderForcesGenerator> { new MechanoidsForcesGenerator(0) };
                            }
                        }
                        rp.faction = Find.FactionManager.OfAncientsHostile;
                        var center = CellFinder.RandomNotEdgeCell(10, map);
                        rp.rect = new CellRect(center.x, center.z, 1, 1); //after generation will be extended to a real size
                        RuinsScatterer.Scatter(rp, currentOptions.Copy(), coverageMap, generators);
                    } catch {
                        Debug.Warning(Debug.Scatter, "Could not scatter a single ruins chunk.");
                    }
                }

                if (shouldUnpause) {
                    //Debug.Message("Finished spawning, unpausing");
                    Find.TickManager.TogglePaused();
                }
            }
        }


    }

    class GenStep_ScatterLargeRealRuins : GenStep {

        private ScatterOptions currentOptions;
        
        public override int SeedPart {
            get {
                return 74293947;
            }
        }


        public override void Generate(Map map, GenStepParams parms) {
            Find.TickManager.Pause();
            //Debug.Message("Overridden LARGE generate");

            string filename = map.Parent.GetComponent<RuinedBaseComp>()?.blueprintFileName;
            Debug.Log(Debug.Scatter, "Large Ruins - Preselected file name is {0}", filename);

            currentOptions = RealRuins_ModSettings.defaultScatterOptions.Copy(); //store as instance variable to keep accessible on subsequent ScatterAt calls

            currentOptions.minRadius = 200;
            currentOptions.maxRadius = 200;
            currentOptions.scavengingMultiplier = 0.1f;
            currentOptions.deteriorationMultiplier = 0.0f;
            currentOptions.hostileChance = 1.0f;


            currentOptions.blueprintFileName = filename;
            currentOptions.costCap = map.Parent.GetComponent<RuinedBaseComp>()?.currentCapCost ?? -1;
            currentOptions.startingPartyPoints = (int)(map.Parent.GetComponent<RuinedBaseComp>()?.raidersActivity ?? -1);
            currentOptions.minimumCostRequired = (int)Math.Min(100000.0f, RealRuins_ModSettings.ruinsCostCap);
            currentOptions.minimumDensityRequired = 0.015f;
            currentOptions.minimumAreaRequired = 6400;
            currentOptions.deleteLowQuality = false; //do not delete since we have much higher requirements for base ruins
            currentOptions.shouldKeepDefencesAndPower = true;
            currentOptions.shouldLoadPartOnly = false;
            currentOptions.shouldAddRaidTriggers = Find.Storyteller.difficulty.allowBigThreats;
            currentOptions.claimableBlocks = false;
            currentOptions.enableDeterioration = false;


            ResolveParams resolveParams = default(ResolveParams);
            BaseGen.globalSettings.map = map;
            resolveParams.faction = Find.FactionManager.OfAncientsHostile;
            resolveParams.rect = new CellRect(0, 0, map.Size.x, map.Size.z);
            List<AbstractDefenderForcesGenerator> generators = new List<AbstractDefenderForcesGenerator> { new BattleRoyaleForcesGenerator() };


            BaseGen.globalSettings.mainRect = resolveParams.rect;

            float uncoveredCost = currentOptions.uncoveredCost;
            if (uncoveredCost < 0) {
                if (Rand.Chance(0.5f)) {
                    uncoveredCost = -uncoveredCost; //adding really small party
                }
            }


            RuinsScatterer.Scatter(resolveParams, currentOptions, null, generators);
            BaseGen.symbolStack.Push("chargeBatteries", resolveParams);
            BaseGen.symbolStack.Push("refuel", resolveParams);
            BaseGen.Generate();



            //adding starting party
            //don't doing it via basegen because of uh oh i don't remember, something with pawn location control
            if (generators != null) {
                foreach (AbstractDefenderForcesGenerator generator in generators) {
                    generator.GenerateStartingParty(map, resolveParams, currentOptions);
                }
            }
        }


    }
    
    class GenStep_ScatterMediumRealRuins : GenStep {

        private ScatterOptions currentOptions;
        
        public override int SeedPart {
            get {
                return 74293948;
            }
        }


        public override void Generate(Map map, GenStepParams parms) {

            Debug.Log(Debug.Scatter, "Medium generate");
                Find.TickManager.Pause();

                currentOptions = RealRuins_ModSettings.defaultScatterOptions.Copy(); //store as instance variable to keep accessible on subsequent ScatterAt calls

                currentOptions.minRadius = 24;
                currentOptions.maxRadius = 50;   
                currentOptions.scavengingMultiplier = 0.1f;
                currentOptions.deteriorationMultiplier = 0.1f;
                currentOptions.hostileChance = 0.8f;
                currentOptions.itemCostLimit = 800;

                currentOptions.minimumCostRequired = 25000;
                currentOptions.minimumDensityRequired = 0.01f;
                currentOptions.minimumAreaRequired = 4000;
                currentOptions.deleteLowQuality = false; //do not delete since we have much higher requirements for base ruins
                currentOptions.shouldKeepDefencesAndPower = true;

                ResolveParams rp = default(ResolveParams);
                BaseGen.globalSettings.map = map;
                rp.rect = new CellRect(0, 0, map.Size.x, map.Size.z);
                rp.SetCustom<ScatterOptions>(Constants.ScatterOptions, currentOptions);
                rp.faction = Find.FactionManager.OfAncientsHostile;
                BaseGen.symbolStack.Push("chargeBatteries", rp);
                BaseGen.symbolStack.Push("refuel", rp);
                BaseGen.symbolStack.Push("scatterRuins", rp);


                if (Rand.Chance(0.5f * Find.Storyteller.difficulty.threatScale)) {
                    float pointsCost = Math.Abs(Rand.Gaussian()) * 500 * Find.Storyteller.difficulty.threatScale;

                rp.faction = Find.FactionManager.RandomEnemyFaction();
                rp.singlePawnLord = LordMaker.MakeNewLord(rp.faction,
                        new LordJob_AssaultColony(rp.faction, false, false, true, true), map, null);

                rp.pawnGroupKindDef = (rp.pawnGroupKindDef ?? PawnGroupKindDefOf.Settlement);

                    if (rp.pawnGroupMakerParams == null) {
                    rp.pawnGroupMakerParams = new PawnGroupMakerParms();
                    rp.pawnGroupMakerParams.tile = map.Tile;
                    rp.pawnGroupMakerParams.faction = rp.faction;
                        PawnGroupMakerParms pawnGroupMakerParams = rp.pawnGroupMakerParams;
                        pawnGroupMakerParams.points = pointsCost;
                    }

                    BaseGen.symbolStack.Push("pawnGroup", rp);

                }

                    
                BaseGen.Generate();
          
        }

    }
    
}
