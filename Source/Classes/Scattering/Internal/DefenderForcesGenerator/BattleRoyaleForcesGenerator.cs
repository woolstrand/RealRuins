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
        public override void GenerateForces(Map map, ResolveParams rp, ScatterContext context) {
            SpawnRaidTriggers(map, rp, context, "battle royale");
        }

        protected override Faction PickTriggerFaction(ResolveParams rp, ScatterContext context) {
            if (context.options.allowFriendlyRaids && Rand.Chance(0.2f)) {
                return Find.FactionManager.RandomNonHostileFaction();
            }
            return Find.FactionManager.RandomEnemyFaction();
        }

        public override void GenerateStartingParty(Map map, ResolveParams rp, ScatterContext context) {
            float uncoveredCost = context.state.uncoveredCost;

            if (uncoveredCost > 0 || context.options.startingPartyPoints > 0) {
                float pointsCost = 0;
                if (context.options.startingPartyPoints > 0) {
                    pointsCost = context.options.startingPartyPoints;
                } else {
                    pointsCost = uncoveredCost / 10.0f;
                    FloatRange defaultPoints = new FloatRange(pointsCost * 0.7f,
                        Math.Min(12000.0f, pointsCost * 2.0f));
                    Debug.Log(Debug.ForceGen, "Adding starting party. Remaining points: {0}. Used points range: {1}",
                        context.state.uncoveredCost, defaultPoints);

                }
                pointsCost = ScalePointsToDifficulty(pointsCost, context);
                ScatterStartingParties((int)pointsCost, context.options.allowFriendlyRaids, map);
            }

        }

        private void ScatterStartingParties(int points, bool allowFriendly, Map map) {

            int failedAttempts = 0;
            while (points > 0) {
                int pointsUsed = Rand.Range(200, Math.Min(3000, points / 5));

                IntVec3 rootCell = CellFinder.RandomNotEdgeCell(30, map);
                CellFinder.TryFindRandomSpawnCellForPawnNear(rootCell, map, out IntVec3 result);
                if (!result.IsValid) {
                    //points are spent only on success, so a map without spawn cells would loop forever
                    if (++failedAttempts >= 50) {
                        Debug.Warning(Debug.ForceGen, "Could not find a spawn cell for starting parties, {0} points left unused", points);
                        return;
                    }
                    continue;
                }

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
