using RimWorld;
using RimWorld.BaseGen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using Verse.AI.Group;

namespace RealRuins {
    class MechanoidsForcesGenerator: AbstractDefenderForcesGenerator {

        private float powerMax;

        public MechanoidsForcesGenerator(int power) {
            powerMax = power;
        }

        public override void GenerateForces(Map map, ResolveParams rp, ScatterOptions options) {
            Debug.Log(Debug.ForceGen, "Generating mechanoid forces");

            CellRect rect = rp.rect;

            /*if (rect.minX < 15 || rect.minZ < 15 || rect.maxX > map.Size.x - 15 || rect.maxZ > map.Size.z - 15) {
                return; //do not add enemies if we're on the map edge
            }

            if (!CellFinder.TryFindRandomCellInsideWith(rect, (IntVec3 x) => x.Standable(map) && options.roomMap[x.x - rect.BottomLeft.x, x.z - rect.BottomLeft.z] > 1, out IntVec3 testCell)) {
                return; //interrupt if there are no closed cells available
            }*/

            PawnKindDef pawnKindDef = null;

            if (powerMax == 0) {
                powerMax = rect.Area / 30.0f;
            }

            powerMax = ScalePointsToDifficulty(powerMax);

            float powerThreshold = (Math.Abs(Rand.Gaussian(0.5f, 1)) * powerMax) + 1;
            float cumulativePower = 0;

            Faction faction = Faction.OfMechanoids;

            Lord lord = LordMaker.MakeNewLord(lordJob: new LordJob_DefendPoint(rect.CenterCell), faction: faction, map: map, startingPawns: null);
            int tile = map.Tile;

            while (cumulativePower <= powerThreshold) {


                PawnKindDef currentPawnKindDef = (from kind in DefDatabase<PawnKindDef>.AllDefsListForReading
                                                  where kind.RaceProps.IsMechanoid
                                                  select kind).RandomElementByWeight((PawnKindDef kind) => 1f / kind.combatPower);

                PawnGenerationRequest request =
                    new PawnGenerationRequest(currentPawnKindDef, faction: faction, tile: tile, forceGenerateNewPawn: true,
                    mustBeCapableOfViolence: true, forceAddFreeWarmLayerIfNeeded: true, excludeBiologicalAgeRange: new FloatRange(0, 16));

                IntVec3 cell = IntVec3.Invalid;
                CellFinder.TryFindRandomSpawnCellForPawnNear(rect.CenterCell, map, out cell);

                if (cell != IntVec3.Invalid) { 
                    Pawn pawn = PawnGenerator.GeneratePawn(request);

                    FilthMaker.TryMakeFilth(cell, map, ThingDefOf.Filth_Blood, 5);
                    GenSpawn.Spawn(pawn, cell, map, WipeMode.Vanish);

                    lord.AddPawn(pawn);
                    cumulativePower += pawn.kindDef.combatPower;
                } else {
                    break; //no more suitable cells
                }
                Debug.Log(Debug.ForceGen, "Spawned mechanoid of faction {0}", faction.Name);
            }
        }

        public override void GenerateStartingParty(Map map, ResolveParams rp, ScatterOptions options) {
        }
    }
}
