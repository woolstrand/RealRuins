using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;

using System.Reflection;

using RimWorld;
using RimWorld.Planet;
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
                        Debug.Message("Found object {0} at distance of {1}", wo.def.defName, traversalDistance);
                        if (wo.Faction != Faction.OfPlayer) {
                            if (wo is Settlement) {
                                proximityFactor += 8.0f / (float)Math.Pow(traversalDistance, 1.5f);
                                Debug.Message("This is a settlement, proximity factor is now {0}!", proximityFactor);
                            } else if (wo is Site) {
                                proximityFactor += 4.0f / (traversalDistance*traversalDistance);
                                Debug.Message("This is a site, proximity factor is now {0}!", proximityFactor);
                            }
                        }
                    }
                }

                return false;
            }, 2147483647, null);

            return proximityFactor;
        }


        public override void Generate(Map map, GenStepParams parms) {
            Debug.Message("Overridden generate");
            bool shouldReturn = false;
            foreach (WorldObject wo in Find.World.worldObjects.ObjectsAt(map.Tile))
            {
                Debug.Message("World Object on generation tile: {0} ({1}).", wo.def.defName, wo.GetType().ToString());
                if (!(wo is Site site)) continue;
                Debug.Message("Site core: {0}", site.core.def.defName);
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
                    currentOptions.referenceRadiusAverage = Math.Min(60, Math.Max(6, (int)(currentOptions.referenceRadiusAverage * scaleMultiplier))); //keep between 6 and 60
                    currentOptions.scavengingMultiplier *= ((float)Math.Pow(proximityFactor, 0.5f) * 3.0f);
                    currentOptions.deteriorationMultiplier += Math.Min(0.2f, (1.0f / proximityFactor) / 40.0f);

                    if (densityMultiplier > 20.0f) densityMultiplier = 20.0f;
                    while (densityMultiplier * currentOptions.referenceRadiusAverage > 800) {
                        densityMultiplier *= 0.9f;
                    }
                }
                
                FloatRange per10k = new FloatRange(countPer10kCellsRange.min * totalDensity, countPer10kCellsRange.max * totalDensity);
                int num = CountFromPer10kCells(per10k.RandomInRange, map, -1);

                Debug.Message("total density: {0}{1}, densityMultiplier: {2}, scaleMultiplier: {3}, new density: {4}. new radius: {5}, new per10k: {6}", "", totalDensity, densityMultiplier, scaleMultiplier, currentOptions.densityMultiplier, currentOptions.referenceRadiusAverage, per10k);

                Debug.Message("Spawning {0} ruin chunks", num);

                for (int i = 0; i < num; i++) {
                    if (!TryFindScatterCell(map, out IntVec3 result)) {
                        return;
                    }
                    ScatterAt(result, map, 1);
                    usedSpots.Add(result);
                }
                usedSpots.Clear();
                RuinsScatterer.FinalizeCellUsage();
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
            Debug.Message("Overridden LARGE generate");
            if (allowInWaterBiome || !map.TileInfo.WaterCovered) {
                RuinsScatterer.PrepareCellUsageFor(map);

                currentOptions = RealRuins_ModSettings.defaultScatterOptions.Copy(); //store as instance variable to keep accessible on subsequent ScatterAt calls

                currentOptions.referenceRadiusAverage = Rand.Range(90, 120);
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
                

                ScatterAt(map.Center, map);
                RuinsScatterer.FinalizeCellUsage();

                FloatRange defaultPoints = new FloatRange(200.0f, 3000.0f);
                
                ResolveParams resolveParams = default(ResolveParams);
                resolveParams.rect = CellRect.CenteredOn(map.Center, currentOptions.referenceRadiusAverage);
                resolveParams.faction = Find.FactionManager.RandomEnemyFaction();
                resolveParams.singlePawnLord = LordMaker.MakeNewLord(resolveParams.faction, new LordJob_DefendBase(resolveParams.faction, resolveParams.rect.CenterCell), map, null);;
                resolveParams.pawnGroupKindDef = (resolveParams.pawnGroupKindDef ?? PawnGroupKindDefOf.Settlement);
                
                if (resolveParams.pawnGroupMakerParams == null)
                {
                    resolveParams.pawnGroupMakerParams = new PawnGroupMakerParms();
                    resolveParams.pawnGroupMakerParams.tile = map.Tile;
                    resolveParams.pawnGroupMakerParams.faction = resolveParams.faction;
                    PawnGroupMakerParms pawnGroupMakerParams = resolveParams.pawnGroupMakerParams;
                    pawnGroupMakerParams.points = defaultPoints.RandomInRange;
                    resolveParams.pawnGroupMakerParams.inhabitants = true;
                }

                BaseGen.globalSettings.map = map;
                BaseGen.globalSettings.mainRect = resolveParams.rect;
                BaseGen.symbolStack.Push("pawnGroup", resolveParams);

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
