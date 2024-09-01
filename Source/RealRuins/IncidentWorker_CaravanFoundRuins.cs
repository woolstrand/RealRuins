using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RealRuins;

public class IncidentWorker_CaravanFoundRuins : IncidentWorker
{
	protected override bool CanFireNowSub(IncidentParms parms)
	{
		if (!base.CanFireNowSub(parms))
		{
			return false;
		}
		if (!SnapshotStoreManager.Instance.CanFireMediumEvent())
		{
			return false;
		}
		if (parms.target is Map)
		{
			return true;
		}
		return CaravanIncidentUtility.CanFireIncidentWhichWantsToGenerateMapAt(parms.target.Tile);
	}

	private bool TryFindEntryCell(Map map, out IntVec3 cell)
	{
		return CellFinder.TryFindRandomEdgeCellWith((IntVec3 x) => x.Standable(map) && map.reachability.CanReachColony(x), map, CellFinder.EdgeRoadChance_Hostile, out cell);
	}

	protected override bool TryExecuteWorker(IncidentParms parms)
	{
		if (parms.target is Map)
		{
			return IncidentDefOf.TravelerGroup.Worker.TryExecute(parms);
		}
		Caravan caravan = (Caravan)parms.target;
		CameraJumper.TryJumpAndSelect(caravan);
		string text = "RealRuins.CaravanFoundRuins".Translate(caravan.Name).CapitalizeFirst();
		DiaNode diaNode = new DiaNode(text);
		DiaOption diaOption = new DiaOption("RealRuins.CaravanFoundRuins.Investigate".Translate());
		DiaOption diaOption2 = new DiaOption("CaravanMeeting_MoveOn".Translate());
		diaOption.action = delegate
		{
			LongEventHandler.QueueLongEvent(delegate
			{
				Pawn pawn = caravan.PawnsListForReading[0];
				Map orGenerateMapForIncident = CaravanIncidentUtility.GetOrGenerateMapForIncident(caravan, new IntVec3(120, 1, 120), DefDatabase<WorldObjectDef>.GetNamed("CaravanSmallRuinsWorldObject"));
				CaravanEnterMapUtility.Enter(caravan, orGenerateMapForIncident, CaravanEnterMode.Edge);
				CameraJumper.TryJumpAndSelect(pawn);
				Find.TickManager.Notify_GeneratedPotentiallyHostileMap();
			}, "GeneratingMapForNewEncounter", doAsynchronously: false, null);
		};
		diaOption.resolveTree = true;
		diaOption2.resolveTree = true;
		diaNode.options.Add(diaOption);
		diaNode.options.Add(diaOption2);
		string title = "RealRuins.CaravanFoundRuinsTitle".Translate(caravan.Label);
		WindowStack windowStack = Find.WindowStack;
		windowStack.Add(new Dialog_NodeTree(diaNode, delayInteractivity: true, radioMode: false, title));
		Find.Archive.Add(new ArchivedDialog(diaNode.text, title));
		return true;
	}
}
