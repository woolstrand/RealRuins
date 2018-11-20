using System.Text;
using RimWorld;
using Verse;

using RimWorld.Planet;

namespace RealRuins {
    public class RuinedBaseWorker : SiteCoreWorker {
        public static readonly IntVec3 MapSize = new IntVec3(240, 1, 240);
        
        private void DoEnter(Caravan caravan, Site site)
        {
            Pawn t = caravan.PawnsListForReading[0];
            bool flag = site.Faction == null || site.Faction.HostileTo(Faction.OfPlayer);
            Map orGenerateMap = GetOrGenerateMapUtility.GetOrGenerateMap(site.Tile, MapSize, null);

            Find.LetterStack.ReceiveLetter("LetterLabelCaravanEnteredRuins".Translate(), "LetterCaravanEnteredRuins".Translate(caravan.Label).CapitalizeFirst(), LetterDefOf.ThreatBig, t, null, null);

            Map map = orGenerateMap;
            CaravanEnterMode enterMode = CaravanEnterMode.Edge;
            bool draftColonists = flag;
            CaravanEnterMapUtility.Enter(caravan, map, enterMode, CaravanDropInventoryMode.DoNotDrop, draftColonists, null);
        }
        
        protected new void Enter(Caravan caravan, Site site)
        {
            if (!site.HasMap)
            {
                LongEventHandler.QueueLongEvent(delegate
                {
                    DoEnter(caravan, site);
                }, "GeneratingMapForNewEncounter", false, null);
            }
            else
            {
                DoEnter(caravan, site);
            }
        }
        
        public override void VisitAction(Caravan caravan, Site site)
        {
            Debug.Message("Entering visit action overriden");
            Enter(caravan, site);
        }

    }
}