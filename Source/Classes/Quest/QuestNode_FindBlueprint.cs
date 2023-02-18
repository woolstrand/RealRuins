using System;
using RimWorld;
using RimWorld.QuestGen;

namespace RealRuins
{
	public class QuestNode_FindBlueprint : QuestNode
	{

        public SlateRef<string> storeAs;
        public SlateRef<string> storeCostAs;

        public SlateRef<int> minimumWealth;
		public SlateRef<int> minimumArea;
		public SlateRef<int> minimumItemsCount;

		private string FindBlueprint(Slate slate) {
			string filename;
			Blueprint bp = BlueprintFinder.FindRandomBlueprintWithParameters(out filename, minimumArea.GetValue(slate), 0.01f, (int)Math.Min(minimumWealth.GetValue(slate), RealRuins_ModSettings.ruinsCostCap), maxAttemptsCount: 50);
			if (bp != null) {
				int intCost = (int)bp.totalCost;
				if (intCost != 0) {
					string key = storeCostAs.GetValue(slate);
					if (key != null) {
						slate.Set("blueprintCachedCost", (int)(bp.totalCost));
						var storedCost = slate.Get<int>("blueprintCachedCost");
                        Debug.Log(Debug.QuestNode_Find, "value: {0}, slate: {1}", storedCost, slate);

						//success
                        Debug.Log(Debug.QuestNode_Find, "Found suitable blueprint {0} of total cost {1}", filename, bp.totalCost);
                    } else {
                        Debug.Log(Debug.QuestNode_Find, "Key for cost cache value is null");
                    }
                } else {
                    Debug.Log(Debug.QuestNode_Find, "Resulting blueprint cost = 0");
                }
            } else {
                Debug.Log(Debug.QuestNode_Find, "Suitable blueprint not found");
            }
			return filename;
		}

		protected override bool TestRunInt(Slate slate) {
            Debug.Log("QuestNode_FindBlueprintNode", "TestRun launched");
            var filename = "TEST";
            slate.Set(storeAs.GetValue(slate), filename);
            return true;
        }

		protected override void RunInt() {
            Debug.Log("QuestNode_FindBlueprintNode", "Real run launched");
            Slate slate = QuestGen.slate;
			var filename = FindBlueprint(slate);
			if (filename != null) {
				slate.Set(storeAs.GetValue(slate), filename);
			}
		}
	}
}

