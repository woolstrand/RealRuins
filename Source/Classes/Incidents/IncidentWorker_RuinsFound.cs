using System;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RealRuins;

public class IncidentWorker_RuinsFound : IncidentWorker
{
	protected override bool CanFireNowSub(IncidentParms parms)
	{
		if (!base.CanFireNowSub(parms))
		{
			return false;
		}
		if (!SnapshotStoreManager.Instance.CanFireLargeEvent())
		{
			return false;
		}
		return Find.FactionManager.RandomNonHostileFaction(allowHidden: false, allowDefeated: false, allowNonHumanlike: false) != null;
	}

	protected override bool TryExecuteWorker(IncidentParms parms)
	{
		Debug.Log("Event", "Starting incident worker for ruins major event");
		Faction faction = parms.faction;
		if (faction == null)
		{
			faction = Find.FactionManager.RandomNonHostileFaction(allowHidden: false, allowDefeated: false, allowNonHumanlike: false);
		}
		if (faction == null)
		{
			return false;
		}
		if (!TryFindTile(out var tile))
		{
			return false;
		}
		AbandonedBaseWorldObject abandonedBaseWorldObject = (AbandonedBaseWorldObject)WorldObjectMaker.MakeWorldObject(DefDatabase<WorldObjectDef>.GetNamed("AbandonedBase"));
		abandonedBaseWorldObject.Tile = tile;
		abandonedBaseWorldObject.SetFaction(null);
		Find.WorldObjects.Add(abandonedBaseWorldObject);
		Debug.Log("Event", "Created world object");
		string filename = null;
		Blueprint blueprint = BlueprintFinder.FindRandomBlueprintWithParameters(out filename, 6400, 0.01f, (int)Math.Min(30000f, RealRuins_ModSettings.ruinsCostCap), 50);
		if (blueprint != null)
		{
			Debug.Log("Event", "Found suitable blueprint at path {0}", filename);
			RuinedBaseComp component = abandonedBaseWorldObject.GetComponent<RuinedBaseComp>();
			if (component == null)
			{
				Debug.Error("Event", "RuinedBase component is null during abandoned base event creation.");
				return false;
			}
			component.blueprintFileName = filename;
			float ruinsCostCap = RealRuins_ModSettings.ruinsCostCap;
			float num = Math.Min(ruinsCostCap, blueprint.totalCost);
			if (num > 2.1474836E+09f)
			{
				num = 2.1474836E+09f;
			}
			Debug.Log("Event", "Initial cost set to {0} (blueprint cost {1}, settings cap {2}", num, blueprint.totalCost, ruinsCostCap);
			component.StartScavenging((int)num);
			int timeoutDays = (int)(Math.Pow(abandonedBaseWorldObject.GetComponent<RuinedBaseComp>().currentCapCost / 1000, 0.41) * 1.1);
			string letterText = GetLetterText(faction, timeoutDays);
			Find.LetterStack.ReceiveLetter(def.letterLabel, letterText, def.letterDef, abandonedBaseWorldObject, faction);
			Debug.Log("Event", "Event preparations completed, blueprint is ready and stored, letter sent.");
			return true;
		}
		Debug.Warning("Event", "Could not found suitable blueprint!");
		return false;
	}

	private bool TryFindTile(out int tile)
	{
		IntRange intRange = new IntRange(5, 30);
		return TileFinder.TryFindNewSiteTile(out tile, intRange.min, intRange.max, allowCaravans: false, TileFinderMode.Random);
	}

	private string GetLetterText(Faction alliedFaction, int timeoutDays)
	{
		return string.Format(def.letterText, alliedFaction.leader.LabelShort, alliedFaction.def.leaderTitle, alliedFaction.Name, timeoutDays).CapitalizeFirst();
	}
}
