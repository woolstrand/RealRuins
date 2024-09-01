using System;
using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using Verse;

namespace RealRuins;

public class QuestNode_GenerateRuinsObject : QuestNode
{
	public SlateRef<string> blueprintFilename;

	public SlateRef<int> blueprintCachedCost;

	public SlateRef<int> tile;

	public SlateRef<string> storeAs;

	private AbandonedBaseWorldObject TryGenerateWorldObject(Slate slate)
	{
		string value = blueprintFilename.GetValue(slate);
		int value2 = blueprintCachedCost.GetValue(slate);
		int num = slate.Get("blueprintCachedCost", 0);
		int value3 = tile.GetValue(slate);
		AbandonedBaseWorldObject abandonedBaseWorldObject = (AbandonedBaseWorldObject)WorldObjectMaker.MakeWorldObject(DefDatabase<WorldObjectDef>.GetNamed("AbandonedBase"));
		abandonedBaseWorldObject.Tile = value3;
		abandonedBaseWorldObject.SetFaction(null);
		RuinedBaseComp component = abandonedBaseWorldObject.GetComponent<RuinedBaseComp>();
		if (component == null)
		{
			Debug.Warning("Component is null");
		}
		else
		{
			component.blueprintFileName = value;
			float ruinsCostCap = RealRuins_ModSettings.ruinsCostCap;
			float num2 = Math.Min(ruinsCostCap, Math.Max(value2, num));
			if (num2 > 2.1474836E+09f)
			{
				num2 = 2.1474836E+09f;
			}
			Debug.Log("Event", "Initial cost set to {0} (blueprint cost {1} OR {3}, settings cap {2}", num2, value2, ruinsCostCap, num);
			component.StartScavenging((int)num2);
			component.successSignal = QuestGenUtility.HardcodedSignalWithQuestID("ruins.LeftAlive");
			component.expireSignal = QuestGenUtility.HardcodedSignalWithQuestID("ruins.Scavenged");
		}
		return abandonedBaseWorldObject;
	}

	protected override bool TestRunInt(Slate slate)
	{
		Debug.Log("QuestNode_GenerateRuinsNode", "TestRun Launched");
		int value = tile.GetValue(slate);
		AbandonedBaseWorldObject abandonedBaseWorldObject = (AbandonedBaseWorldObject)WorldObjectMaker.MakeWorldObject(DefDatabase<WorldObjectDef>.GetNamed("AbandonedBase"));
		abandonedBaseWorldObject.Tile = value;
		slate.Set(storeAs.GetValue(slate), abandonedBaseWorldObject);
		return true;
	}

	protected override void RunInt()
	{
		Slate slate = QuestGen.slate;
		Debug.Log("QuestNode_GenerateRuinsNode", "Real run launched");
		AbandonedBaseWorldObject var = TryGenerateWorldObject(slate);
		slate.Set(storeAs.GetValue(slate), var);
	}
}
