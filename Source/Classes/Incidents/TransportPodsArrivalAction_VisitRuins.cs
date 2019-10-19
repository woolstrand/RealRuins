// RimWorld.Planet.TransportPodsArrivalAction_VisitSite
using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using Verse;

namespace RealRuins {
    public class TransportPodsArrivalAction_VisitRuins : TransportPodsArrivalAction {
        private MapParent site;

        private PawnsArrivalModeDef arrivalMode;

        public TransportPodsArrivalAction_VisitRuins() {
        }

        public TransportPodsArrivalAction_VisitRuins(MapParent site, PawnsArrivalModeDef arrivalMode) {
            this.site = site;
            this.arrivalMode = arrivalMode;
        }

        public override void ExposeData() {
            base.ExposeData();
            Scribe_References.Look(ref site, "site");
            Scribe_Defs.Look(ref arrivalMode, "arrivalMode");
        }

        public override FloatMenuAcceptanceReport StillValid(IEnumerable<IThingHolder> pods, int destinationTile) {
            FloatMenuAcceptanceReport floatMenuAcceptanceReport = base.StillValid(pods, destinationTile);
            if (!(bool)floatMenuAcceptanceReport) {
                return floatMenuAcceptanceReport;
            }
            if (site != null && site.Tile != destinationTile) {
                return false;
            }
            return CanVisit(pods, site);
        }

        public override bool ShouldUseLongEvent(List<ActiveDropPodInfo> pods, int tile) {
            return !site.HasMap;
        }

        public override void Arrived(List<ActiveDropPodInfo> pods, int tile) {
            Debug.Log("Overridden arrive pods");
            Thing lookTarget = TransportPodsArrivalActionUtility.GetLookTarget(pods);
            bool flag = !site.HasMap;
            Map orGenerateMap = GetOrGenerateMapUtility.GetOrGenerateMap(site.Tile, RuinedBaseWorker.MapSize, null);
            if (flag) {
                Find.TickManager.Notify_GeneratedPotentiallyHostileMap();
                PawnRelationUtility.Notify_PawnsSeenByPlayer_Letter_Send(orGenerateMap.mapPawns.AllPawns, "LetterRelatedPawnsInMapWherePlayerLanded".Translate(Faction.OfPlayer.def.pawnsPlural), LetterDefOf.NeutralEvent, informEvenIfSeenBefore: true);

                Find.LetterStack.ReceiveLetter("LetterLabelTransportPodsArrivedAtRuins".Translate(), "LetterTransportPodsArrivedAtRuins".Translate().CapitalizeFirst(), LetterDefOf.ThreatBig, lookTarget, null, null);
            } else {
                Messages.Message("MessageTransportPodsArrived".Translate(), lookTarget, MessageTypeDefOf.TaskCompletion);
            }
            arrivalMode.Worker.TravelingTransportPodsArrived(pods, orGenerateMap);
        }



        public static FloatMenuAcceptanceReport CanVisit(IEnumerable<IThingHolder> pods, MapParent site) {
            if (site == null || !site.Spawned) {
                return false;
            }
            if (!TransportPodsArrivalActionUtility.AnyNonDownedColonist(pods)) {
                return false;
            }
            if (site.EnterCooldownBlocksEntering()) {
                return FloatMenuAcceptanceReport.WithFailMessage("MessageEnterCooldownBlocksEntering".Translate(site.EnterCooldownDaysLeft().ToString("0.#")));
            }
            return true;
        }

        public static IEnumerable<FloatMenuOption> GetFloatMenuOptions(CompLaunchable representative, IEnumerable<IThingHolder> pods, MapParent site) {
            foreach (FloatMenuOption floatMenuOption in TransportPodsArrivalActionUtility.GetFloatMenuOptions(() => CanVisit(pods, site), () => new TransportPodsArrivalAction_VisitRuins(site, PawnsArrivalModeDefOf.EdgeDrop), "DropAtEdge".Translate(), representative, site.Tile)) {
                yield return floatMenuOption;
            }
            foreach (FloatMenuOption floatMenuOption2 in TransportPodsArrivalActionUtility.GetFloatMenuOptions(() => CanVisit(pods, site), () => new TransportPodsArrivalAction_VisitRuins(site, PawnsArrivalModeDefOf.CenterDrop), "DropInCenter".Translate(), representative, site.Tile)) {
                yield return floatMenuOption2;
            }
        }
    }
}