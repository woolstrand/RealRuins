using System;
using RimWorld;
using RimWorld.BaseGen;
using Verse;

namespace RealRuins {
    // Raiders already at a ruin spotted by a caravan. There are no triggers or waves: a caravan can leave at any moment.
    class CaravanRuinsForcesGenerator : AbstractDefenderForcesGenerator {
        private const float maxPoints = 5000; //before difficulty scaling
        private const float costPerPoint = 40; //a 200k ruin reaches the maximum
        private const float minPartyPoints = 200; //the smallest party ScatterStartingParties makes

        public override void GenerateForces(Map map, ResolveParams rp, ScatterContext context) { }

        public override void GenerateStartingParty(Map map, ResolveParams rp, ScatterContext context) {
            float points = context.state.uncoveredCost / costPerPoint;
            if (Rand.Chance(0.5f * Find.Storyteller.difficulty.threatScale)) {
                points += Math.Abs(Rand.Gaussian()) * 500;
            }

            points = ScalePointsToDifficulty(Math.Min(maxPoints, points), context);
            Debug.Log(Debug.ForceGen, "Caravan ruins worth {0}: {1} points of raiders", context.state.uncoveredCost, points);
            if (points < minPartyPoints || Find.FactionManager.RandomEnemyFaction() == null) return;

            ScatterStartingParties((int)points, false, map);
        }
    }
}
