using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using RimWorld;
using Verse;

using RimWorld.Planet;

namespace RealRuins {
    class RealRuinsPOIWorker : SiteCoreWorker {
        public static new IntVec3 MapSize = new IntVec3(250, 0, 250);

        private void DoEnter(Caravan caravan, Site site) {
            MapSize = Find.World.info.initialMapSize;

            Pawn t = caravan.PawnsListForReading[0];
            bool flag = site.Faction == null || site.Faction.HostileTo(Faction.OfPlayer);
            Map orGenerateMap = GetOrGenerateMapUtility.GetOrGenerateMap(site.Tile, MapSize, null);

            Find.LetterStack.ReceiveLetter("LetterLabelCaravanEnteredPOI".Translate(), "LetterCaravanEnteredPOI".Translate(caravan.Label).CapitalizeFirst(), LetterDefOf.ThreatBig, t, null, null);

            Map map = orGenerateMap;


            CaravanEnterMode enterMode = CaravanEnterMode.Edge;
            bool draftColonists = flag;
            CaravanEnterMapUtility.Enter(caravan, map, enterMode, CaravanDropInventoryMode.DoNotDrop, draftColonists, null);

        }

        protected new void Enter(Caravan caravan, Site site) {
            if (!site.HasMap) {
                LongEventHandler.QueueLongEvent(delegate {
                    DoEnter(caravan, site);
                }, "GeneratingMapForNewEncounter", false, null);
            } else {
                DoEnter(caravan, site);
            }
        }

        public override void VisitAction(Caravan caravan, Site site) {
            Enter(caravan, site);
        }

        public override IEnumerable<FloatMenuOption> GetTransportPodsFloatMenuOptions(IEnumerable<IThingHolder> pods, CompLaunchable representative, Site site) {
            Debug.Message("overriden transport pod options");
            foreach (FloatMenuOption floatMenuOption in TransportPodsArrivalAction_VisitRuins.GetFloatMenuOptions(representative, pods, site)) {
                yield return floatMenuOption;
            }
        }
    }
}
