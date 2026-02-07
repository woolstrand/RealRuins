using System;
using RimWorld;
using RimWorld.QuestGen;
using Verse;

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
			Debug.Extra(Debug.QuestNode_FindBlueprint, "FindBlueprint: Starting blueprint search");
			
			int minArea = minimumArea.GetValue(slate);
			int minWealth = minimumWealth.GetValue(slate);
			long wealthCap = (long)RealRuins_ModSettings.ruinsCostCap;
			long effectiveMinWealth = (long)Math.Min(minWealth, wealthCap);
			
			Debug.Extra(Debug.QuestNode_FindBlueprint, "FindBlueprint: Parameters - minArea={0}, minWealth={1}, wealthCap={2}, effectiveMinWealth={3}", 
				minArea, minWealth, wealthCap, effectiveMinWealth);
			
			string filename;
			Blueprint bp = BlueprintFinder.FindRandomBlueprintWithParameters(out filename, minArea, 0.01f, (int)effectiveMinWealth, maxAttemptsCount: 50);
			
			Debug.Extra(Debug.QuestNode_FindBlueprint, "FindBlueprint: BlueprintFinder returned - bp={0}, filename={1}", 
				bp != null ? "NOT NULL" : "NULL", filename ?? "NULL");
			
			if (bp != null) {
				int intCost = (int)bp.totalCost;
				Debug.Extra(Debug.QuestNode_FindBlueprint, "FindBlueprint: Blueprint found - filename={0}, totalCost={1}, intCost={2}", 
					filename, bp.totalCost, intCost);
				
				if (intCost != 0) {
					string key = storeCostAs.GetValue(slate);
					Debug.Extra(Debug.QuestNode_FindBlueprint, "FindBlueprint: storeCostAs key={0}", key ?? "NULL");
					
					if (key != null) {
						slate.Set("blueprintCachedCost", (int)(bp.totalCost));
						var storedCost = slate.Get<int>("blueprintCachedCost");
                        Debug.Extra(Debug.QuestNode_Find, "value: {0}, slate: {1}", storedCost, slate);

						//success
                        Debug.Extra(Debug.QuestNode_Find, "Found suitable blueprint {0} of total cost {1}", filename, bp.totalCost);
                    } else {
                        Debug.Extra(Debug.QuestNode_Find, "Key for cost cache value is null");
                    }
                } else {
                    Debug.Extra(Debug.QuestNode_Find, "Resulting blueprint cost = 0");
                }
            } else {
                Debug.Extra(Debug.QuestNode_FindBlueprint, "FindBlueprint: No blueprint found! filename={0}", filename ?? "NULL");
                Debug.Warning(Debug.QuestNode_FindBlueprint, "FindBlueprint: Failed to find suitable blueprint with parameters: minArea={0}, minWealth={1}", minArea, effectiveMinWealth);
            }
			
			Debug.Extra(Debug.QuestNode_FindBlueprint, "FindBlueprint: Returning filename={0}", filename ?? "NULL");
			return filename;
		}

		protected override bool TestRunInt(Slate slate) {
            Debug.Log(Debug.QuestNode_FindBlueprintNode, "TestRun launched for RealRuins_AbandonedBase quest");
            var filename = "TEST";
            string storeAsKey = storeAs.GetValue(slate);
            Debug.Log(Debug.QuestNode_FindBlueprintNode, "TestRun: storeAs key={0}", storeAsKey ?? "NULL");
            slate.Set(storeAsKey, filename);
            Debug.Log(Debug.QuestNode_FindBlueprintNode, "TestRun: Set slate[{0}]=TEST, returning true", storeAsKey);
            return true;
        }

		protected override void RunInt() {
            Debug.Log(Debug.QuestNode_FindBlueprintNode, "RunInt: Starting real run for RealRuins_AbandonedBase quest");
            try {
                Slate slate = QuestGen.slate;
                
                string storeAsKey = storeAs.GetValue(slate);
                Debug.Log(Debug.QuestNode_FindBlueprintNode, "RunInt: storeAs key={0}", storeAsKey ?? "NULL");
                
                var filename = FindBlueprint(slate);
                
                Debug.Log(Debug.QuestNode_FindBlueprintNode, "RunInt: FindBlueprint returned filename={0}", filename ?? "NULL");
                
                if (filename != null && filename != "" && filename != "TEST") {
                    slate.Set(storeAsKey, filename);
                    var verifyValue = slate.Get<string>(storeAsKey);
                    Debug.Log(Debug.QuestNode_FindBlueprintNode, "RunInt: Successfully set slate[{0}]={1}, verified={2}", 
                        storeAsKey, filename, verifyValue ?? "NULL");
                } else {
                    Debug.Error(Debug.QuestNode_FindBlueprintNode, "RunInt: No blueprint found! filename={0}, storeAsKey={1}. Quest will fail.", 
                        filename ?? "NULL", storeAsKey ?? "NULL");
                }
            } catch (Exception ex) {
                Debug.Error(Debug.QuestNode_FindBlueprintNode, "RunInt: Exception occurred: {0}", ex);
                throw;
            }
		}
	}
}

