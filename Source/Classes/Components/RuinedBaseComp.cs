using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RealRuins;

public class RuinedBaseComp : WorldObjectComp
{
	public string blueprintFileName = "";

	private RuinedBaseState state = RuinedBaseState.Inactive;

	public float unlockTargetTime = -1f;

	public int currentCapCost = -1;

	public float raidersActivity = -1f;

	public string expireSignal = null;

	public string successSignal = null;

	private int unlockHysteresisTimeout = 1000;

	private List<RaidTrigger> triggersCache;

	private bool ShouldRemoveWorldObjectNow => state == RuinedBaseState.ScavengedCompletely && !base.ParentHasMap;

	public bool isActive => state != RuinedBaseState.Inactive;

	public bool mapExitLocked => state != RuinedBaseState.InformedWaitingForLeaving;

	public override void Initialize(WorldObjectCompProperties props)
	{
		base.Initialize(props);
	}

	private void BuildRaidTriggersCache()
	{
		if (!base.ParentHasMap)
		{
			return;
		}
		triggersCache = new List<RaidTrigger>();
		foreach (Thing item in (IEnumerable<Thing>)(parent as MapParent).Map.spawnedThings)
		{
			if (item is RaidTrigger)
			{
				triggersCache.Add(item as RaidTrigger);
			}
		}
	}

	private void CheckTriggers()
	{
		if (triggersCache == null)
		{
			BuildRaidTriggersCache();
		}
		foreach (RaidTrigger item in triggersCache)
		{
			if (!item.IsTriggered() || item.TicksLeft() > 0)
			{
				return;
			}
		}
		if (state == RuinedBaseState.FightingWaves)
		{
			if (unlockHysteresisTimeout == 0)
			{
				state = RuinedBaseState.WaitingForEnemiesToBeDefeated;
			}
			else
			{
				unlockHysteresisTimeout--;
			}
		}
	}

	public override void PostMapGenerate()
	{
		base.PostMapGenerate();
		if (state == RuinedBaseState.WaitingForArrival)
		{
			state = RuinedBaseState.FightingWaves;
			BuildRaidTriggersCache();
		}
	}

	public void StartScavenging(int initialCost)
	{
		currentCapCost = initialCost;
		raidersActivity = 0f;
		state = RuinedBaseState.WaitingForArrival;
	}

	public override void PostExposeData()
	{
		base.PostExposeData();
		Scribe_Values.Look(ref blueprintFileName, "blueprintFileName", "");
		Scribe_Values.Look(ref currentCapCost, "currentCapCost", -1);
		Scribe_Values.Look(ref raidersActivity, "raidersActivity", -1f);
		Scribe_Values.Look(ref unlockTargetTime, "unlockTargetTime", 0f);
		Scribe_Values.Look(ref state, "state", RuinedBaseState.Inactive);
		Scribe_Values.Look(ref expireSignal, "expireSignal", "");
		Scribe_Values.Look(ref successSignal, "successSignal", "");
	}

	public override void CompTick()
	{
		base.CompTick();
		if (ShouldRemoveWorldObjectNow)
		{
			string tag = expireSignal;
			Find.SignalManager.SendSignal(new Signal(tag));
			Find.WorldObjects.Remove(parent);
		}
		if (state == RuinedBaseState.Inactive)
		{
			return;
		}
		if (!base.ParentHasMap)
		{
			if (Rand.Chance(1E-05f + raidersActivity / 50000000f) || raidersActivity * 4f > (float)(currentCapCost / 2))
			{
				int val = (int)(Rand.Range(0.05f, 0.2f) * (raidersActivity / 12000f) * (float)currentCapCost);
				currentCapCost -= Math.Max(val, (int)(raidersActivity * 4f));
				raidersActivity /= Rand.Range(2f, 5f);
			}
			raidersActivity += Rand.Range(-0.1f, 0.25f) * (float)currentCapCost / 100000f;
			if (raidersActivity < 0f)
			{
				raidersActivity = 0f;
			}
			if ((currentCapCost < 20000 && Rand.Chance(1E-05f)) || currentCapCost <= 100)
			{
				state = RuinedBaseState.ScavengedCompletely;
			}
		}
		if (base.ParentHasMap && parent.Faction != Faction.OfPlayer && RealRuins_ModSettings.caravanReformType == 1 && !GenHostility.AnyHostileActiveThreatToPlayer((parent as MapParent).Map))
		{
			Debug.Log("Generic", "pristine ruins was cleared, changing faction.");
			parent.SetFaction(Faction.OfPlayer);
		}
		if (RealRuins_ModSettings.caravanReformType != 2 || !base.ParentHasMap)
		{
			return;
		}
		if (state == RuinedBaseState.FightingWaves)
		{
			CheckTriggers();
		}
		else if (state == RuinedBaseState.WaitingForEnemiesToBeDefeated)
		{
			if (!GenHostility.AnyHostileActiveThreatTo((parent as MapParent).Map, Faction.OfPlayer))
			{
				unlockTargetTime = Find.TickManager.TicksGame + Rand.Range(30000, 30000 + currentCapCost);
				state = RuinedBaseState.WaitingTimeoutAfterEnemiesDefeat;
			}
		}
		else if (state == RuinedBaseState.WaitingTimeoutAfterEnemiesDefeat && (float)Find.TickManager.TicksGame > unlockTargetTime)
		{
			state = RuinedBaseState.WaitingToBeInformed;
			string text = "RealRuins.NoMoreEnemies".Translate();
			Messages.Message(text, parent, MessageTypeDefOf.PositiveEvent);
			state = RuinedBaseState.InformedWaitingForLeaving;
		}
	}

	public override string CompInspectStringExtra()
	{
		if (!base.ParentHasMap && state == RuinedBaseState.WaitingForArrival)
		{
			string text = "";
			if (currentCapCost > 0)
			{
				string text2 = "";
				int[] array = new int[5] { 0, 10000, 100000, 1000000, 10000000 };
				if (currentCapCost > array[array.Length - 1])
				{
					text2 = ("RealRuins.RuinsWealth." + (array.Length - 1)).Translate();
				}
				for (int i = 0; i < array.Length - 1; i++)
				{
					if (currentCapCost > array[i] && currentCapCost <= array[i + 1])
					{
						text2 = ("RealRuins.RuinsWealth." + i).Translate();
					}
				}
				text += "RealRuins.RuinsWealth".Translate() + text2;
				if (Prefs.DevMode)
				{
					text = text + " (" + currentCapCost + ")";
				}
			}
			if (raidersActivity > 0f)
			{
				if (text.Length > 0)
				{
					text += "\r\n";
				}
				string text3 = "";
				int[] array2 = new int[5] { 0, 800, 3000, 6000, 12000 };
				if (raidersActivity > (float)array2[array2.Length - 1])
				{
					text3 = ("RealRuins.RuinsActivity." + (array2.Length - 1)).Translate();
				}
				for (int j = 0; j < array2.Length - 1; j++)
				{
					if (raidersActivity > (float)array2[j] && raidersActivity <= (float)array2[j + 1])
					{
						text3 = ("RealRuins.RuinsActivity." + j).Translate();
					}
				}
				text += "RealRuins.RuinsActivity".Translate() + text3;
				if (Prefs.DevMode)
				{
					text = text + " (" + raidersActivity + ")";
				}
			}
			if (text.Length > 0)
			{
				return text;
			}
		}
		return null;
	}
}
