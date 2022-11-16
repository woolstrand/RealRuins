using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using RimWorld.BaseGen;
using Verse;
using Verse.AI.Group;

namespace RealRuins{
    class CitizenForcesGeneration : AbstractDefenderForcesGenerator {

        private int bedCount;
        private Faction faction;

        public CitizenForcesGeneration(int bedCount, Faction factionOverride = null) {
            this.bedCount = bedCount;
            faction = factionOverride;
        }

        public override void GenerateForces(Map map, ResolveParams rp, ScatterOptions options) {
            if (options == null) return;

            int addedTriggers = 0;
            float ratio = 10;
            float remainingCost = options.uncoveredCost * (Rand.Value + 0.5f); //cost estimation as seen by other factions
            Debug.Log(Debug.ForceGen, "Running citizen force generation with remaining cost of {0} (while uncovered is {1})", remainingCost, options.uncoveredCost);

            float initialCost = remainingCost;

            int triggersAbsoluteMaximum = 100;

            while (remainingCost > 0) {

                IntVec3 mapLocation = rp.rect.RandomCell;
                if (!mapLocation.InBounds(map)) continue;

                ThingDef raidTriggerDef = ThingDef.Named("RaidTrigger");
                RaidTrigger trigger = ThingMaker.MakeThing(raidTriggerDef) as RaidTrigger;

                trigger.faction = rp.faction;

                int raidMaxPoints = (int)(remainingCost / ratio);
                float raidValue = Math.Abs(Rand.Gaussian()) * raidMaxPoints + Rand.Value * raidMaxPoints + 250.0f;
                if (raidValue > 10000) raidValue = Rand.Range(8000, 11000); //sanity cap. against some beta-poly bases.
                remainingCost -= raidValue * ratio;

                int timeout = (int)Math.Abs(Rand.Gaussian(0, 75));
                trigger.value = ScalePointsToDifficulty(raidValue);
                trigger.SetTimeouts(timeout, 200);

                GenSpawn.Spawn(trigger, mapLocation, map);
                Debug.Log(Debug.ForceGen, "Spawned trigger at {0}, {1} for {2} points, autofiring after {3} rare ticks", mapLocation.x, mapLocation.z, trigger.value, timeout);
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

            Faction faction = rp.faction;
            if (this.faction != null) {
                faction = this.faction;
            }
            if (faction == null) {
                faction = Find.FactionManager.RandomEnemyFaction(true, true, false);
            }

            Debug.Log(Debug.ForceGen, "Citizen force gen: uncoveredCost {0}. rect {1}", uncoveredCost, rp.rect);
            int maxPoints = Math.Min((int)(uncoveredCost / 10), 5000);
            SpawnGroup((int)ScalePointsToDifficulty(maxPoints), rp.rect, faction, map, Rand.Range(Math.Max(2, (int)(bedCount * 0.7)), (int)(bedCount * 1.5)));
            currentOptions.uncoveredCost -= maxPoints * 10;
        }

        private void SpawnGroup(int points, CellRect locationRect, Faction faction, Map map, int countCap) {

            PawnGroupKindDef groupKind;
            if (faction.def.pawnGroupMakers.Where((PawnGroupMaker gm) => gm.kindDef == PawnGroupKindDefOf.Settlement).Count() > 0) {
                groupKind = PawnGroupKindDefOf.Settlement;
            } else {
                groupKind = faction.def.pawnGroupMakers.RandomElement().kindDef;
            }

            PawnGroupMakerParms pawnGroupMakerParms = new PawnGroupMakerParms();
            pawnGroupMakerParms.groupKind = groupKind;
            pawnGroupMakerParms.tile = map.Tile;
            pawnGroupMakerParms.points = points;
            pawnGroupMakerParms.faction = faction;
            pawnGroupMakerParms.generateFightersOnly = false;
            pawnGroupMakerParms.seed = Rand.Int;

            List<Pawn> pawns = PawnGroupMakerUtility.GeneratePawns(pawnGroupMakerParms).ToList();
            CellRect rect = locationRect;

            if (pawns == null) {
                Debug.Warning(Debug.ForceGen, "Generating starting party: Pawns list is null");
            } else {
                Debug.Log(Debug.ForceGen, "Pawns list contains {0} records. Cap is {1}", pawns.Count, countCap);
            }

            while (pawns.Count > countCap) {
                pawns.Remove(pawns.RandomElement());
            }

            foreach (Pawn p in pawns) {

                bool result = CellFinder.TryFindRandomCellInsideWith(locationRect, (IntVec3 x) => x.Standable(map), out IntVec3 location);


                if (result) {
                    GenSpawn.Spawn(p, location, map, Rot4.Random);
                    Debug.Extra(Debug.ForceGen, "Spawned pawn {0} at {1}, {2}", p.Name, location.x, location.z);
                } else {
                    Debug.Warning(Debug.ForceGen, "Can't find spawning location for defender pawn!");
                }
            }

            LordJob lordJob = null;
            lordJob = new LordJob_DefendBase(faction, rect.CenterCell);

            if (lordJob != null) {
                LordMaker.MakeNewLord(faction, lordJob, map, pawns);
            }
        }
    }
}
