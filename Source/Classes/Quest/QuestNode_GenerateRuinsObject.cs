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
            var tileId = tile.GetValue(slate);

            AbandonedBaseWorldObject site = (AbandonedBaseWorldObject)WorldObjectMaker.MakeWorldObject(DefDatabase<WorldObjectDef>.GetNamed("AbandonedBase"));
            site.Tile = tileId;
            site.SetFaction(null);
            Find.WorldObjects.Add(site);

            RuinedBaseComp comp = site.GetComponent<RuinedBaseComp>();
            if (comp == null) {
                Debug.Warning("Component is null");
            } else {
                Debug.Warning("Starting scavenging...");
                var cost = (int)Math.Min(cachedCost, RealRuins_ModSettings.ruinsCostCap);
                comp.blueprintFileName = filename;
                comp.StartScavenging(cost); //passing initial cost to calculate coefficients without need to load bp one more time
            }

            return site;
        }

        protected override bool TestRunInt(Slate slate) {
            var obj = TryGenerateWorldObject(slate);
            if (obj == null) {
                return false;
            } else { 
                return true;
            }
		}

		protected override void RunInt() {
			Slate slate = QuestGen.slate;
			var obj = TryGenerateWorldObject(slate);
            slate.Set(storeAs.GetValue(slate), obj);
		}
	}
}

