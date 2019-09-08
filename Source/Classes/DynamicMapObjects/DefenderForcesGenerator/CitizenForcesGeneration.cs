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

        public override void GenerateForces(Map map, ResolveParams rp) {

        }

        public override void GenerateStartingParty(Map map, ResolveParams rp) {
            ScatterOptions currentOptions = rp.GetCustom<ScatterOptions>(Constants.ScatterOptions);
            float uncoveredCost = currentOptions.uncoveredCost;

            Debug.Message("citizen gen: uncoveredCost {0}. rect {1}", uncoveredCost, rp.rect);
            int points = (int)(uncoveredCost / 10);
            SpawnGroup(points, rp.rect, rp.faction, map);
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
                Debug.Message("Pawns list is null");
            } else {
                Debug.Message("Pawns list contains {0} records", pawns.Count);
            }

            foreach (Pawn p in pawns) {

                bool result = CellFinder.TryFindRandomCellInsideWith(locationRect, (IntVec3 x) => x.Standable(map), out IntVec3 location);


                if (result) {
                    GenSpawn.Spawn(p, location, map, Rot4.Random);
                } else {
                    Debug.Message("Can't find location!");
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
