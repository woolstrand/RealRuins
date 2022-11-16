using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using RimWorld.BaseGen;
using Verse;
using Verse.AI.Group;

namespace RealRuins {
    class BattleRoyaleForcesGenerator : AbstractDefenderForcesGenerator {
        public override void GenerateForces(Map map, ResolveParams rp, ScatterOptions options) {
            if (options == null) return;

            int addedTriggers = 0;
            float ratio = 10;
            float remainingCost = options.uncoveredCost * (Rand.Value + 0.5f); //cost estimation as seen by other factions
            Debug.Log(Debug.ForceGen, "Running battle royale force generation with remaining cost of {0} (while uncovered is {1})", remainingCost, options.uncoveredCost);

            float initialCost = remainingCost;

            int triggersAbsoluteMaximum = 100;

            while (remainingCost > 0) {

                IntVec3 mapLocation = rp.rect.RandomCell;
                if (!mapLocation.InBounds(map)) continue;

                ThingDef raidTriggerDef = ThingDef.Named("RaidTrigger");
                RaidTrigger trigger = ThingMaker.MakeThing(raidTriggerDef) as RaidTrigger;

                if (options.allowFriendlyRaids) {
                    if (Rand.Chance(0.2f)) {
                        trigger.faction = Find.FactionManager.RandomNonHostileFaction();
                    } else {
                        trigger.faction = Find.FactionManager.RandomEnemyFaction();
                    }
                } else {
                    trigger.faction = Find.FactionManager.RandomEnemyFaction();
                }

                int raidMaxPoints = (int)(remainingCost / ratio);
                float raidValue = Math.Abs(Rand.Gaussian()) * raidMaxPoints + Rand.Value * raidMaxPoints + 250.0f;
                if (raidValue > 10000) raidValue = Rand.Range(8000, 11000); //sanity cap. against some beta-poly bases.
                remainingCost -= raidValue * ratio;

                trigger.value = ScalePointsToDifficulty(raidValue);

                GenSpawn.Spawn(trigger, mapLocation, map);
                Debug.Log(Debug.ForceGen, "Spawned trigger at {0}, {1} for {2} points, autofiring after {3} rare ticks", mapLocation.x, mapLocation.z, trigger.value, 0);
                addedTriggers++;

                options.uncoveredCost = Math.Abs(remainingCost);

                if (addedTriggers > triggersAbsoluteMaximum) {
                    if (remainingCost < initialCost * 0.2f) {
                        if (Rand.Chance(0.1f)) {
                            if (remainingCost > 100000) {
                                remainingCost = Rand.Range(80000, 110000);
                            }
                            return;
                        }
                    }
                }
            }
        }

        public override void GenerateStartingParty(Map map, ResolveParams rp, ScatterOptions currentOptions) {
            float uncoveredCost = currentOptions.uncoveredCost;

            if (uncoveredCost > 0 || currentOptions.startingPartyPoints > 0) {
                float pointsCost = 0;
                if (currentOptions.startingPartyPoints > 0) {
                    pointsCost = currentOptions.startingPartyPoints;
                } else {
                    pointsCost = uncoveredCost / 10.0f;
                    FloatRange defaultPoints = new FloatRange(pointsCost * 0.7f,
                        Math.Min(12000.0f, pointsCost * 2.0f));
                    Debug.Log(Debug.ForceGen, "Adding starting party. Remaining points: {0}. Used points range: {1}",
                        currentOptions.uncoveredCost, defaultPoints);

                }
                pointsCost = ScalePointsToDifficulty(pointsCost);
                ScatterStartingParties((int)pointsCost, currentOptions.allowFriendlyRaids, map);
            }

        }

        private void ScatterStartingParties(int points, bool allowFriendly, Map map) {

            while (points > 0) {
                int pointsUsed = Rand.Range(200, Math.Min(3000, points / 5));

                IntVec3 rootCell = CellFinder.RandomNotEdgeCell(30, map);
                CellFinder.TryFindRandomSpawnCellForPawnNear(rootCell, map, out IntVec3 result);
                if (result.IsValid) {
                    Faction faction = null;
                    if (allowFriendly) {
                        Find.FactionManager.TryGetRandomNonColonyHumanlikeFaction(out faction, false);
                    } else {
                        faction = Find.FactionManager.RandomEnemyFaction();
                    }

                    if (faction == null) faction = Find.FactionManager.AllFactions.RandomElement();

                    SpawnGroup(pointsUsed, new CellRect(result.x - 10, result.z - 10, 20, 20), faction, map);
                    points -= pointsUsed;
                }
            }
        }

        private void SpawnGroup(int points, CellRect locationRect, Faction faction, Map map) {
            PawnGroupMakerParms pawnGroupMakerParms = new PawnGroupMakerParms();
            pawnGroupMakerParms.groupKind = PawnGroupKindDefOf.Combat;
            pawnGroupMakerParms.tile = map.Tile;
            pawnGroupMakerParms.points = points;
            pawnGroupMakerParms.faction = faction;
            pawnGroupMakerParms.generateFightersOnly = true;
            pawnGroupMakerParms.raidStrategy = RaidStrategyDefOf.ImmediateAttack;
            pawnGroupMakerParms.seed = Rand.Int;

            List<Pawn> pawns = PawnGroupMakerUtility.GeneratePawns(pawnGroupMakerParms).ToList();
            CellRect rect = locationRect;

            if (pawns == null) {
                Debug.Warning(Debug.ForceGen, "Pawns list is null");
            }

            foreach (Pawn p in pawns) {

                bool result = CellFinder.TryFindRandomSpawnCellForPawnNear(locationRect.RandomCell, map, out IntVec3 location);

                if (result) {
                    GenSpawn.Spawn(p, location, map, Rot4.Random);
                }
            }

            LordJob lordJob = null;
            lordJob = new LordJob_AssaultColony(faction, canKidnap: false, canTimeoutOrFlee: Rand.Chance(0.5f));


            if (lordJob != null) {
                LordMaker.MakeNewLord(faction, lordJob, map, pawns);
            }
        }
    }
}
