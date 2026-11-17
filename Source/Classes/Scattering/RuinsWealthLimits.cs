using System;

using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RealRuins {
    enum RuinsKind {
        Regular,       //all regular ruin chunks of one map together
        Caravan,       //main ruin of the caravan event
        AbandonedBase, //abandoned base event and quest
        AbandonedPOI,  //planetary POI spawned as ruins
    }

    // The only place which decides what ruins may be worth: everything asks here, nothing else reads the cap settings.
    static class RuinsWealthLimits {
        public const float NoLimit = 1.0e9f;

        //The poorest colony still gets this part of the limits, the map being generated at game start included
        private const float minMultiplier = 0.5f;

        public static float For(RuinsKind kind) {
            float cap = SettingCap(kind);
            if (cap >= NoLimit || !RealRuins_ModSettings.ruinsScaleWithColonyWealth) return cap;
            return cap * Math.Max(minMultiplier, ColonyWealth() / RealRuins_ModSettings.ruinsReferenceColonyWealth);
        }

        //The limit set by the player, NoLimit if unlimited
        public static float SettingCap(RuinsKind kind) {
            switch (kind) {
                case RuinsKind.Regular:
                    return RealRuins_ModSettings.ruinsWealthPerMap < RealRuins_ModSettings.ruinsWealthPerMapUnlimited ? RealRuins_ModSettings.ruinsWealthPerMap : NoLimit;
                case RuinsKind.Caravan:
                    return RealRuins_ModSettings.caravanRuinsCostCap < RealRuins_ModSettings.caravanRuinsCostCapUnlimited ? Math.Max(RealRuins_ModSettings.caravanRuinsCostCapMin, RealRuins_ModSettings.caravanRuinsCostCap) : NoLimit;
                case RuinsKind.AbandonedPOI:
                    return RealRuins_ModSettings.poiRuinsCostCap < RealRuins_ModSettings.poiRuinsCostCapUnlimited ? RealRuins_ModSettings.poiRuinsCostCap : NoLimit;
                default:
                    return RealRuins_ModSettings.ruinsCostCap < 9.9999e8f ? RealRuins_ModSettings.ruinsCostCap : NoLimit;
            }
        }

        //A limit as ScatterOptions.costCap, where anything below 1 means no cap
        public static int ToCostCap(float limit) {
            return limit < NoLimit ? Math.Max(1, (int)limit) : -1;
        }

        //Home maps and player caravans, as the storyteller counts them. The map being generated is not a part of the colony yet.
        private static float ColonyWealth() {
            float wealth = 0;
            foreach (Map map in Find.Maps) {
                if (map.IsPlayerHome && map != MapGenerator.mapBeingGenerated) wealth += map.PlayerWealthForStoryteller;
            }
            foreach (Caravan caravan in Find.WorldObjects.Caravans) {
                if (caravan.IsPlayerControlled) wealth += caravan.PlayerWealthForStoryteller;
            }
            return wealth;
        }
    }
}
