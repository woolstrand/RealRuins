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
        int minTriggerTimeout = 0;

        public MilitaryForcesGenerator(float militaryPower, int minimalTriggerFiringTimeout = 0) {
            if (militaryPower > 1) {
                this.militaryPower = militaryPower;
                minTriggerTimeout = minimalTriggerFiringTimeout;
            }
        }

        public override void GenerateForces(Map map, ResolveParams rp) {
       
        }

        public override void GenerateStartingParty(Map map, ResolveParams rp) {
            ScatterOptions currentOptions = rp.GetCustom<ScatterOptions>(Constants.ScatterOptions);
            float uncoveredCost = currentOptions.uncoveredCost;

            int points = (int)(uncoveredCost / (10 * militaryPower));
            int initialGroup = 0;
            if (points > 10000) {
                initialGroup = Rand.Range(5000, 10000);
            } else {
                initialGroup = points;
            }
            Debug.Log(Debug.ForceGen, "Military gen: uncoveredCost {0}, military power: {1}, total points allowed: {2}", uncoveredCost, militaryPower, points);

            points -= initialGroup;
            SpawnGroup(initialGroup, rp.rect, rp.faction, map);
            Debug.Log(Debug.ForceGen, "Initial group of {0} spawned, {1} points left for triggers", initialGroup, points);

            while (points > 0) {
                IntVec3 mapLocation = rp.rect.RandomCell;
                if (!mapLocation.InBounds(map)) continue;

                ThingDef raidTriggerDef = ThingDef.Named("RaidTrigger");
                RaidTrigger trigger = ThingMaker.MakeThing(raidTriggerDef) as RaidTrigger;

                trigger.faction = rp.faction;
                trigger.SetTimeouts(0, 300);

                int raidMaxPoints = (int)(10000 / Math.Max(Math.Sqrt(d: militaryPower), 1.0));
                trigger.value = Math.Abs(Rand.Gaussian()) * raidMaxPoints + Rand.Value * raidMaxPoints + 250.0f;
                if (trigger.value > 10000) trigger.value = Rand.Range(8000, 11000); //sanity cap. against some beta-poly bases.
                points -= (int)trigger.value;

                GenSpawn.Spawn(trigger, mapLocation, map);
                Debug.Log(Debug.ForceGen, "Spawned trigger at {0}, {1} for {2} points, autofiring after {3} rare ticks", mapLocation.x, mapLocation.z, trigger.value, 0);
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
