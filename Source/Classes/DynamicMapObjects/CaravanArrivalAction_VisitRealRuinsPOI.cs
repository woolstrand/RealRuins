using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RealRuins {
    class CaravanArrivalAction_VisitRealRuinsPOI : CaravanArrivalAction {

        private MapParent mapParent;

        public override string Label => "RealRuins.EnterPOI".Translate();

        public override string ReportString => "RealRuins.CaravanEnteringPOI".Translate();

        public CaravanArrivalAction_VisitRealRuinsPOI(MapParent mapParent) {
            this.mapParent = mapParent;
        }

        public override void Arrived(Caravan caravan) {

            if (!mapParent.HasMap) {
                LongEventHandler.QueueLongEvent(delegate
                {
                    DoArrivalAction(caravan);
                }, "GeneratingMapForNewEncounter", doAsynchronously: false, exceptionHandler: null);
            } else {
                DoArrivalAction(caravan);
            }
        }

        private void DoArrivalAction(Caravan caravan) {
            bool flag = !mapParent.HasMap;
            if (flag) {
                mapParent.SetFaction(Faction.OfPlayer);
            }
            Map orGenerateMap = GetOrGenerateMapUtility.GetOrGenerateMap(mapParent.Tile, null);
            Pawn t = caravan.PawnsListForReading[0];
            CaravanEnterMapUtility.Enter(caravan, orGenerateMap, CaravanEnterMode.Edge, CaravanDropInventoryMode.UnloadIndividually);
            //add letters
        }


        public static FloatMenuAcceptanceReport CanEnter(Caravan caravan, MapParent mapParent) {
            return true;
        }


        public static IEnumerable<FloatMenuOption> GetFloatMenuOptions(Caravan caravan, MapParent mapParent) {
            return CaravanArrivalActionUtility.GetFloatMenuOptions(() => CanEnter(caravan, mapParent), () => new CaravanArrivalAction_VisitRealRuinsPOI(mapParent), "EnterMap".Translate(mapParent.Label), caravan, mapParent.Tile, mapParent);
        }
    }
}
