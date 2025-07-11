using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using Verse;

namespace RealRuins {
    public class TransportPodsArrivalAction_VisitRuins : TransportersArrivalAction {
        private MapParent site;

        private PawnsArrivalModeDef arrivalMode;

        public override bool GeneratesMap {
            get {
                return true;
            }
        }

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

        public override FloatMenuAcceptanceReport StillValid(IEnumerable<IThingHolder> pods, PlanetTile destinationTile) {
            FloatMenuAcceptanceReport floatMenuAcceptanceReport = base.StillValid(pods, destinationTile);
            if (!(bool)floatMenuAcceptanceReport) {
                return floatMenuAcceptanceReport;
            }
            if (site != null && site.Tile != destinationTile) {
                return false;
            }
            return CanVisit(pods, site);
        }

        public override bool ShouldUseLongEvent(List<ActiveTransporterInfo> pods, PlanetTile tile) {
            return !site.HasMap;
        }

        public override void Arrived(List<ActiveTransporterInfo> pods, PlanetTile tile) {
            Debug.Log("Overridden arrive pods - visit ruins");
            Thing lookTarget = TransportersArrivalActionUtility.GetLookTarget(pods);
            bool flag = !site.HasMap;

            Map orGenerateMap = GetOrGenerateMapUtility.GetOrGenerateMap(site.Tile, /*new IntVec3(250, 0, 250),*/ null);
            Debug.Log("Generated encounter map, which is {0}", orGenerateMap?.ToString() ?? "NULL");
            if (flag) {
                Find.TickManager.Notify_GeneratedPotentiallyHostileMap();
                PawnRelationUtility.Notify_PawnsSeenByPlayer_Letter_Send(orGenerateMap.mapPawns.AllPawns, "LetterRelatedPawnsInMapWherePlayerLanded".Translate(Faction.OfPlayer.def.pawnsPlural), LetterDefOf.NeutralEvent, informEvenIfSeenBefore: true);

                Find.LetterStack.ReceiveLetter("LetterLabelTransportPodsArrivedAtRuins".Translate(), "LetterTransportPodsArrivedAtRuins".Translate().CapitalizeFirst(), LetterDefOf.ThreatBig, lookTarget, null, null);
            } else {
                Messages.Message("MessageTransportPodsArrived".Translate(), lookTarget, MessageTypeDefOf.TaskCompletion);
            }

            arrivalMode.Worker.TravellingTransportersArrived(pods, orGenerateMap);
        }



        public static FloatMenuAcceptanceReport CanVisit(IEnumerable<IThingHolder> pods, MapParent site) {
            if (site == null || !site.Spawned) {
                return false;
            }
            if (!TransportersArrivalActionUtility.AnyNonDownedColonist(pods)) {
                return false;
            }
            if (site.EnterCooldownBlocksEntering()) {
                return FloatMenuAcceptanceReport.WithFailMessage("MessageEnterCooldownBlocksEntering".Translate(site.EnterCooldownDaysLeft().ToString("0.#")));
            }
            return true;
        }

        public static IEnumerable<FloatMenuOption> GetFloatMenuOptions(Action<PlanetTile, TransportersArrivalAction> arrivalAction, IEnumerable<IThingHolder> pods, MapParent site) {
            foreach (FloatMenuOption floatMenuOption in TransportersArrivalActionUtility.GetFloatMenuOptions(() => CanVisit(pods, site), () => new TransportPodsArrivalAction_VisitRuins(site, PawnsArrivalModeDefOf.EdgeDrop), "DropAtEdge".Translate(), arrivalAction, site.Tile)) {
                yield return floatMenuOption;
            }
            foreach (FloatMenuOption floatMenuOption2 in TransportersArrivalActionUtility.GetFloatMenuOptions(() => CanVisit(pods, site), () => new TransportPodsArrivalAction_VisitRuins(site, PawnsArrivalModeDefOf.CenterDrop), "DropInCenter".Translate(), arrivalAction, site.Tile)) {
                yield return floatMenuOption2;
            }
        }
    }
}