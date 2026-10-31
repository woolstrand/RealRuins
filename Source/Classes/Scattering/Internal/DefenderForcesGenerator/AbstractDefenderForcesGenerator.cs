using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Verse;
using RimWorld;
using RimWorld.BaseGen;

namespace RealRuins {
    abstract class AbstractDefenderForcesGenerator {
        //rp provides faction and pawn generation params, placement rect comes from context.state
        public abstract void GenerateForces(Map map, ResolveParams rp, ScatterContext context); //called during map generation

        //There was some problem with pawns not created properly during basegen stack steps, but all working fine in genstep handler
        //Probably should investigate this later and combine all generation in one method
        public abstract void GenerateStartingParty(Map map, ResolveParams rp, ScatterContext context);

        //Spends the uncovered cost on raid triggers scattered over the placement rect
        protected void SpawnRaidTriggers(Map map, ResolveParams rp, ScatterContext context, string forcesName) {
            if (context == null) return;

            int addedTriggers = 0;
            float ratio = 10;
            float remainingCost = context.state.uncoveredCost * (Rand.Value + 0.5f); //cost estimation as seen by other factions
            Debug.Log(Debug.ForceGen, "Running {2} force generation with remaining cost of {0} (while uncovered is {1})", remainingCost, context.state.uncoveredCost, forcesName);

            float initialCost = remainingCost;

            int triggersAbsoluteMaximum = 100;

            while (remainingCost > 0) {

                bool hasLocation = TryFindTriggerLocation(map, context, out IntVec3 mapLocation);

                ThingDef raidTriggerDef = ThingDef.Named("RaidTrigger");
                RaidTrigger trigger = ThingMaker.MakeThing(raidTriggerDef) as RaidTrigger;

                trigger.faction = PickTriggerFaction(rp, context);

                int raidMaxPoints = (int)(remainingCost / ratio);
                float raidValue = Math.Abs(Rand.Gaussian()) * raidMaxPoints + Rand.Value * raidMaxPoints + 250.0f;
                if (raidValue > 10000) raidValue = Rand.Range(8000, 11000); //sanity cap. against some beta-poly bases.
                remainingCost -= raidValue * ratio;

                trigger.value = ScalePointsToDifficulty(raidValue, context);

                if (hasLocation) {
                    GenSpawn.Spawn(trigger, mapLocation, map);
                    Debug.Log(Debug.ForceGen, "Spawned trigger at {0}, {1} for {2} points, ticks left: {3}, faction: {4}", mapLocation.x, mapLocation.z, trigger.value, trigger.TicksLeft(), trigger.faction);
                }
                addedTriggers++;

                context.state.uncoveredCost = Math.Abs(remainingCost);

                if (addedTriggers > triggersAbsoluteMaximum) {
                    if (remainingCost < initialCost * 0.2f) {
                        if (Rand.Chance(0.1f)) {
                            return;
                        }
                    }
                }
            }
        }

        //Placement rect can stick out of the map, so a random cell is retried a few times before giving up.
        //A trigger without a location is skipped, but its cost is still spent so the generation loop always ends.
        protected bool TryFindTriggerLocation(Map map, ScatterContext context, out IntVec3 location) {
            for (int attempt = 0; attempt < 10; attempt++) {
                location = context.state.placementRect.RandomCell;
                if (location.InBounds(map)) return true;
            }
            location = IntVec3.Invalid;
            Debug.Warning(Debug.ForceGen, "Can't find in-bounds location for raid trigger in {0}, skipping it", context.state.placementRect);
            return false;
        }

        protected virtual Faction PickTriggerFaction(ResolveParams rp, ScatterContext context) {
            return rp.faction;
        }

        public float ScalePointsToDifficulty(float points, ScatterContext context) {
            float forceMultiplier = context.settings.forceMultiplier;
            Debug.Log("Scaling difficulty from {0} points to {1}", points, points * Find.Storyteller.difficulty.threatScale * forceMultiplier);
            return points * Find.Storyteller.difficulty.threatScale * forceMultiplier;
        }
    }
}
