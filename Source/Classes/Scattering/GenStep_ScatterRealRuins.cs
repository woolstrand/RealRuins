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

namespace RealRuins
{

    class GenStep_ScatterRealRuins : GenStep_Scatterer {
        public override int SeedPart {
            get {
                return 74293945;
            }
        }

        private float multiplier = 1.0f;
        private ScatterOptions currentOptions = RealRuins_ModSettings.defaultScatterOptions;



        public float CalculateProximityMultiplier(Map map)
        {
            int rootTile = map.Tile;
            int proximityLimit = 16;
            float proximityFactor = 0.05f;
            List<int> distances = new List<int>();

            foreach (WorldObject wo in Find.World.worldObjects.ObjectsAt(map.Tile)) {
                if (wo.Faction != Faction.OfPlayer && (wo is Settlement || wo is Site)) return 1.0f; //some default proximity index for bases and sites. not too much, but not flat area.
            }
            
            Find.WorldFloodFiller.FloodFill(rootTile, (int x) => !Find.World.Impassable(x), delegate(int tile, int traversalDistance)
            {
                if (traversalDistance > proximityLimit)
                {
                    return true;
                }

                if (traversalDistance > 0) {
                    foreach (WorldObject wo in Find.World.worldObjects.ObjectsAt(tile)) {
                        //Debug.Message("Found object {0} at distance of {1}", wo.def.defName, traversalDistance);
                        if (wo.Faction != Faction.OfPlayer) {
                            if (wo is Settlement) {
                                proximityFactor += 6.0f / (float)Math.Pow(traversalDistance, 1.5f);
                                //Debug.Message("This is a settlement, proximity factor is now {0}!", proximityFactor);
                            } else if (wo is Site) {
                                proximityFactor += 2.0f / (traversalDistance*traversalDistance);
                                //Debug.Message("This is a site, proximity factor is now {0}!", proximityFactor);
                            }
                        }
                    }
                }

                return false;
            }, 2147483647, null);

            return proximityFactor;
        }


        public override void Generate(Map map, GenStepParams parms) {
            //Debug.Message("Overridden generate");

            //skip generation due to low blueprints count
            if (SnapshotStoreManager.Instance.StoredSnapshotsCount() < 10) {
                Debug.Message("Skipping ruins gerenation due to low blueprints count.");
                return;
            }

            bool shouldReturn = false;
            foreach (WorldObject wo in Find.World.worldObjects.ObjectsAt(map.Tile))
            {
                //Debug.Message("World Object on generation tile: {0} ({1}).", wo.def.defName, wo.GetType().ToString());
                if (!(wo is Site site)) continue;
                //Debug.Message("Site core: {0}", site.core.def.defName);
                shouldReturn = true;
            }
            
            if (shouldReturn) return;
            
            if (allowInWaterBiome || !map.TileInfo.WaterCovered) {
                RuinsScatterer.PrepareCellUsageFor(map);

                float densityMultiplier = 1.0f;
                float scaleMultiplier = 1.0f;
                float totalDensity = RealRuins_ModSettings.defaultScatterOptions.densityMultiplier;
                currentOptions = RealRuins_ModSettings.defaultScatterOptions.Copy(); //store as instance variable to keep accessible on subsequent ScatterAt calls

                if (RealRuins_ModSettings.defaultScatterOptions.enableProximity) {
                        
                    float proximityFactor = CalculateProximityMultiplier(map);
                    if (proximityFactor < 0.1f && Rand.Chance(0.8f)) {
                        totalDensity = 0;
                    } else {
                        totalDensity *= proximityFactor;
                    }
                    
                    if (totalDensity > 0) {
                        densityMultiplier = Rand.Value * (totalDensity - 0.1f) + 0.1f; //to ensure it is > 0
                        scaleMultiplier = (float)Math.Sqrt(totalDensity / densityMultiplier); //to keep scale^2 * density = const
                    } else {
                        densityMultiplier = 0.0f;
                    }

                    currentOptions.densityMultiplier *= densityMultiplier;
                    currentOptions.minRadius = Math.Min(60, Math.Max(6, (int)(currentOptions.minRadius * scaleMultiplier))); //keep between 6 and 60
                    currentOptions.maxRadius = Math.Min(60, Math.Max(6, (int)(currentOptions.maxRadius * scaleMultiplier))); //keep between 6 and 60
                    currentOptions.scavengingMultiplier *= ((float)Math.Pow(proximityFactor, 0.5f) * 3.0f);
                    currentOptions.deteriorationMultiplier += Math.Min(0.2f, (1.0f / proximityFactor) / 40.0f);

                    if (densityMultiplier > 20.0f) densityMultiplier = 20.0f;
                    while (densityMultiplier * currentOptions.maxRadius > 800) {
                        densityMultiplier *= 0.9f;
                    }
                }
                
                FloatRange per10k = new FloatRange(countPer10kCellsRange.min * totalDensity, countPer10kCellsRange.max * totalDensity);
                int num = CountFromPer10kCells(per10k.RandomInRange, map, -1);

                Debug.Message("total density: {0}{1}, densityMultiplier: {2}, scaleMultiplier: {3}, new density: {4}. new radius: {5}, new per10k: {6}", "", totalDensity, densityMultiplier, scaleMultiplier, currentOptions.densityMultiplier, currentOptions.minRadius, currentOptions.maxRadius, per10k);

                Debug.Message("Spawning {0} ruin chunks", num);


                bool shouldUnpause = false;
                Find.TickManager.Pause();
                if (!Find.TickManager.Paused) {
                    Find.TickManager.TogglePaused();
                    shouldUnpause = true;
                }
                for (int i = 0; i < num; i++) {
                    if (!TryFindScatterCell(map, out IntVec3 result)) {
                        return;
                    }
                    ScatterAt(result, map, 1);
                    usedSpots.Add(result);
                }
                usedSpots.Clear();
                RuinsScatterer.FinalizeCellUsage();
                if (shouldUnpause) {
                    Debug.Message("Finished spawning, unpausing");
                    Find.TickManager.TogglePaused();
                }
            }
        }


        protected override void ScatterAt(IntVec3 loc, Map map, int count = 1) {
            float scavengersActivity = Rand.Value * 0.5f + 0.5f; //later will be based on other settlements proximity
            float ruinsAge = Rand.Range(1, 25);
            float deteriorationDegree = Rand.Value;
            int referenceRadius = Rand.Range(4 + (int)(multiplier / 3), 12 + (int)multiplier);

            new RuinsScatterer().ScatterRuinsAt(loc, map, currentOptions);
        }

        protected override bool CanScatterAt(IntVec3 loc, Map map) {

            return true;
        }
    }

    class GenStep_ScatterLargeRealRuins : GenStep_Scatterer {

        private ScatterOptions currentOptions;
        
        public override int SeedPart {
            get {
                return 74293947;
            }
        }


        public override void Generate(Map map, GenStepParams parms) {
            Find.TickManager.Pause();
            Debug.Message("Overridden LARGE generate");
            if (allowInWaterBiome || !map.TileInfo.WaterCovered) {
                RuinsScatterer.PrepareCellUsageFor(map);

                currentOptions = RealRuins_ModSettings.defaultScatterOptions.Copy(); //store as instance variable to keep accessible on subsequent ScatterAt calls

                currentOptions.minRadius = 200;
                currentOptions.maxRadius = 200;
                currentOptions.scavengingMultiplier = 0.1f;
                currentOptions.deteriorationMultiplier = 0.0f;
                currentOptions.hostileChance = 1.0f;

                currentOptions.minimumCostRequired = 5000;
                currentOptions.minimumDensityRequired = 0.2f;
                currentOptions.minimumSizeRequired = 10000;
                currentOptions.deleteLowQuality = false; //do not delete since we have much higher requirements for base ruins
                currentOptions.shouldKeepDefencesAndPower = true;
                currentOptions.shouldAddSignificantResistance = true;
                currentOptions.shouldCutBlueprint = false;
                currentOptions.shouldAddRaidTriggers = true;
                currentOptions.claimableBlocks = false;
                

                ScatterAt(map.Center, map);
                RuinsScatterer.FinalizeCellUsage();

                float uncoveredCost = currentOptions.uncoveredCost;
                if (uncoveredCost < 0) {
                    if (Rand.Chance(0.5f)) {
                        uncoveredCost = -uncoveredCost; //adding really small party
                    }
                }

                ResolveParams resolveParams = default(ResolveParams);
                resolveParams.rect = CellRect.CenteredOn(map.Center, Math.Min(map.Size.x, map.Size.z) / 2 - 2);
                BaseGen.globalSettings.map = map;
                BaseGen.globalSettings.mainRect = resolveParams.rect;

                if (uncoveredCost > 0) {
                    float pointsCost = uncoveredCost / 10.0f;
                    FloatRange defaultPoints = new FloatRange(pointsCost * 0.2f,
                        Math.Min(10000.0f, pointsCost * 2.0f));
                    Debug.Message("Adding starting party. Remaining points: {0}. Used points range: {1}",
                        currentOptions.uncoveredCost, defaultPoints);

                    if (currentOptions.allowFriendlyRaids) {
                        if (Rand.Chance(0.1f)) {
                            resolveParams.faction = Find.FactionManager.RandomNonHostileFaction();
                        } else {
                            resolveParams.faction = Find.FactionManager.RandomEnemyFaction();
                        }
                    } else {
                        resolveParams.faction = Find.FactionManager.RandomEnemyFaction();
                    }


                    PawnGroupMakerParms pawnGroupMakerParms = new PawnGroupMakerParms();
                    pawnGroupMakerParms.groupKind = PawnGroupKindDefOf.Combat;
                    pawnGroupMakerParms.tile = map.Tile;
                    pawnGroupMakerParms.points = pointsCost;
                    pawnGroupMakerParms.faction = resolveParams.faction;
                    pawnGroupMakerParms.generateFightersOnly = true;
                    pawnGroupMakerParms.raidStrategy = RaidStrategyDefOf.ImmediateAttack;
                    pawnGroupMakerParms.forceOneIncap = false;
                    pawnGroupMakerParms.seed = Rand.Int;

                    List<Pawn> pawns = PawnGroupMakerUtility.GeneratePawns(pawnGroupMakerParms).ToList();
                    CellRect rect = currentOptions.blueprintRect;

                    Debug.Message("Rect: {0}, {1} - {2}, {3}", rect.BottomLeft.x, rect.BottomLeft.z, rect.TopRight.x, rect.TopRight.z);
                    Debug.Message("corner: {0}, {1} size: {2}, {3}", currentOptions.topLeft.x, currentOptions.topLeft.z, currentOptions.roomMap.GetLength(0), currentOptions.roomMap.GetLength(1));

                    CellRect spawnRect = new CellRect(10, 10, map.Size.x - 20, map.Size.y - 20);
                    //CellRect mapRect = new CellRect(currentOptions.topLeft.x, currentOptions.topLeft.z, currentOptions.)

                    foreach (Pawn p in pawns) {

                        IntVec3 location = CellFinder.RandomNotEdgeCell(10, map);
                        bool result = CellFinder.TryFindRandomSpawnCellForPawnNear(location, map, out location);
                        
                        if ( result ) { 
                            GenSpawn.Spawn(p, location, map, Rot4.Random);
                            Debug.Message("Spawned at {0}, {1}", p.Position.x, p.Position.z);
                        } else {
                            Debug.Message("Failed to find a new position");
                        }
                    }

                    LordJob lordJob = new LordJob_AssaultColony(resolveParams.faction, canKidnap: false, canTimeoutOrFlee: Rand.Chance(0.5f));
                    if (lordJob != null) {
                        LordMaker.MakeNewLord(resolveParams.faction, lordJob, map, pawns);
                    }

                }

                BaseGen.symbolStack.Push("chargeBatteries", resolveParams);
                BaseGen.symbolStack.Push("refuel", resolveParams);
                    
                BaseGen.Generate();

            }
        }

        protected override void ScatterAt(IntVec3 loc, Map map, int count = 1) {
            new RuinsScatterer().ScatterRuinsAt(loc, map, currentOptions);
        }

        protected override bool CanScatterAt(IntVec3 loc, Map map) {
            return true;
        }
    }
    
    class GenStep_ScatterMediumRealRuins : GenStep_Scatterer {

        private ScatterOptions currentOptions;
        
        public override int SeedPart {
            get {
                return 74293948;
            }
        }


        public override void Generate(Map map, GenStepParams parms) {
            if (allowInWaterBiome || !map.TileInfo.WaterCovered) {
                Find.TickManager.Pause();
                RuinsScatterer.PrepareCellUsageFor(map);

                currentOptions = RealRuins_ModSettings.defaultScatterOptions.Copy(); //store as instance variable to keep accessible on subsequent ScatterAt calls

                currentOptions.minRadius = 24;
                currentOptions.maxRadius = 50;   
                currentOptions.scavengingMultiplier = 0.5f;
                currentOptions.deteriorationMultiplier = 0.1f;
                currentOptions.hostileChance = 0.8f;
                currentOptions.itemCostLimit = 800;

                currentOptions.minimumCostRequired = 5000;
                currentOptions.minimumDensityRequired = 0.2f;
                currentOptions.minimumSizeRequired = 1000;
                currentOptions.deleteLowQuality = false; //do not delete since we have much higher requirements for base ruins
                currentOptions.shouldKeepDefencesAndPower = true;

                ScatterAt(map.Center, map);
                RuinsScatterer.FinalizeCellUsage();

                ResolveParams resolveParams = default(ResolveParams);
                resolveParams.rect = CellRect.CenteredOn(map.Center, currentOptions.minRadius + (currentOptions.maxRadius - currentOptions.maxRadius) / 2);
                BaseGen.globalSettings.map = map;
                BaseGen.globalSettings.mainRect = resolveParams.rect;

                if (Rand.Chance(0.2f)) {
                    float pointsCost = Math.Abs(Rand.Gaussian()) * 500; 

                    resolveParams.faction = Find.FactionManager.RandomEnemyFaction();
                    resolveParams.singlePawnLord = LordMaker.MakeNewLord(resolveParams.faction,
                        new LordJob_AssaultColony(resolveParams.faction, false, false, true, true), map, null);

                    resolveParams.pawnGroupKindDef = (resolveParams.pawnGroupKindDef ?? PawnGroupKindDefOf.Settlement);

                    if (resolveParams.pawnGroupMakerParams == null) {
                        resolveParams.pawnGroupMakerParams = new PawnGroupMakerParms();
                        resolveParams.pawnGroupMakerParams.tile = map.Tile;
                        resolveParams.pawnGroupMakerParams.faction = resolveParams.faction;
                        PawnGroupMakerParms pawnGroupMakerParams = resolveParams.pawnGroupMakerParams;
                        pawnGroupMakerParams.points = pointsCost;
                    }

                    BaseGen.symbolStack.Push("pawnGroup", resolveParams);

                }

                BaseGen.symbolStack.Push("chargeBatteries", resolveParams);
                BaseGen.symbolStack.Push("refuel", resolveParams);
                    
                BaseGen.Generate();
            }
        }

        protected override void ScatterAt(IntVec3 loc, Map map, int count = 1) {
            new RuinsScatterer().ScatterRuinsAt(loc, map, currentOptions);
        }

        protected override bool CanScatterAt(IntVec3 loc, Map map) {
            return true;
        }
    }
    
}
