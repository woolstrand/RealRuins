using RimWorld;
using Verse;
using RimWorld.Planet;

namespace RealRuins {
    public class IncidentWorker_CaravanFoundRuins : IncidentWorker {
    
	protected override bool CanFireNowSub(IncidentParms parms)
	{
		if (!base.CanFireNowSub(parms))
		{
			return false;
		}

		return true;
	}

	protected override bool TryExecuteWorker(IncidentParms parms)
	{
		if (parms.target is Map)
		{
			return IncidentDefOf.TravelerGroup.Worker.TryExecute(parms);
		}
		Caravan caravan = (Caravan)parms.target;
		if (!TryFindFaction(out Faction faction))
		{
			return false;
		}
		CameraJumper.TryJumpAndSelect(caravan);
		List<Pawn> pawns = GenerateCaravanPawns(faction);
		Caravan metCaravan = CaravanMaker.MakeCaravan(pawns, faction, -1, false);
		string text = "CaravanMeeting".Translate(caravan.Name, faction.Name, PawnUtility.PawnKindsToCommaList(metCaravan.PawnsListForReading, true)).CapitalizeFirst();
		DiaNode diaNode = new DiaNode(text);
		Pawn bestPlayerNegotiator = BestCaravanPawnUtility.FindBestNegotiator(caravan);
		if (metCaravan.CanTradeNow)
		{
			DiaOption diaOption = new DiaOption("CaravanMeeting_Trade".Translate());
			diaOption.action = delegate
			{
				Find.WindowStack.Add(new Dialog_Trade(bestPlayerNegotiator, metCaravan, false));
				PawnRelationUtility.Notify_PawnsSeenByPlayer_Letter_Send(metCaravan.Goods.OfType<Pawn>(), "LetterRelatedPawnsTradingWithOtherCaravan".Translate(Faction.OfPlayer.def.pawnsPlural), LetterDefOf.NeutralEvent, false, true);
			};
			if (bestPlayerNegotiator == null)
			{
				diaOption.Disable("CaravanMeeting_TradeIncapable".Translate());
			}
			diaNode.options.Add(diaOption);
		}
		DiaOption diaOption2 = new DiaOption("CaravanMeeting_Attack".Translate());
		diaOption2.action = delegate
		{
			LongEventHandler.QueueLongEvent(delegate
			{
				Pawn t = caravan.PawnsListForReading[0];
				Faction faction3 = faction;
				Faction ofPlayer = Faction.OfPlayer;
				FactionRelationKind kind = FactionRelationKind.Hostile;
				string reason = "GoodwillChangedReason_AttackedCaravan".Translate();
				faction3.TrySetRelationKind(ofPlayer, kind, true, reason, t);
				Map map = CaravanIncidentUtility.GetOrGenerateMapForIncident(caravan, new IntVec3(100, 1, 100), WorldObjectDefOf.AttackedNonPlayerCaravan);
				MultipleCaravansCellFinder.FindStartingCellsFor2Groups(map, out IntVec3 playerSpot, out IntVec3 enemySpot);
				CaravanEnterMapUtility.Enter(caravan, map, (Pawn p) => CellFinder.RandomClosewalkCellNear(playerSpot, map, 12, null), CaravanDropInventoryMode.DoNotDrop, true);
				List<Pawn> list = metCaravan.PawnsListForReading.ToList();
				CaravanEnterMapUtility.Enter(metCaravan, map, (Pawn p) => CellFinder.RandomClosewalkCellNear(enemySpot, map, 12, null), CaravanDropInventoryMode.DoNotDrop, false);
				LordMaker.MakeNewLord(faction, new LordJob_DefendAttackedTraderCaravan(list[0].Position), map, list);
				Find.TickManager.Notify_GeneratedPotentiallyHostileMap();
				CameraJumper.TryJumpAndSelect(t);
				PawnRelationUtility.Notify_PawnsSeenByPlayer_Letter_Send(list, "LetterRelatedPawnsGroupGeneric".Translate(Faction.OfPlayer.def.pawnsPlural), LetterDefOf.NeutralEvent, true, true);
			}, "GeneratingMapForNewEncounter", false, null);
		};
		diaOption2.resolveTree = true;
		diaNode.options.Add(diaOption2);
		DiaOption diaOption3 = new DiaOption("CaravanMeeting_MoveOn".Translate());
		diaOption3.action = delegate
		{
			RemoveAllPawnsAndPassToWorld(metCaravan);
		};
		diaOption3.resolveTree = true;
		diaNode.options.Add(diaOption3);
		string text2 = "CaravanMeetingTitle".Translate(caravan.Label);
		WindowStack windowStack = Find.WindowStack;
		DiaNode nodeRoot = diaNode;
		Faction faction2 = faction;
		bool delayInteractivity = true;
		string title = text2;
		windowStack.Add(new Dialog_NodeTreeWithFactionInfo(nodeRoot, faction2, delayInteractivity, false, title));
		Find.Archive.Add(new ArchivedDialog(diaNode.text, text2, faction));
		return true;
	}

	private bool TryFindTile(out int tile)
	{
		IntRange itemStashQuestSiteDistanceRange = new IntRange(5, 30);
		return TileFinder.TryFindNewSiteTile(out tile, itemStashQuestSiteDistanceRange.min, itemStashQuestSiteDistanceRange.max, false, true, -1);
	}

	public static Site CreateSite(int tile, SitePartDef sitePart, int days, Faction siteFaction)
	{
		Site site = SiteMaker.MakeSite(DefDatabase<SiteCoreDef>.GetNamed("RuinedBaseSite"), sitePart, tile, siteFaction, true, null);
		site.sitePartsKnown = true;
		site.GetComponent<TimeoutComp>().StartTimeout(days * 60000);
		Find.WorldObjects.Add(site);
		return site;
	}

	private string GetLetterText(Faction alliedFaction, int timeoutDays)
	{
		string text = string.Format(def.letterText, alliedFaction.leader.LabelShort, alliedFaction.def.leaderTitle, alliedFaction.Name, timeoutDays).CapitalizeFirst();
		return text;
	}
    }
    }
}