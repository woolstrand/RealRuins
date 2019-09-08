using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using RimWorld.BaseGen;
using Verse;
using Verse.AI.Group;

namespace RealRuins {
    class MilitaryForcesGenerator : AbstractDefenderForcesGenerator {

        float militaryPower = 1;

        public MilitaryForcesGenerator(float militaryPower) {
            if (militaryPower > 1) {
                this.militaryPower = militaryPower;
            }
        }

        public override void GenerateForces(Map map, ResolveParams rp) {
            ScatterOptions options = rp.GetCustom<ScatterOptions>(Constants.ScatterOptions);
            if (options == null) return;

            int addedTriggers = 0;
            float ratio = 10;
            float remainingCost = options.uncoveredCost * (Rand.Value * 0.5f + 0.5f);

            float initialCost = remainingCost;

            int triggersAbsoluteMaximum = 100;

            while (remainingCost > 0) {

                IntVec3 mapLocation = rp.rect.RandomCell;
                if (!mapLocation.InBounds(map)) continue;

                ThingDef raidTriggerDef = ThingDef.Named("RaidTrigger");
                RaidTrigger trigger = ThingMaker.MakeThing(raidTriggerDef) as RaidTrigger;

                trigger.faction = rp.faction;

                int raidMaxPoints = (int)(remainingCost / ratio);
                trigger.value = Math.Abs(Rand.Gaussian()) * raidMaxPoints + Rand.Value * raidMaxPoints + 250.0f;
                if (trigger.value > 10000) trigger.value = Rand.Range(8000, 11000); //sanity cap. against some beta-poly bases.
                remainingCost -= trigger.value * ratio;

                GenSpawn.Spawn(trigger, mapLocation, map);
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
                Debug.Log("military gen: added {0} triggers", addedTriggers);
            }

        }

        public override void GenerateStartingParty(Map map, ResolveParams rp) {
            ScatterOptions currentOptions = rp.GetCustom<ScatterOptions>(Constants.ScatterOptions);
            float uncoveredCost = currentOptions.uncoveredCost;

            Debug.Log("army gen: uncoveredCost {0}", uncoveredCost);
            int points = (int)(uncoveredCost / (10 * militaryPower));
            int initialGroup = 0;
            if (points > 10000) {
                initialGroup = Rand.Range(5000, 10000);
            } else {
                initialGroup = points;
            }
            points -= initialGroup;
            SpawnGroup(initialGroup, rp.rect, rp.faction, map);

            while (points > 0) {
                IntVec3 mapLocation = rp.rect.RandomCell;
                if (!mapLocation.InBounds(map)) continue;

                ThingDef raidTriggerDef = ThingDef.Named("RaidTrigger");
                RaidTrigger trigger = ThingMaker.MakeThing(raidTriggerDef) as RaidTrigger;

                trigger.faction = rp.faction;

                int raidMaxPoints = (int)(10000 / Math.Max(Math.Sqrt(d: militaryPower), 1.0));
                trigger.value = Math.Abs(Rand.Gaussian()) * raidMaxPoints + Rand.Value * raidMaxPoints + 250.0f;
                if (trigger.value > 10000) trigger.value = Rand.Range(8000, 11000); //sanity cap. against some beta-poly bases.
                points -= (int)trigger.value;

                GenSpawn.Spawn(trigger, mapLocation, map);
            }
        }

        private void SpawnGroup(int points, CellRect locationRect, Faction faction, Map map) {
            PawnGroupMakerParms pawnGroupMakerParms = new PawnGroupMakerParms();
            pawnGroupMakerParms.groupKind = PawnGroupKindDefOf.Settlement;
            pawnGroupMakerParms.tile = map.Tile;
            pawnGroupMakerParms.points = points;
            pawnGroupMakerParms.faction = faction;
            pawnGroupMakerParms.generateFightersOnly = false;
            pawnGroupMakerParms.forceOneIncap = false;
            pawnGroupMakerParms.seed = Rand.Int;

            List<Pawn> pawns = PawnGroupMakerUtility.GeneratePawns(pawnGroupMakerParms).ToList();
            CellRect rect = locationRect;

            if (pawns == null) {
                Debug.Warning("Pawns list is null");
            } else {
                Debug.Log("Pawns list contains {0} records", pawns.Count);
            }

            foreach (Pawn p in pawns) {

                bool result = CellFinder.TryFindRandomCellInsideWith(locationRect, (IntVec3 x) => x.Standable(map), out IntVec3 location);


                if (result) {
                    GenSpawn.Spawn(p, location, map, Rot4.Random);
                } else {
                    Debug.Warning("Can't find location!");
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
