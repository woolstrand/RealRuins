using RimWorld;
using Verse;
using RimWorld.Planet;

namespace RealRuins {
    static class POIArrivalUtility {
        //Intended (FIXES_QUEUE M7, won't fix): entering a POI that belongs to a faction is an attack on their property,
        //its defenders are hostile on arrival. This is a copy of vanilla SettlementUtility.AffectRelationsOnAttacked,
        //and the relation change is appended to the arrival letter the same way.
        public static void AffectRelationsIfNeeded(MapParent mapParent, ref TaggedString letterText) {
            if (mapParent.Faction == null || mapParent.Faction == Faction.OfPlayer) {
                return;
            }

            FactionRelationKind playerRelationKind = mapParent.Faction.PlayerRelationKind;
            Faction.OfPlayer.TryAffectGoodwillWith(mapParent.Faction, Faction.OfPlayer.GoodwillToMakeHostile(mapParent.Faction),
                canSendMessage: false, canSendHostilityLetter: false, HistoryEventDefOf.AttackedSettlement);
            mapParent.Faction.TryAppendRelationKindChangedInfo(ref letterText, playerRelationKind, mapParent.Faction.PlayerRelationKind);
        }
    }
}
