using System;

using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RealRuins {
    enum RuinsKind {
        Regular,       //all regular ruin chunks of one map together
        Caravan,       //main ruin of the caravan event
        AbandonedBase, //abandoned base event and quest
    }

    // The only place which decides what ruins may be worth: everything asks here, nothing else reads the cap settings.
    static class RuinsWealthLimits {
        public const float NoLimit = 1.0e9f;

        //Scaled limit of each kind, indexed by RuinsKind: colony wealth x share setting x weight, but never below the minimum.
        private static readonly float[] shareWeights = { 1, 2, 5 };
        //An abandoned base worth under 20k is soon scavenged completely (RuinedBaseComp), so a poor colony would hardly ever reach one.
        private static readonly float[] minimums = { 3000, 5000, 25000 };

        public static float For(RuinsKind kind) {
            float cap = SettingCap(kind);
            if (!RealRuins_ModSettings.ruinsScaleWithColonyWealth) return cap;
            return Math.Min(cap, ScaledLimit(kind, ColonyWealth()));
        }

        //The limit set by the player, NoLimit if unlimited
        public static float SettingCap(RuinsKind kind) {
            switch (kind) {
                case RuinsKind.Regular:
                    return RealRuins_ModSettings.ruinsWealthPerMap < RealRuins_ModSettings.ruinsWealthPerMapUnlimited ? RealRuins_ModSettings.ruinsWealthPerMap : NoLimit;
                case RuinsKind.Caravan:
                    return RealRuins_ModSettings.caravanRuinsCostCap < RealRuins_ModSettings.caravanRuinsCostCapUnlimited ? RealRuins_ModSettings.caravanRuinsCostCap : NoLimit;
                default:
                    return RealRuins_ModSettings.ruinsCostCap < 9.9999e8f ? RealRuins_ModSettings.ruinsCostCap : NoLimit;
            }
        }

        public static float ScaledLimit(RuinsKind kind, float colonyWealth) {
            return Math.Max(minimums[(int)kind], colonyWealth * RealRuins_ModSettings.ruinsColonyWealthShare * shareWeights[(int)kind]);
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
