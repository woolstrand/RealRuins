using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using RimWorld;
using Verse;

namespace RealRuins {

    // Analyzes a blueprint and decides which POI type it is.
    // Pipeline: FindRooms, preprocessing (with ForAnalyzer options), metrics (BlueprintAnalyzerResult.Collect),
    // classification (POIClassifier). What things are for is told by DefRoles. Safe to call from a background thread.
    class BlueprintAnalyzer {

        private Blueprint blueprint;
        public POIType determinedType;
        public float militaryPower;
        public int mannableCount;

        public BlueprintAnalyzerResult result { get; private set; }
        public POIClassification classification { get; private set; }
        public bool shouldSuppressLogging = true;

        private ScatterOptions options;

        public BlueprintAnalyzer(Blueprint blueprint) : this(blueprint, ScatterOptionsFactory.ForAnalyzer()) {
        }

        // options: ForAnalyzer with another tech level cap (POI type sampling)
        public BlueprintAnalyzer(Blueprint blueprint, ScatterOptions options) {
            this.blueprint = blueprint;
            this.options = options;
        }

        public void Analyze() {
            if (!shouldSuppressLogging) {
                Debug.Log(Debug.Analyzer, "analyzing blueprint {0} with options {1}", blueprint, options);
            }

            blueprint.FindRooms();
            int bedsBeforeTechLimit = TechLevelFilter.IsActive(options) ? BlueprintAnalyzerResult.CountBeds(blueprint) : -1;
            BlueprintPreprocessor.ProcessBlueprint(blueprint, options);
            blueprint.UpdateBlueprintStats(includeCost: true);

            result = BlueprintAnalyzerResult.Collect(blueprint);
            result.bedsBeforeTechLimit = Math.Max(result.bedsCount, bedsBeforeTechLimit);
            classification = POIClassifier.Classify(result, options);

            //The classification tells what kind of place it is (settlement, production, military), the era what it is called:
            //a medieval town is a village, a medieval factory a workshop, a medieval military base a castle.
            determinedType = classification.type;
            militaryPower = classification.militaryPower;
            mannableCount = result.mannableCount;
            if (!shouldSuppressLogging) {
                Debug.Log(Debug.Analyzer, "Type is {0} ({1} in era {2}), military features {3}, fortification {4}, research {5}, prod {6} by {7}",
                    determinedType, classification.functionalType, classification.era, classification.militaryFeatures,
                    classification.fortification, classification.researchScore, classification.prodScore, result.ToString());
            }
        }

        public float chanceOfHavingFaction() {
            switch (determinedType) {
                case POIType.Ruins:
                    return 0.0f;

                case POIType.Camp:
                case POIType.Outpost:
                case POIType.Storage:
                case POIType.Shipwreck:
                    return 0.3f;

                case POIType.MilitaryBaseSmall:
                case POIType.Communication:
                case POIType.Village:
                case POIType.Workshop:
                    return 0.7f;

                case POIType.Castle:
                case POIType.City:
                case POIType.Stronghold:
                case POIType.MilitaryBaseLarge:
                case POIType.Research:
                case POIType.PowerPlant:
                case POIType.Factory:
                    return 0.9f;

                default:
                    return 0.5f;
            }
        }
    }
}
