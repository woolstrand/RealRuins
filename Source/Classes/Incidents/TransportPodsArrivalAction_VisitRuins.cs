using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using Verse;

namespace RealRuins {
    public class TransportPodsArrivalAction_VisitRuins : TransportersArrivalAction_VisitRuinsBase {
        public TransportPodsArrivalAction_VisitRuins() {
        }

        public TransportPodsArrivalAction_VisitRuins(MapParent site, PawnsArrivalModeDef arrivalMode) : base(site, arrivalMode) {
        }

        public override void Arrived(List<ActiveTransporterInfo> pods, PlanetTile tile) {
            Debug.Log("Overridden arrive pods - visit ruins");
            Thing lookTarget = TransportersArrivalActionUtility.GetLookTarget(pods);
            bool firstVisit = !site.HasMap;

            Map orGenerateMap = GetOrGenerateMapUtility.GetOrGenerateMap(site.Tile, null);
            Debug.Log("Generated encounter map, which is {0}", orGenerateMap?.ToString() ?? "NULL");
            if (firstVisit) {
                Find.LetterStack.ReceiveLetter("LetterLabelTransportPodsArrivedAtRuins".Translate(), "LetterTransportPodsArrivedAtRuins".Translate().CapitalizeFirst(), LetterDefOf.ThreatBig, lookTarget, null, null);
            }

            FinishArrival(pods, orGenerateMap, firstVisit, lookTarget);
        }

        public static IEnumerable<FloatMenuOption> GetFloatMenuOptions(Action<PlanetTile, TransportersArrivalAction> launchAction, IEnumerable<IThingHolder> pods, MapParent site) {
            return GetFloatMenuOptions(launchAction, pods, site, mode => new TransportPodsArrivalAction_VisitRuins(site, mode));
        }
    }
}
