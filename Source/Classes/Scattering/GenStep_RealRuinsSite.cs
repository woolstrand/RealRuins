using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using RimWorld;
using RimWorld.BaseGen;
using Verse;

namespace RealRuins {
    // What a site genstep decides before the common generation flow runs.
    class SitePlan {
        public ScatterOptions options;
        public Faction faction;
        public CellRect rect;

        // Blueprint already loaded during this same map generation, so the scatterer doesn't parse the file again.
        // Leave null to let the scatterer load options.blueprintFileName (or a random one).
        public Blueprint blueprint;

        // Defender generators. Null or empty = no defenders; ruins are scattered either way.
        public List<AbstractDefenderForcesGenerator> generators;

        public bool ensureCanHoldRoof = true;

        // Pawns of plan.faction to man the turrets (POI), 0 = none.
        public int turretOperators;
    }

    // A map that is one ruined (or intact) base made from a single blueprint: abandoned base, caravan small ruins, planetary POI.
    // Subclasses only decide the plan; the generation flow is the same for all of them.
    // Regular maps with several ruin chunks use GenStep_ScatterRealRuins instead.
    abstract class GenStep_RealRuinsSite : GenStep {

        // Returns null if the site can't be generated (the map stays empty of ruins).
        protected abstract SitePlan MakePlan(Map map);

        // Extra BaseGen symbols (manned turrets, a raid party). Pushed before the common symbols, so (LIFO) resolved after them.
        protected virtual void PushExtraSymbols(Map map, ResolveParams rp, SitePlan plan) { }

        // Called after generation, also when it failed.
        protected virtual void Finish(Map map) { }

        public override void Generate(Map map, GenStepParams parms) {
            Find.TickManager.Pause();
            try {
                SitePlan plan = MakePlan(map);
                if (plan == null) {
                    Debug.Warning(Debug.Scatter, "{0}: no site plan, skipping", GetType().Name);
                    return;
                }
                Generate(map, plan);
            } finally {
                Finish(map);
            }
        }

        private void Generate(Map map, SitePlan plan) {
            ScatterContext context = new ScatterContext(plan.options, PipelineSettings.FromModSettings());
            context.preloadedBlueprint = plan.blueprint;

            ResolveParams rp = default(ResolveParams);
            rp.faction = plan.faction;
            rp.rect = plan.rect;
            BaseGen.globalSettings.map = map;
            BaseGen.globalSettings.mainRect = rp.rect;

            PushExtraSymbols(map, rp, plan);

            RuinsScatterer.Scatter(rp, context, null, plan.generators);

            BaseGen.symbolStack.Push("chargeBatteries", rp);
            if (plan.ensureCanHoldRoof) {
                BaseGen.symbolStack.Push("ensureCanHoldRoof", rp);
            }
            BaseGen.symbolStack.Push("refuel", rp);
            BaseGen.Generate();

            //adding starting party
            //don't doing it via basegen because of uh oh i don't remember, something with pawn location control
            if (plan.generators != null) {
                Debug.Log(Debug.BlueprintTransfer, "Generating {0} starting parties", plan.generators.Count);
                foreach (AbstractDefenderForcesGenerator generator in plan.generators) {
                    generator.GenerateStartingParty(map, rp, context);
                }
            }
        }
    }
}
