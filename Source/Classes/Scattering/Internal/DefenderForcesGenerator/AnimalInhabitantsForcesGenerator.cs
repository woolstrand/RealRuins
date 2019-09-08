using RimWorld;
using RimWorld.BaseGen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using Verse.AI.Group;

namespace RealRuins {
    class AnimalInhabitantsForcesGenerator : AbstractDefenderForcesGenerator {
        public override void GenerateForces(Map map, ResolveParams rp) {
            ScatterOptions options = rp.GetCustom<ScatterOptions>(Constants.ScatterOptions);
            CellRect rect = rp.rect;

            if (rect.minX < 15 || rect.minZ < 15 || rect.maxX > map.Size.x - 15 || rect.maxZ > map.Size.z - 15) {
                return; //do not add enemies if we're on the map edge
            }

            if (!CellFinder.TryFindRandomCellInsideWith(rect, (IntVec3 x) => x.Standable(map) && options.roomMap[x.x - rect.BottomLeft.x, x.z - rect.BottomLeft.z] > 1, out IntVec3 testCell)) {
                return; //interrupt if there are no closed cells available
            }

            PawnKindDef pawnKindDef = null;

           
            pawnKindDef = map.Biome.AllWildAnimals.RandomElementByWeight((PawnKindDef def) => (def.RaceProps.foodType == FoodTypeFlags.CarnivoreAnimal || def.RaceProps.foodType == FoodTypeFlags.OmnivoreAnimal) ? 1 : 0);
           
            float powerMax = rect.Area / 30.0f;
            float powerThreshold = (Math.Abs(Rand.Gaussian(0.5f, 1)) * powerMax) + 1;

            float cumulativePower = 0;

            Faction faction = Faction.OfAncientsHostile;

            Lord lord = LordMaker.MakeNewLord(lordJob: new LordJob_DefendPoint(rect.CenterCell), faction: faction, map: map, startingPawns: null);
            int tile = map.Tile;

            while (cumulativePower <= powerThreshold) {

                PawnKindDef currentPawnKindDef = pawnKindDef;
                PawnGenerationRequest request =
                    new PawnGenerationRequest(currentPawnKindDef, faction, PawnGenerationContext.NonPlayer, tile, true, false, false, //allowDead is last
                    false, true, false, 1f,
                    false, true, true, false,
                    false, false, false,
                    null, null, null, null,
                    null, null, null, null);

                if (CellFinder.TryFindRandomCellInsideWith(rect, (IntVec3 x) => x.Standable(map) && options.roomMap[x.x - rect.minX, x.z - rect.minZ] > 1, out IntVec3 cell)) {
                    Pawn pawn = PawnGenerator.GeneratePawn(request);

                    FilthMaker.MakeFilth(cell, map, ThingDefOf.Filth_Blood, 5);
                    GenSpawn.Spawn(pawn, cell, map, WipeMode.Vanish);

                    lord.AddPawn(pawn);
                    cumulativePower += pawn.kindDef.combatPower;
                } else {
                    break; //no more suitable cells
                }
            }
        }

        public override void GenerateStartingParty(Map map, ResolveParams rp) {
        }
    }
}
