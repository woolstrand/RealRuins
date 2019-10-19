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

        public override void GenerateForces(Map map, ResolveParams rp) {

        }

        public override void GenerateStartingParty(Map map, ResolveParams rp) {
            ScatterOptions currentOptions = rp.GetCustom<ScatterOptions>(Constants.ScatterOptions);
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
            pawnGroupMakerParms.forceOneIncap = false;
            pawnGroupMakerParms.seed = Rand.Int;

            List<Pawn> pawns = PawnGroupMakerUtility.GeneratePawns(pawnGroupMakerParms).ToList();
            CellRect rect = locationRect;

            if (pawns == null) {
                Debug.Warning(Debug.ForceGen, "Generating starting party: Pawns list is null");
            } else {
                Debug.Log(Debug.ForceGen, "Pawns list contains {0} records", pawns.Count);
            }

            while (pawns.Count > countCap) {
                pawns.Remove(pawns.RandomElement());
            }

            foreach (Pawn p in pawns) {

                bool result = CellFinder.TryFindRandomCellInsideWith(locationRect, (IntVec3 x) => x.Standable(map), out IntVec3 location);


                if (result) {
                    GenSpawn.Spawn(p, location, map, Rot4.Random);
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
