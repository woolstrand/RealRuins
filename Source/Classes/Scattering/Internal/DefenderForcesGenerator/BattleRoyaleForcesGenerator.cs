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
    }
}
