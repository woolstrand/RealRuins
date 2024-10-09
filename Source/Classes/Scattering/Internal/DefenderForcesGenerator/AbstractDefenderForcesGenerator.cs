using RimWorld.BaseGen;
using Verse;

namespace RealRuins;

internal abstract class AbstractDefenderForcesGenerator
{
	public abstract void GenerateForces(Map map, ResolveParams rp, ScatterOptions options);

	public abstract void GenerateStartingParty(Map map, ResolveParams rp, ScatterOptions options);

	public float ScalePointsToDifficulty(float points)
	{
		Debug.Log("Scaling difficulty from {0} points to {1}", points, points * Find.Storyteller.difficulty.threatScale * RealRuins_ModSettings.forceMultiplier);
		return points * Find.Storyteller.difficulty.threatScale * RealRuins_ModSettings.forceMultiplier;
	}
}
