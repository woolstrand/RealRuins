using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using Verse;

namespace RealRuins {
    public class TransportPodsArrivalAction_VisitRuinsPOI : TransportersArrivalAction_VisitRuinsBase {
        public TransportPodsArrivalAction_VisitRuinsPOI() {
        }

        public TransportPodsArrivalAction_VisitRuinsPOI(MapParent site, PawnsArrivalModeDef arrivalMode) : base(site, arrivalMode) {
            Debug.Log("Creating visit ruins action with mode {0}", arrivalMode);
        }

        public override void Arrived(List<ActiveTransporterInfo> pods, PlanetTile tile) {
            Debug.Log("Overridden arrive pods - visit POI, mode: {0}", arrivalMode);
            Thing lookTarget = TransportersArrivalActionUtility.GetLookTarget(pods);
            bool firstVisit = !site.HasMap;

            // If no map created (first entry) we need to prepare a letter describing relationship change
            TaggedString letterText = null;
            TaggedString letterCaption = null;
            LetterDef letterDef = LetterDefOf.NeutralEvent;
            if (firstVisit) {
                if (site.Faction == null) {
                    letterCaption = "LetterLabelCaravanEnteredUnownedPOI".Translate();
                    letterText = "LetterTransportPodsArrivedInUnownedPOI".Translate().CapitalizeFirst();
                    letterDef = LetterDefOf.NeutralEvent;
                } else {
                    letterCaption = "LetterLabelTransportPodsAttackedPOI".Translate();
                    letterText = "LetterTransportPodsAttackedPOI".Translate().CapitalizeFirst();
                    letterDef = LetterDefOf.ThreatBig;
                }
            }

            POIArrivalUtility.AffectRelationsIfNeeded(site, ref letterText);
            Find.LetterStack.ReceiveLetter(letterCaption, letterText, letterDef, lookTarget, null, null);

            Map orGenerateMap = GetOrGenerateMapUtility.GetOrGenerateMap(site.Tile, null);
            Debug.Log("Generated POI map, which is {0}", orGenerateMap?.ToString() ?? "NULL");

            FinishArrival(pods, orGenerateMap, firstVisit, lookTarget);
        }

        public static IEnumerable<FloatMenuOption> GetFloatMenuOptions(Action<PlanetTile, TransportersArrivalAction> launchAction, IEnumerable<IThingHolder> pods, MapParent site) {
            return GetFloatMenuOptions(launchAction, pods, site, mode => new TransportPodsArrivalAction_VisitRuinsPOI(site, mode));
        }
    }
}
