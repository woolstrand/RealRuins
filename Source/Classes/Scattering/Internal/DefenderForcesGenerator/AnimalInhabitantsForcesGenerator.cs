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
        //Budget cap before difficulty scaling: five timber wolves. Keeps big valuable chunks from producing huge packs.
        private const float MaxPoints = 5 * 75;
        //Budget floor, as in vanilla manhunter packs: a small chunk can still afford a single small animal.
        private const float MinPoints = 70;
        //Never more than this many animals in one ruin, whatever their kind.
        private const int MaxCount = 5;
        //They defend the ruin for this long, then become ordinary wild animals (see LordJob_AnimalInhabitants).
        private static readonly IntRange ReleaseAfterTicks = new IntRange(2 * GenDate.TicksPerDay, 4 * GenDate.TicksPerDay);

        //Meat eaters only: carnivores and omnivores, never herbivores or modded animals with exotic diets (energy, minerals, etc.).
        //canArriveManhunter is the flag mods and vanilla clear for animals not fit to come as a hostile pack.
        //Kinds without a positive combat power would never advance the spawn loop.
        private static bool CanInhabitRuins(PawnKindDef def) {
            return def.RaceProps.Animal && def.canArriveManhunter && def.combatPower > 0 && (def.RaceProps.foodType & FoodTypeFlags.Meat) != 0;
        }

        public override void GenerateForces(Map map, ResolveParams rp, ScatterContext context) {
            Debug.Log(Debug.ForceGen, "Animal forces generation");
            CellRect rect = context.state.placementRect;

            /*if (rect.minX < 15 || rect.minZ < 15 || rect.maxX > map.Size.x - 15 || rect.maxZ > map.Size.z - 15) {
                return; //do not add enemies if we're on the map edge
            }

            if (!CellFinder.TryFindRandomCellInsideWith(rect, (IntVec3 x) => x.Standable(map) && context.state.roomMap[x.x - rect.BottomLeft.x, x.z - rect.BottomLeft.z] > 1, out IntVec3 testCell)) {
                return; //interrupt if there are no closed cells available
            }*/

            float powerMax = (float)Math.Sqrt(context.state.uncoveredCost / 10 * (rect.Area / 30.0f));
            Debug.Log(Debug.ForceGen, "Unscaled power is {0} based on cost of {1} and area of {2}", powerMax, context.state.uncoveredCost, rect.Area);
            powerMax = ScalePointsToDifficulty(powerMax, context);
            if (powerMax <= 0) return; //threat scale set to 0: no inhabitants (the budget floor below would still afford one)
            float powerThreshold = (Math.Abs(Rand.Gaussian(0.5f, 1)) * powerMax) + 1;
            powerThreshold = Math.Max(Math.Min(powerThreshold, ScalePointsToDifficulty(MaxPoints, context)), MinPoints);

            //Any kind the budget affords at least once, with no preference for stronger ones; the budget and MaxCount then limit the count.
            //A biome may have no fitting animals at all (or no wild animals, like orbit).
            if (!map.Biome.AllWildAnimals.Where(def => CanInhabitRuins(def) && def.combatPower <= powerThreshold).TryRandomElement(out PawnKindDef pawnKindDef)) {
                Debug.Log(Debug.ForceGen, "No wild animals in biome {0} fit {1} points, skipping animal forces", map.Biome.defName, powerThreshold);
                return;
            }

            float cumulativePower = 0;

            Faction faction = Faction.OfAncientsHostile;

            Lord lord = LordMaker.MakeNewLord(lordJob: new LordJob_AnimalInhabitants(rect.CenterCell, Find.TickManager.TicksGame + ReleaseAfterTicks.RandomInRange), faction: faction, map: map, startingPawns: null);
            int tile = map.Tile;

            //stop before going over the budget or MaxCount; the kind fits the budget, so there is always at least one
            int count = 0;
            while (count < MaxCount && cumulativePower + pawnKindDef.combatPower <= powerThreshold) {

                PawnKindDef currentPawnKindDef = pawnKindDef;
                PawnGenerationRequest request =
                    new PawnGenerationRequest(currentPawnKindDef, faction: faction, tile: tile, forceGenerateNewPawn: true,
                    mustBeCapableOfViolence: true, forceAddFreeWarmLayerIfNeeded: true);

                IntVec3 cell = IntVec3.Invalid;
                if (!CellFinder.TryFindRandomCellInsideWith(rect, (IntVec3 x) => x.Standable(map) && context.state.roomMap[x.x - rect.minX, x.z - rect.minZ] > 1, out cell)) {
                    CellFinder.TryFindRandomSpawnCellForPawnNear(rect.CenterCell, map, out cell);
                }

                if (cell != IntVec3.Invalid) {
                    Pawn pawn = PawnGenerator.GeneratePawn(request);

                    FilthMaker.TryMakeFilth(cell, map, ThingDefOf.Filth_Blood, 5);
                    GenSpawn.Spawn(pawn, cell, map, WipeMode.Vanish);

                    lord.AddPawn(pawn);
                    cumulativePower += pawn.kindDef.combatPower;
                    count++;
                } else {
                    break; //no more suitable cells
                }
            }
        }

        public override void GenerateStartingParty(Map map, ResolveParams rp, ScatterContext context) {
        }
    }
}
