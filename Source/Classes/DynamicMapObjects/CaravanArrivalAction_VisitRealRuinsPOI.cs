using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RealRuins;

internal class CaravanArrivalAction_VisitRealRuinsPOI : CaravanArrivalAction
{
	private MapParent mapParent;

	public override string Label
	{
		get
		{
			if (mapParent.Faction == null || mapParent.Faction == Faction.OfPlayer)
			{
				return "RealRuins.EnterPOI".Translate(mapParent.Label);
			}
			return "RealRuins.AttackPOI".Translate(mapParent.Label);
		}
	}

	public override string ReportString => "RealRuins.CaravanEnteringPOI".Translate(mapParent.Label);

	public CaravanArrivalAction_VisitRealRuinsPOI(MapParent mapParent)
	{
		this.mapParent = mapParent;
	}

	public override void Arrived(Caravan caravan)
	{
		if (!mapParent.HasMap)
		{
			LongEventHandler.QueueLongEvent(delegate
			{
				DoArrivalAction(caravan);
			}, "GeneratingMapForNewEncounter", doAsynchronously: false, null);
		}
		else
		{
			DoArrivalAction(caravan);
		}
	}

	private void DoArrivalAction(Caravan caravan)
	{
		bool flag = !mapParent.HasMap;
		Map orGenerateMap = GetOrGenerateMapUtility.GetOrGenerateMap(mapParent.Tile, null);
		Pawn pawn = caravan.PawnsListForReading[0];
		TaggedString letterText = null;
		TaggedString label = null;
		LetterDef textLetterDef = LetterDefOf.NeutralEvent;
		if (flag)
		{
			if (mapParent.Faction == null)
			{
				label = "LetterLabelCaravanEnteredUnownedPOI".Translate();
				letterText = "LetterCaravanEnteredUnownedPOI".Translate(caravan.Label).CapitalizeFirst();
				textLetterDef = LetterDefOf.NeutralEvent;
			}
			else
			{
				label = "LetterLabelCaravanAttackedPOI".Translate();
				letterText = "LetterCaravanAttackedPOI".Translate(caravan.Label).CapitalizeFirst();
				textLetterDef = LetterDefOf.ThreatBig;
			}
		}
		AffectRelationsIfNeeded(ref letterText);
		Find.LetterStack.ReceiveLetter(label, letterText, textLetterDef, pawn);
		Map map = orGenerateMap;
		bool draftColonists = mapParent.Faction != null && mapParent.Faction != Faction.OfPlayer;
		CaravanEnterMapUtility.Enter(caravan, orGenerateMap, CaravanEnterMode.Edge, CaravanDropInventoryMode.DoNotDrop, draftColonists);
	}

	public static FloatMenuAcceptanceReport CanEnter(Caravan caravan, MapParent mapParent)
	{
		return !(mapParent.GetComponent<EnterCooldownComp>()?.BlocksEntering ?? false);
	}

	public static IEnumerable<FloatMenuOption> GetFloatMenuOptions(Caravan caravan, MapParent mapParent)
	{
		return CaravanArrivalActionUtility.GetFloatMenuOptions(() => CanEnter(caravan, mapParent), () => new CaravanArrivalAction_VisitRealRuinsPOI(mapParent), "EnterMap".Translate(mapParent.Label), caravan, mapParent.Tile, mapParent);
	}

	private void AffectRelationsIfNeeded(ref TaggedString letterText)
	{
		if (mapParent.Faction != null && mapParent.Faction != Faction.OfPlayer)
		{
			FactionRelationKind playerRelationKind = mapParent.Faction.PlayerRelationKind;
			Faction.OfPlayer.TryAffectGoodwillWith(mapParent.Faction, Faction.OfPlayer.GoodwillToMakeHostile(mapParent.Faction), canSendMessage: false, canSendHostilityLetter: false, HistoryEventDefOf.AttackedSettlement);
			mapParent.Faction.TryAppendRelationKindChangedInfo(ref letterText, playerRelationKind, mapParent.Faction.PlayerRelationKind);
		}
	}
}
