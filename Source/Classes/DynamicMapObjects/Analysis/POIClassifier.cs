using System;

using RimWorld;
using Verse;

namespace RealRuins {

    class POIClassification {
        public POIType functionalType; //what kind of place it is: settlement, production, military
        public POIType type; //functionalType fitted to the era: what the POI is
        public TechLevel era; //Undefined: any
        public float militaryFeatures;
        public float militaryPower = 1;
        public float fortification;
        public float researchScore;
        public float prodScore;
    }

    // Decides the POI type from blueprint metrics (BlueprintAnalyzerResult). Pure: same metrics give the same type,
    // except the outpost-sized military base, which is random on purpose.
    static class POIClassifier {
        // Share of buildings above medieval a blueprint may have and still count as a medieval one
        public const float lowTechAllowedShare = 0.05f;

        // Fortification: a courtyard of at least this share of the internal area (and at least minCourtyardArea cells)
        // makes the outer walls count as defences. Index: outer wall strength per perimeter cell of a square of the internal
        // area, so about the average wall strength of a closed ring (granite 1.7), whatever the size.
        public const float minCourtyardShare = 0.15f;
        public const int minCourtyardArea = 100;
        public const float fortificationWeight = 1.5f; //military features per point of fortification index

        // Research: high tech is absolute (spacer and above), not compared to the rest of the base
        public const float researchBenchWeight = 5;
        public const float researchFacilityWeight = 3;
        public const float minResearchScore = 15;
        public const float minResearchGoodsValue = 100; //average value of a haulable stack

        // Power plant: generators and batteries make this share of the buildings
        public const int minGenerators = 4;
        public const float minPowerShare = 0.25f;

        public static POIClassification Classify(BlueprintAnalyzerResult result, ScatterOptions options) {
            POIClassification c = new POIClassification();
            c.functionalType = FunctionalType(result, c);
            c.era = Era(result, options);
            c.type = POITypeUtility.FitToEra(c.functionalType, c.era);
            return c;
        }

        // Era the POI type has to belong to: the tech level cap, or medieval if the base is a medieval one. Undefined if neither applies.
        private static TechLevel Era(BlueprintAnalyzerResult result, ScatterOptions options) {
            TechLevel era = TechLevel.Undefined;
            if (result.aboveMedievalBuildingsCount <= result.buildingsCount * lowTechAllowedShare) era = TechLevel.Medieval;
            if (TechLevelFilter.IsActive(options) && (era == TechLevel.Undefined || options.maxTechLevel < era)) era = options.maxTechLevel;
            return era;
        }

        private static POIType FunctionalType(BlueprintAnalyzerResult result, POIClassification c) {
            //many walls, liitle internal space => ruins
            if (result.wallLength < 70 || (result.totalItemsCost < 2000 && result.bedsCount < 1)) {
                return POIType.Ruins;
            }

            //if 2/3 of all items (except walls) are outside (outside of room and unroofed), consider it a ruin. Caveat: enclosed rooms formed by mountains are not considered closed rooms.
            if (result.itemsInside < (result.occupiedTilesCount - result.wallLength) * 0.3) {
                return POIType.Ruins;
            }

            //no enclosed rooms at all (e.g. roofed or walled area without closed rooms): the calculations below divide by internal area
            if (result.internalArea <= 0) {
                return POIType.Ruins;
            }

            if (result.internalArea / result.wallLength < 2 && result.roomsCount < 6) {
                return POIType.Ruins;
            }

            //internal area is > 0 here, so occupiedTilesCount is too
            if (result.totalItemsCost / result.occupiedTilesCount < 10) {
                return POIType.Ruins;
            }

            c.fortification = Fortification(result);
            c.militaryFeatures = (float)(result.militaryItemsCount + (result.defensiveItemsCount * 10)) * 25 / result.internalArea
                + c.fortification * fortificationWeight;
            c.militaryPower = (float)(result.defensiveItemsCount * 250) / result.internalArea + 1;
            c.prodScore = result.productionItemsCount * 10 + (result.haulableStacksCount * 5 / result.internalArea);
            c.researchScore = result.researchBenchesCount * researchBenchWeight + result.researchFacilitiesCount * researchFacilityWeight
                + result.highTechBuildingsCount;

            bool stronghold = c.militaryFeatures >= 8 && result.defensiveItemsCount > 15;
            if (stronghold) {
                return POIType.Stronghold;
            }

            //research prevails over military: its high tech is the point, defences only protect it
            float goodsValue = result.haulableStacksCount > 0 ? result.haulableItemsCost / result.haulableStacksCount : 0;
            if (result.researchBenchesCount > 0 && c.researchScore >= minResearchScore && goodsValue >= minResearchGoodsValue) {
                return POIType.Research;
            }

            if (result.generatorsCount >= minGenerators && result.generatorsCount + result.batteriesCount >= result.buildingsCount * minPowerShare) {
                return POIType.PowerPlant;
            }

            if (c.militaryFeatures < 3 && c.prodScore <= 50) {
                if (result.internalArea < 2000 && result.bedsCount > 0 && result.totalItemsCost < 30000) {
                    return POIType.Camp;
                }
                if (result.internalArea >= 2000 && result.roomsCount > 12) {
                    return POIType.City;
                }
                if (result.haulableStacksCount > 0 && (float)(result.occupiedTilesCount - result.wallLength) / result.haulableStacksCount < 3 || result.bedsCount < 3) {
                    return POIType.Storage;
                }
                return POIType.Camp;
            }

            if (c.militaryFeatures >= 3 && c.militaryPower < 8 && c.prodScore < 50) {
                if (result.internalArea < 2000) {
                    return result.commsCount > 0 ? POIType.Communication : POIType.Outpost;
                }
                if (result.internalArea > 2000 && result.internalArea < 6000 && result.roomsCount < 20) {
                    return POIType.MilitaryBaseSmall;
                }
                if (result.internalArea > 6000 || result.roomsCount > 20) {
                    return POIType.City;
                }
            }

            if (c.militaryFeatures < 8 && c.prodScore >= 50) {
                return POIType.Factory;
            }

            if (c.militaryFeatures >= 8) {
                return result.internalArea > 2500 ? POIType.MilitaryBaseLarge : POIType.MilitaryBaseSmall;
            }

            return POIType.Ruins;
        }

        // 0 without a courtyard, else about the average strength of the outer walls if they were a closed ring (capped at 3)
        private static float Fortification(BlueprintAnalyzerResult result) {
            if (result.enclosedOpenArea < Math.Max(minCourtyardArea, result.internalArea * minCourtyardShare)) return 0;
            float ring = 4 * (float)Math.Sqrt(result.internalArea);
            return Math.Min(3, result.outerWallStrength / ring);
        }
    }
}
