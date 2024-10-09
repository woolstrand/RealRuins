using System;
using RimWorld.QuestGen;

namespace RealRuins;

public class QuestNode_FindBlueprint : QuestNode
{
	public SlateRef<string> storeAs;

	public SlateRef<string> storeCostAs;

	public SlateRef<int> minimumWealth;

	public SlateRef<int> minimumArea;

	public SlateRef<int> minimumItemsCount;

	private string FindBlueprint(Slate slate)
	{
		string filename;
		Blueprint blueprint = BlueprintFinder.FindRandomBlueprintWithParameters(out filename, minimumArea.GetValue(slate), 0.01f, (int)Math.Min(minimumWealth.GetValue(slate), RealRuins_ModSettings.ruinsCostCap), 50);
		if (blueprint != null)
		{
			if ((int)blueprint.totalCost != 0)
			{
				string value = storeCostAs.GetValue(slate);
				if (value != null)
				{
					slate.Set("blueprintCachedCost", (int)blueprint.totalCost);
					int num = slate.Get("blueprintCachedCost", 0);
					Debug.Log("QuestNode_Find", "value: {0}, slate: {1}", num, slate);
					Debug.Log("QuestNode_Find", "Found suitable blueprint {0} of total cost {1}", filename, blueprint.totalCost);
				}
				else
				{
					Debug.Log("QuestNode_Find", "Key for cost cache value is null");
				}
			}
			else
			{
				Debug.Log("QuestNode_Find", "Resulting blueprint cost = 0");
			}
		}
		else
		{
			Debug.Log("QuestNode_Find", "Suitable blueprint not found");
		}
		return filename;
	}

	protected override bool TestRunInt(Slate slate)
	{
		Debug.Log("QuestNode_FindBlueprintNode", "TestRun launched");
		string var = "TEST";
		slate.Set(storeAs.GetValue(slate), var);
		return true;
	}

	protected override void RunInt()
	{
		Debug.Log("QuestNode_FindBlueprintNode", "Real run launched");
		Slate slate = QuestGen.slate;
		string text = FindBlueprint(slate);
		if (text != null)
		{
			slate.Set(storeAs.GetValue(slate), text);
		}
	}
}
