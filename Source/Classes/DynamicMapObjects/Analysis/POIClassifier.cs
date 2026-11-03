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
        public float prodScore; //worktables per bed
    }

    // Decides the POI type from blueprint metrics (BlueprintAnalyzerResult). Pure: same metrics give the same type.
    // Thresholds were tuned with the Sample POI types debug action (docs/06-planetary-ruins.md).
    static class POIClassifier {
        // Share of buildings above medieval a blueprint may have and still count as a medieval one
        public const float lowTechAllowedShare = 0.05f;

        // Fortification: a courtyard of at least this share of the internal area (and at least minCourtyardArea cells)
        // makes the perimeter walls count as defences. Index: perimeter wall strength per cell of the perimeter of a square
        // of the internal area, so about the average wall strength of a closed ring (granite 1.7), whatever the size.
        public const float minCourtyardShare = 0.15f;
        public const int minCourtyardArea = 100;
        public const float fortificationWeight = 2.5f; //military features per point of fortification index
        public const float minSmallBaseFortification = 0.5f; //a small military site this fortified is a base, not an outpost

        // Research: a research bench and a lot of high tech. High tech is absolute (spacer and above), not compared to the
        // rest of the base. Research facilities count too, at most two per bench (a vanilla bench takes one multi-analyzer).
        public const float researchFacilityWeight = 2;
        public const int researchFacilitiesPerBench = 2;
        public const float minResearchScore = 12;
        public const float minResearchGoodsValue = 150; //average value of a haulable stack, median of all bases is ~190

        // Power plant: generators and batteries make this share of the buildings (p90 of all bases is ~0.11)
        public const int minGenerators = 4;
        public const float minPowerShare = 0.15f;

        // Production site: many worktables, and many per bed (median of all bases is ~0.55 per bed)
        public const int minProductionWorktables = 6;
        public const float minWorktablesPerBed = 0.75f;

        // City: a large settlement. Up to medieval it becomes a village, which is smaller.
        public const int minCityArea = 1200;
        public const int minCityRooms = 13;
        public const int minCityBeds = 8;
        public const int minVillageArea = 600;
        public const int minVillageRooms = 8;
        public const int minVillageBeds = 5;

        // Military site size by internal area. Military bases in sampled blueprints have ~1000-1800.
        // Moderate military features: outpost (or comm center) < 800 <= small base < 1500 <= large base < 6000 <= armed city.
        // High military features (8+): large base from 1200.
        public const int maxOutpostArea = 800;
        public const int minLargeBaseArea = 1500;
        public const int minArmedCityArea = 6000;
        public const int minHeavyLargeBaseArea = 1200;

        public static POIClassification Classify(BlueprintAnalyzerResult result, ScatterOptions options) {
            POIClassification c = new POIClassification();
            c.era = Era(result, options);
            c.functionalType = FunctionalType(result, c);
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
            c.prodScore = result.bedsCount > 0 ? (float)result.productionItemsCount / result.bedsCount : result.productionItemsCount;
            bool production = result.productionItemsCount >= minProductionWorktables && result.productionItemsCount >= result.bedsCount * minWorktablesPerBed;
            c.researchScore = result.highTechBuildingsCount
                + researchFacilityWeight * Math.Min(result.researchFacilitiesCount, researchFacilitiesPerBench * result.researchBenchesCount);

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

            if (c.militaryFeatures < 3 && !production) {
                bool village = c.era != TechLevel.Undefined && c.era <= TechLevel.Medieval;
                if (result.internalArea >= (village ? minVillageArea : minCityArea) && result.roomsCount >= (village ? minVillageRooms : minCityRooms)
                    && result.bedsBeforeTechLimit >= (village ? minVillageBeds : minCityBeds)) {
                    return POIType.City;
                }
                if (result.internalArea < 2000 && result.bedsCount > 0 && result.totalItemsCost < 30000) {
                    return POIType.Camp;
                }
                if (result.haulableStacksCount > 0 && (float)(result.occupiedTilesCount - result.wallLength) / result.haulableStacksCount < 3 || result.bedsCount < 3) {
                    return POIType.Storage;
                }
                return POIType.Camp;
            }

            if (c.militaryFeatures >= 3 && c.militaryPower < 8 && !production) {
                if (result.internalArea < maxOutpostArea) {
                    if (c.fortification >= minSmallBaseFortification) return POIType.MilitaryBaseSmall;
                    return result.commsCount > 0 ? POIType.Communication : POIType.Outpost;
                }
                if (result.internalArea < minLargeBaseArea) {
                    return POIType.MilitaryBaseSmall;
                }
                return result.internalArea < minArmedCityArea ? POIType.MilitaryBaseLarge : POIType.City;
            }

            if (c.militaryFeatures < 8 && production) {
                return POIType.Factory;
            }

            if (c.militaryFeatures >= 8) {
                return result.internalArea >= minHeavyLargeBaseArea ? POIType.MilitaryBaseLarge : POIType.MilitaryBaseSmall;
            }

            return POIType.Ruins;
        }

        // 0 without a courtyard, else about the average strength of the outer walls if they were a closed ring (capped at 3)
        private static float Fortification(BlueprintAnalyzerResult result) {
            if (result.enclosedOpenArea < Math.Max(minCourtyardArea, result.internalArea * minCourtyardShare)) return 0;
            float ring = 4 * (float)Math.Sqrt(result.internalArea);
            return Math.Min(3, result.perimeterWallStrength / ring);
        }
    }
}
