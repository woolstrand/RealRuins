using RimWorld;
using Verse;
using RimWorld.Planet;

namespace RealRuins {
	public class IncidentWorker_CaravanFoundRuins : IncidentWorker {

		protected override bool CanFireNowSub(IncidentParms parms) {
			if (!base.CanFireNowSub(parms)) {
				return false;
			}

            if (!SnapshotStoreManager.Instance.CanFireMediumEvent()) {
                return false;
            }

            if (parms.target is Map) {
                return true;
            }

            return CaravanIncidentUtility.CanFireIncidentWhichWantsToGenerateMapAt(parms.target.Tile);
        }

        private bool TryFindEntryCell(Map map, out IntVec3 cell) {
            return CellFinder.TryFindRandomEdgeCellWith((IntVec3 x) => x.Standable(map) && map.reachability.CanReachColony(x), map, CellFinder.EdgeRoadChance_Hostile, out cell);
        }

        protected override bool TryExecuteWorker(IncidentParms parms) {
			if (parms.target is Map) {
				return IncidentDefOf.TravelerGroup.Worker.TryExecute(parms);
			}

			Caravan caravan = (Caravan) parms.target;

			CameraJumper.TryJumpAndSelect(caravan);

			string text = "RealRuins.CaravanFoundRuins".Translate(caravan.Name).CapitalizeFirst();

			DiaNode diaNode = new DiaNode(text);

			DiaOption diaOptionInvestigate = new DiaOption("RealRuins.CaravanFoundRuins.Investigate".Translate());
			DiaOption diaOptionMoveOn = new DiaOption("CaravanMeeting_MoveOn".Translate());
			diaOptionInvestigate.action = delegate {
				LongEventHandler.QueueLongEvent(delegate {
					Pawn t = caravan.PawnsListForReading[0];
					
					/*Site site = SiteMaker.MakeSite(SiteCoreDefOf.Nothing, (RimWorld.SitePartDef) null, caravan.Tile, null);
					site.sitePartsKnown = true;
					site.GetComponent<TimeoutComp>().StartTimeout(3 * 60000);
					Find.WorldObjects.Add(site);
					*/

					Map map = CaravanIncidentUtility.GetOrGenerateMapForIncident(caravan, new IntVec3(120, 1, 120),
						DefDatabase<WorldObjectDef>.GetNamed("CaravanSmallRuinsWorldObject"));
					CaravanEnterMapUtility.Enter(caravan, map, CaravanEnterMode.Edge);
					CameraJumper.TryJumpAndSelect(t);
					Find.TickManager.Notify_GeneratedPotentiallyHostileMap();
				}, "GeneratingMapForNewEncounter", false, null);
			};

			diaOptionInvestigate.resolveTree = true;
			diaOptionMoveOn.resolveTree = true;

			diaNode.options.Add(diaOptionInvestigate);
			diaNode.options.Add(diaOptionMoveOn);

			string diaTitle = "RealRuins.CaravanFoundRuinsTitle".Translate(caravan.Label);
			WindowStack windowStack = Find.WindowStack;
			windowStack.Add(new Dialog_NodeTree(diaNode, true, false, diaTitle));
			Find.Archive.Add(new ArchivedDialog(diaNode.text, diaTitle));
			return true;
		}
	}
}