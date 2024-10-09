using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RealRuins;

internal class CaravanArrivalAction_VisitAbandonedBase : CaravanArrivalAction
{
	private MapParent mapParent;

	public override string Label => "RealRuins.EnterPOI".Translate();

	public override string ReportString => "RealRuins.CaravanEnteringPOI".Translate();

	public CaravanArrivalAction_VisitAbandonedBase(MapParent mapParent)
	{
		this.mapParent = mapParent;
	}

	public override void Arrived(Caravan caravan)
	{
		if (!mapParent.HasMap)
		{
			LongEventHandler.QueueLongEvent(delegate
			{
				DoEnter(caravan);
			}, "GeneratingMapForNewEncounter", doAsynchronously: false, null);
		}
		else
		{
			DoEnter(caravan);
		}
	}

	private void DoEnter(Caravan caravan)
	{
		Debug.Log("Event", "Major event entering.");
		Pawn pawn = caravan.PawnsListForReading[0];
		bool flag = mapParent.Faction == null || mapParent.Faction.HostileTo(Faction.OfPlayer);
		Debug.Log("Event", "Will generate map now.........");
		Map orGenerateMap = GetOrGenerateMapUtility.GetOrGenerateMap(mapParent.Tile, null);
		Debug.Log("Event", "Map generated");
		Find.LetterStack.ReceiveLetter("LetterLabelCaravanEnteredRuins".Translate(), "LetterCaravanEnteredRuins".Translate(caravan.Label).CapitalizeFirst(), LetterDefOf.ThreatBig, pawn);
		Map map = orGenerateMap;
		CaravanEnterMode enterMode = CaravanEnterMode.Edge;
		bool draftColonists = flag;
		CaravanEnterMapUtility.Enter(caravan, map, enterMode, CaravanDropInventoryMode.DoNotDrop, draftColonists);
	}

	public static FloatMenuAcceptanceReport CanEnter(Caravan caravan, MapParent mapParent)
	{
		return true;
	}

	public static IEnumerable<FloatMenuOption> GetFloatMenuOptions(Caravan caravan, MapParent mapParent)
	{
		return CaravanArrivalActionUtility.GetFloatMenuOptions(() => CanEnter(caravan, mapParent), () => new CaravanArrivalAction_VisitAbandonedBase(mapParent), "EnterMap".Translate(mapParent.Label), caravan, mapParent.Tile, mapParent);
	}
}
