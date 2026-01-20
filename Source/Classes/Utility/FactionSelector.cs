using RimWorld;
using Verse;
using System;
using System.Collections.Generic;
using System.Linq;

public static class FactionSelector {
    private static readonly Dictionary<TechLevel, float> techLevelWeights = new Dictionary<TechLevel, float>
    {
        { TechLevel.Animal, 0f },        // usually skip trivial
        { TechLevel.Neolithic, 0.01f },  // tiny probability
        { TechLevel.Medieval, 0.05f },   // small probability
        { TechLevel.Industrial, 0.7f },  // major probability
        { TechLevel.Spacer, 0.15f },     // smaller but noticeable
        { TechLevel.Ultra, 0.08f }       // small chance
    };

    private static readonly Random rng = new Random();

    public static Faction GetRandomFaction(bool includeHostile = true, bool includeFriendly = true) {
        // Get all factions matching criteria
        List<Faction> factions = Find.FactionManager.AllFactions
            .Where(f =>
                ((includeHostile && f.HostileTo(Faction.OfPlayer)) ||
                 (includeFriendly && !f.HostileTo(Faction.OfPlayer)))
                // exclude mechanoids
                && f.def.humanlikeFaction
                // exclude hidden/orbital traders
                && !f.def.hidden
            )
            .ToList();


        // Filter out factions whose tech level has weight 0
        factions = factions
            .Where(f => techLevelWeights.TryGetValue(f.def.techLevel, out float w) && w > 0f)
            .ToList();

        if (!factions.Any())
            return null;

        // Compute cumulative weights
        float totalWeight = factions.Sum(f => techLevelWeights[f.def.techLevel]);
        float r = (float)(rng.NextDouble() * totalWeight);

        float cumulative = 0f;
        foreach (Faction f in factions) {
            cumulative += techLevelWeights[f.def.techLevel];
            if (r <= cumulative)
                return f;
        }

        return Find.FactionManager.RandomEnemyFaction(); // fallback
    }
}
