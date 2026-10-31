using RimWorld.Planet;
using Verse;

namespace RealRuins {
    //Common part of caravan arrival to ruins world objects (abandoned bases and POIs)
    abstract class CaravanArrivalAction_VisitRuinsBase : CaravanArrivalAction {
        protected MapParent mapParent;

        //Needed to load the action from a save while the caravan is still on its way
        protected CaravanArrivalAction_VisitRuinsBase() {
        }

        protected CaravanArrivalAction_VisitRuinsBase(MapParent mapParent) {
            this.mapParent = mapParent;
        }

        public override void ExposeData() {
            base.ExposeData();
            Scribe_References.Look(ref mapParent, "mapParent");
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

        protected abstract void DoEnter(Caravan caravan);
    }
}
