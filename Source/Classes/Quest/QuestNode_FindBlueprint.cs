using System;
using RimWorld;
using RimWorld.QuestGen;

namespace RealRuins
{
	public class QuestNode_FindBlueprint : QuestNode
	{

		public SlateRef<string> storeAs;

		public SlateRef<int> minimumWealth;
		public SlateRef<int> minimumArea;
		public SlateRef<int> minimumItemsCount;

		private string FindBlueprint(Slate slate) {
			string filename;
			BlueprintFinder.FindRandomBlueprintWithParameters(out filename, minimumArea.GetValue(slate), 0.01f, (int)Math.Min(minimumWealth.GetValue(slate), RealRuins_ModSettings.ruinsCostCap), maxAttemptsCount: 20);
			Debug.Log(Debug.QuestNode_Find, "Found suitable blueprint");
			return filename;
		}

		protected override bool TestRunInt(Slate slate) {
			return true;
        }

		protected override void RunInt() {
			Slate slate = QuestGen.slate;
			var filename = FindBlueprint(slate);
			slate.Set(storeAs.GetValue(slate), filename);
		}
	}
}

