using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using Verse;

namespace RealRuins {
    //Common part of pods/shuttle arrival to ruins world objects (abandoned bases and POIs)
    public abstract class TransportersArrivalAction_VisitRuinsBase : TransportersArrivalAction {
        protected MapParent site;

        protected PawnsArrivalModeDef arrivalMode;

        public override bool GeneratesMap {
            get {
                return true;
            }
        }

        protected TransportersArrivalAction_VisitRuinsBase() {
        }

        protected TransportersArrivalAction_VisitRuinsBase(MapParent site, PawnsArrivalModeDef arrivalMode) {
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

        //Called after the map is generated, drops the pods (or lands the shuttle) on it
        protected void FinishArrival(List<ActiveTransporterInfo> pods, Map map, bool firstVisit, Thing lookTarget) {
            if (firstVisit) {
                Find.TickManager.Notify_GeneratedPotentiallyHostileMap();
                PawnRelationUtility.Notify_PawnsSeenByPlayer_Letter_Send(map.mapPawns.AllPawns, "LetterRelatedPawnsInMapWherePlayerLanded".Translate(Faction.OfPlayer.def.pawnsPlural), LetterDefOf.NeutralEvent, informEvenIfSeenBefore: true);
            } else if (pods.IsShuttle()) {
                Messages.Message("MessageShuttleArrived".Translate(), lookTarget, MessageTypeDefOf.TaskCompletion);
            } else {
                Messages.Message("MessageTransportPodsArrived".Translate(), lookTarget, MessageTypeDefOf.TaskCompletion);
            }

            arrivalMode.Worker.TravellingTransportersArrived(pods, map);
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

        //Used for both drop pods and shuttles, same as vanilla sites do
        protected static IEnumerable<FloatMenuOption> GetFloatMenuOptions(Action<PlanetTile, TransportersArrivalAction> launchAction, IEnumerable<IThingHolder> pods, MapParent site, Func<PawnsArrivalModeDef, TransportersArrivalAction> makeAction) {
            foreach (FloatMenuOption floatMenuOption in TransportersArrivalActionUtility.GetFloatMenuOptions(() => CanVisit(pods, site), () => makeAction(PawnsArrivalModeDefOf.EdgeDrop), "DropAtEdge".Translate(), launchAction, site.Tile)) {
                yield return floatMenuOption;
            }
            foreach (FloatMenuOption floatMenuOption in TransportersArrivalActionUtility.GetFloatMenuOptions(() => CanVisit(pods, site), () => makeAction(PawnsArrivalModeDefOf.CenterDrop), "DropInCenter".Translate(), launchAction, site.Tile)) {
                yield return floatMenuOption;
            }
        }
    }
}
