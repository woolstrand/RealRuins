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
        public SlateRef<int> tile; // tile where object to be placed
		public SlateRef<string> storeAs;

		private AbandonedBaseWorldObject TryGenerateWorldObject(Slate slate) {
            var filename = blueprintFilename.GetValue(slate);
            var cachedCost = blueprintCachedCost.GetValue(slate);
            var cachedCost2 = slate.Get<int>("blueprintCachedCost");
            var tileId = tile.GetValue(slate);

            AbandonedBaseWorldObject worldObject = (AbandonedBaseWorldObject)WorldObjectMaker.MakeWorldObject(DefDatabase<WorldObjectDef>.GetNamed("AbandonedBase"));
            worldObject.Tile = tileId;
            worldObject.SetFaction(null);

            RuinedBaseComp comp = worldObject.GetComponent<RuinedBaseComp>();
            if (comp == null) {
                Debug.Warning("Component is null");
            } else {
                comp.blueprintFileName = filename;
                // Here we have to determine starting value. Ruins value will decrease over time (lore: scavenged by other factions)
                // However, we do not want it to be initially larger than total wealth cap.

                float costCap = RealRuins_ModSettings.ruinsCostCap;
                float startingCap = Math.Min(costCap, Math.Max(cachedCost, cachedCost2));
                if (startingCap > int.MaxValue) {
                    startingCap = int.MaxValue - 1; //not sure why StartScavenging takes int as input, but don't want to change it now.
                }

                Debug.Log(Debug.Event, "Initial cost set to {0} (blueprint cost {1} OR {3}, settings cap {2}", startingCap, cachedCost, costCap, cachedCost2);
                comp.StartScavenging((int)startingCap); //passing initial cost to calculate coefficients without need to load bp one more time
            }

            return worldObject;
        }

        protected override bool TestRunInt(Slate slate) {
            Debug.Log("QuestNode_GenerateRuinsNode", "TestRun Launched");

            var tileId = tile.GetValue(slate);

            AbandonedBaseWorldObject worldObject = (AbandonedBaseWorldObject)WorldObjectMaker.MakeWorldObject(DefDatabase<WorldObjectDef>.GetNamed("AbandonedBase"));
            worldObject.Tile = tileId;

            slate.Set(storeAs.GetValue(slate), worldObject);

            return true;
		}

		protected override void RunInt() {
			Slate slate = QuestGen.slate;
            Debug.Log("QuestNode_GenerateRuinsNode", "Real run launched");
            var obj = TryGenerateWorldObject(slate);
            slate.Set(storeAs.GetValue(slate), obj);
		}
	}
}

