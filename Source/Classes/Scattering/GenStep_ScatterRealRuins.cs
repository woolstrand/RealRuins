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

        // Proximity: closer to settlements = more and larger ruin chunks.
        private const int proximityLimit = 16; // distance search limit; "no settlement around" counts as this distance

        // Players can't settle next to a settlement and rarely settle closer than 4 tiles (relations drop), so closer maps
        // get the same chunk count as at 4 tiles.
        private const int closestCountedDistance = 4;

        private static float DensityCurve(float distance) => (float)(Math.Exp(1.0 / (distance / 10.0 + 0.3)) - 0.7);
        private static float ScaleCurve(float distance) => (float)(Math.Exp(1 / (distance / 5 + 0.5)) - 0.3);

        // Chunk count is divided by the average extra coverage the curves give over settle-able distances (count x chunk area,
        // about 2.0). So with any density setting the covered area averaged over distances 4-16 stays what it was when proximity
        // affected chunk size only: ruins are denser near settlements and sparser far away, not more of them overall.
        private static readonly float countNormalization =
            Enumerable.Range(closestCountedDistance, proximityLimit - closestCountedDistance + 1).Sum(d => DensityCurve(d) * ScaleCurve(d) * ScaleCurve(d)) /
            Enumerable.Range(closestCountedDistance, proximityLimit - closestCountedDistance + 1).Sum(d => ScaleCurve(d) * ScaleCurve(d));

        // A non-player settlement or site on the map's own tile (faction base attack, quest site).
        private static bool IsSettlementOrSiteOnTile(Map map)
        {
            foreach (WorldObject wo in Find.World.worldObjects.ObjectsAt(map.Tile)) {
                if (wo.Faction != Faction.OfPlayer && (wo is Settlement || wo is Site)) return true;
            }
            return false;
        }

        public float CalculateDistanceToNearestSettlement(Map map)
        {
            PlanetTile rootTile = map.Tile;
            int minDistance = proximityLimit;

            rootTile.Layer.Filler.FloodFill(rootTile, (PlanetTile x) => !Find.World.Impassable(x), delegate(PlanetTile tile, int traversalDistance)
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
            //skip generation due to low blueprints count
            if (SnapshotStoreManager.Instance.StoredSnapshotsCount() < 10) {
                Debug.Warning(Debug.Scatter, "Skipping ruins gerenation due to low blueprints count.");
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

            //Skip generation on maps which are a ruins site themselves.
            //Caravan small ruins (SmallRuinsWorldObject) are deliberately not skipped: regular chunks around the main ruin are intended.
            foreach (WorldObject wo in Find.World.worldObjects.ObjectsAt(map.Tile))
            {
                if (wo is RealRuinsPOIWorldObject || wo is AbandonedBaseWorldObject) return;
            }
            //On game start the starting POI is already removed from the world and replaced by the player's settlement,
            //so recognize the POI map by its generator instead.
            if (map.generatorDef?.genSteps?.Any(step => step.genStep is GenStep_ScatterPOIRuins) == true) return;

            if (!map.TileInfo.WaterCovered) {

                float densityMultiplier = 1.0f;
                float scaleMultiplier = 1.0f;
                float countMultiplier = 1.0f;
                float distanceToSettlement = 0.0f;
                ScatterOptions options = ScatterOptionsFactory.ForGenericRuins();
                PipelineSettings settings = PipelineSettings.FromModSettings();

                if (options.enableProximity) {
                    bool siteOnTile = IsSettlementOrSiteOnTile(map);
                    distanceToSettlement = siteOnTile ? 1.0f : CalculateDistanceToNearestSettlement(map); //on tile: maximum size, but not flat area
                    if (distanceToSettlement >= proximityLimit && Rand.Chance(0.5f)) {
                        countMultiplier = 0.0f;
                    } else {
                        densityMultiplier = DensityCurve(distanceToSettlement);
                        scaleMultiplier = ScaleCurve(distanceToSettlement);
                        //The map of a base or site itself keeps the plain count: its chunks are already the largest, more would bury it.
                        countMultiplier = siteOnTile ? 1.0f : DensityCurve(Math.Max(distanceToSettlement, closestCountedDistance)) / countNormalization;

                        options.minRadius = Math.Min(60, Math.Max(6, (int)(options.minRadius * scaleMultiplier))); //keep between 6 and 60
                        options.maxRadius = Math.Min(60, Math.Max(6, (int)(options.maxRadius * scaleMultiplier))); //keep between 6 and 60
                        options.scavengingMultiplier *= scaleMultiplier * densityMultiplier;
                        options.deteriorationMultiplier += Math.Min(0.2f, (1.0f / (scaleMultiplier * densityMultiplier * 3)));
                    }
                    options.densityMultiplier *= countMultiplier;
                }

                //number of ruins based on density settings and proximity
                float totalDensity = options.densityMultiplier;
                int num = (int)Math.Ceiling((int)((float)map.Area / 10000.0f) * Rand.Range(1 * totalDensity, 2 * totalDensity));

                Debug.Log(Debug.Scatter, "dist {0}, dens {1} (count x{2}), scale x{3} ({4}-{5}), scav {6}, deter {7}", distanceToSettlement, options.densityMultiplier, countMultiplier, scaleMultiplier, options.minRadius, options.maxRadius, options.scavengingMultiplier, options.deteriorationMultiplier);
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
                        //Each chunk gets its own context (with its own options copy and state) because each chunk has separate location, size, maps, etc.
                        ResolveParams rp = default(ResolveParams);

                        List<AbstractDefenderForcesGenerator> generators = new List<AbstractDefenderForcesGenerator>();
                        if (Rand.Chance(options.hostileChance)) {
                            if (Rand.Chance(0.8f)) {
                                generators = new List<AbstractDefenderForcesGenerator> { new AnimalInhabitantsForcesGenerator() };
                            } else {
                                generators = new List<AbstractDefenderForcesGenerator> { new MechanoidsForcesGenerator(0) };
                            }
                        }
                        rp.faction = Find.FactionManager.OfAncientsHostile;
                        var center = CellFinder.RandomNotEdgeCell(10, map);
                        rp.rect = new CellRect(center.x, center.z, 1, 1); //after generation will be extended to a real size
                        RuinsScatterer.Scatter(rp, new ScatterContext(options, settings), coverageMap, generators);
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

    // Abandoned base site (Pristine ruins event and quest). The blueprint was picked when the site was created.
    class GenStep_ScatterLargeRealRuins : GenStep_RealRuinsSite {

        public override int SeedPart {
            get {
                return 74293947;
            }
        }

        protected override SitePlan MakePlan(Map map) {
            //Only the file name is stored on the site; see RuinsScatterer.Scatter for why the blueprint is loaded again here.
            RuinedBaseComp ruinedBaseComp = map.Parent.GetComponent<RuinedBaseComp>();
            string filename = ruinedBaseComp?.blueprintFileName;
            Debug.Log(Debug.Scatter, "Large Ruins - Preselected file name is {0}", filename);

            SitePlan plan = new SitePlan {
                options = ScatterOptionsFactory.ForAbandonedBase(filename, ruinedBaseComp?.currentCapCost ?? -1, ruinedBaseComp?.raidersActivity ?? -1),
                faction = Find.FactionManager.OfAncientsHostile,
                rect = new CellRect(0, 0, map.Size.x, map.Size.z),
            };

            if (RealRuins_ModSettings.forceMultiplier != 0) {
                plan.generators = new List<AbstractDefenderForcesGenerator> { new BattleRoyaleForcesGenerator() };
            }
            return plan;
        }
    }

    // Single medium-sized ruin spotted by a caravan (CaravanSmallRuinsWorldObject). Random blueprint, sometimes a raid party.
    class GenStep_ScatterMediumRealRuins : GenStep_RealRuinsSite {

        public override int SeedPart {
            get {
                return 74293948;
            }
        }

        protected override SitePlan MakePlan(Map map) {
            Debug.Log(Debug.Scatter, "Medium generate");
            SitePlan plan = new SitePlan {
                options = ScatterOptionsFactory.ForCaravanSmallRuins(),
                faction = Find.FactionManager.OfAncientsHostile,
                rect = new CellRect(0, 0, map.Size.x, map.Size.z),
                ensureCanHoldRoof = false,
            };
            return plan;
        }

        // Sometimes a raid party of a random enemy faction is already there.
        protected override void PushExtraSymbols(Map map, ResolveParams siteParams, SitePlan plan) {
            if (!Rand.Chance(0.5f * Find.Storyteller.difficulty.threatScale)) return;

            float pointsCost = Math.Abs(Rand.Gaussian()) * 500 * Find.Storyteller.difficulty.threatScale;

            ResolveParams rp = siteParams; //copy!
            rp.faction = Find.FactionManager.RandomEnemyFaction();
            rp.singlePawnLord = LordMaker.MakeNewLord(rp.faction, new LordJob_AssaultColony(rp.faction, false, false, true, true), map, null);
            rp.pawnGroupKindDef = PawnGroupKindDefOf.Settlement;
            rp.pawnGroupMakerParams = new PawnGroupMakerParms {
                tile = map.Tile,
                faction = rp.faction,
                points = pointsCost,
            };

            BaseGen.symbolStack.Push("pawnGroup", rp);
        }
    }
    
}
