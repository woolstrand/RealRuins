using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using RimWorld;
using RimWorld.Planet;
using Verse;


namespace RealRuins {
    class CaravanArrivalAction_VisitAbandonedBase: CaravanArrivalAction {
        private MapParent mapParent;

        public override string Label => "RealRuins.EnterPOI".Translate();

        public override string ReportString => "RealRuins.CaravanEnteringPOI".Translate();

        public CaravanArrivalAction_VisitAbandonedBase(MapParent mapParent) {
            this.mapParent = mapParent;
        }

        public override void Arrived(Caravan caravan) {
            if (!mapParent.HasMap) {
                LongEventHandler.QueueLongEvent(delegate {
                    DoEnter(caravan);
                }, "GeneratingMapForNewEncounter", doAsynchronously: false, exceptionHandler: null);
            } else {
                DoEnter(caravan);
            }
        }


        private void DoEnter(Caravan caravan) {
            Debug.Log(Debug.Event, "Major event entering.");
            Pawn t = caravan.PawnsListForReading[0];
            bool flag = mapParent.Faction == null || mapParent.Faction.HostileTo(Faction.OfPlayer);
            Debug.Log(Debug.Event, "Will generate map now.........");
            Map orGenerateMap = GetOrGenerateMapUtility.GetOrGenerateMap(mapParent.Tile, null);
            Debug.Log(Debug.Event, "Map generated");

            Find.LetterStack.ReceiveLetter("LetterLabelCaravanEnteredRuins".Translate(), "LetterCaravanEnteredRuins".Translate(caravan.Label).CapitalizeFirst(), LetterDefOf.ThreatBig, t, null, null);

            Map map = orGenerateMap;

            CaravanEnterMode enterMode = CaravanEnterMode.Edge;
            bool draftColonists = flag;
            CaravanEnterMapUtility.Enter(caravan, map, enterMode, CaravanDropInventoryMode.DoNotDrop, draftColonists, null);
        }


        public static FloatMenuAcceptanceReport CanEnter(Caravan caravan, MapParent mapParent) {
            return true;
        }


        public static IEnumerable<FloatMenuOption> GetFloatMenuOptions(Caravan caravan, MapParent mapParent) {
            return CaravanArrivalActionUtility.GetFloatMenuOptions(() => CanEnter(caravan, mapParent), () => new CaravanArrivalAction_VisitAbandonedBase(mapParent), "EnterMap".Translate(mapParent.Label), caravan, mapParent.Tile, mapParent);
        }
    }
}
