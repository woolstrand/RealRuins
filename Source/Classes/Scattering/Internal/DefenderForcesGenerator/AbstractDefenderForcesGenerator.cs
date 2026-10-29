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

        public float ScalePointsToDifficulty(float points, ScatterContext context) {
            float forceMultiplier = context.settings.forceMultiplier;
            Debug.Log("Scaling difficulty from {0} points to {1}", points, points * Find.Storyteller.difficulty.threatScale * forceMultiplier);
            return points * Find.Storyteller.difficulty.threatScale * forceMultiplier;
        }
    }
}
