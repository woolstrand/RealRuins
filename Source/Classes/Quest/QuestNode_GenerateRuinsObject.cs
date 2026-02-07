using System;
using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using Verse;

namespace RealRuins
{
	public class QuestNode_GenerateRuinsObject: QuestNode {

        public SlateRef<string> blueprintFilename;
        public SlateRef<int> blueprintCachedCost;
        public SlateRef<PlanetTile> tile; // tile where object to be placed
		public SlateRef<string> storeAs;

		private AbandonedBaseWorldObject TryGenerateWorldObject(Slate slate) {
            Debug.Extra(Debug.QuestNode_GenerateRuinsObject, "TryGenerateWorldObject: Starting");
            
            var filename = blueprintFilename.GetValue(slate);
            var cachedCost = blueprintCachedCost.GetValue(slate);
            var cachedCost2 = slate.Get<int>("blueprintCachedCost");
            var tileId = tile.GetValue(slate);
            
            Debug.Extra(Debug.QuestNode_GenerateRuinsObject, "TryGenerateWorldObject: Retrieved values - filename={0}, cachedCost={1}, cachedCost2={2}, tileId={3}", 
                filename ?? "NULL", cachedCost, cachedCost2, tileId);

            AbandonedBaseWorldObject worldObject = (AbandonedBaseWorldObject)WorldObjectMaker.MakeWorldObject(DefDatabase<WorldObjectDef>.GetNamed("AbandonedBase"));
            worldObject.Tile = tileId;
            worldObject.SetFaction(null);
            Debug.Extra(Debug.QuestNode_GenerateRuinsObject, "TryGenerateWorldObject: Created world object at tile {0}", tileId);

            RuinedBaseComp comp = worldObject.GetComponent<RuinedBaseComp>();
            if (comp == null) {
                Debug.Warning(Debug.QuestNode_GenerateRuinsObject, "TryGenerateWorldObject: Component is null!");
            } else {
                Debug.Extra(Debug.QuestNode_GenerateRuinsObject, "TryGenerateWorldObject: Setting blueprintFileName={0}", filename ?? "NULL");
                comp.blueprintFileName = filename;
                
                // Here we have to determine starting value. Ruins value will decrease over time (lore: scavenged by other factions)
                // However, we do not want it to be initially larger than total wealth cap.

                float costCap = RealRuins_ModSettings.ruinsCostCap;
                float startingCap = Math.Min(costCap, Math.Max(cachedCost, cachedCost2));
                if (startingCap > int.MaxValue) {
                    startingCap = int.MaxValue - 1; //not sure why StartScavenging takes int as input, but don't want to change it now.
                }

                Debug.Log(Debug.Event, "Initial cost set to {0} (blueprint cost {1} OR {3}, settings cap {2}", startingCap, cachedCost, costCap, cachedCost2);
                Debug.Extra(Debug.QuestNode_GenerateRuinsObject, "TryGenerateWorldObject: Calling StartScavenging with startingCap={0}", startingCap);
                comp.StartScavenging((int)startingCap); //passing initial cost to calculate coefficients without need to load bp one more time

                comp.successSignal = QuestGenUtility.HardcodedSignalWithQuestID("ruins.LeftAlive");
                comp.expireSignal = QuestGenUtility.HardcodedSignalWithQuestID("ruins.Scavenged");
                Debug.Extra(Debug.QuestNode_GenerateRuinsObject, "TryGenerateWorldObject: Set signals - success={0}, expire={1}", 
                    comp.successSignal ?? "NULL", comp.expireSignal ?? "NULL");
            }

            Debug.Extra(Debug.QuestNode_GenerateRuinsObject, "TryGenerateWorldObject: Returning world object");
            return worldObject;
        }

        protected override bool TestRunInt(Slate slate) {
            Debug.Log(Debug.QuestNode_GenerateRuinsNode, "TestRun Launched for RealRuins_AbandonedBase quest");
            
            // Check if blueprintFilename exists in slate (set by previous node)
            var testFilename = blueprintFilename.GetValue(slate);
            Debug.Log(Debug.QuestNode_GenerateRuinsNode, "TestRun: blueprintFilename from slate={0}", testFilename ?? "NULL");

            var tileId = tile.GetValue(slate);
            Debug.Log(Debug.QuestNode_GenerateRuinsNode, "TestRun: tileId={0}", tileId);
            
            if (tileId == 0) {
                Debug.Warning(Debug.QuestNode_GenerateRuinsNode, "TestRun: tileId is 0! This might cause quest rejection.");
            }

            AbandonedBaseWorldObject worldObject = (AbandonedBaseWorldObject)WorldObjectMaker.MakeWorldObject(DefDatabase<WorldObjectDef>.GetNamed("AbandonedBase"));
            if (worldObject == null) {
                Debug.Error(Debug.QuestNode_GenerateRuinsNode, "TestRun: Failed to create world object!");
                return false;
            }
            worldObject.Tile = tileId;

            string storeAsKey = storeAs.GetValue(slate);
            slate.Set(storeAsKey, worldObject);
            Debug.Log(Debug.QuestNode_GenerateRuinsNode, "TestRun: Set slate[{0}], returning true", storeAsKey);

            return true;
		}

		protected override void RunInt() {
            Debug.Log(Debug.QuestNode_GenerateRuinsNode, "RunInt: Starting real run for RealRuins_AbandonedBase quest");
            try {
                Slate slate = QuestGen.slate;
                
                string storeAsKey = storeAs.GetValue(slate);
                Debug.Log(Debug.QuestNode_GenerateRuinsNode, "RunInt: storeAs key={0}", storeAsKey ?? "NULL");
                
                var obj = TryGenerateWorldObject(slate);
                
                if (obj != null) {
                    slate.Set(storeAsKey, obj);
                    var verifyValue = slate.Get<AbandonedBaseWorldObject>(storeAsKey);
                    Debug.Log(Debug.QuestNode_GenerateRuinsNode, "RunInt: Successfully set slate[{0}], verified={1}", 
                        storeAsKey, verifyValue != null ? "NOT NULL" : "NULL");
                } else {
                    Debug.Error(Debug.QuestNode_GenerateRuinsNode, "RunInt: TryGenerateWorldObject returned NULL!");
                }
            } catch (Exception ex) {
                Debug.Error(Debug.QuestNode_GenerateRuinsNode, "RunInt: Exception occurred: {0}", ex);
                throw;
            }
		}
	}
}

