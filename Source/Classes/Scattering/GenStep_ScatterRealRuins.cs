using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;

using RimWorld;
using Verse;

namespace RealRuins
{

    class GenStep_ScatterRealRuins : GenStep_Scatterer {
        public override int SeedPart {
            get {
                return 74293945;
            }
        }

        private void CalculateProximity() {
            /*
            int maxDist = MaxDist;
            List<Settlement> settlements = Find.WorldObjects.Settlements;
            for (int i = 0; i < settlements.Count; i++) {
                Settlement settlement = settlements[i];
                if (settlement.Faction != null && settlement.Faction != Faction.OfPlayer && (!ignorePermanentlyHostile || !settlement.Faction.def.permanentEnemy) && (!ignoreIfAlreadyMinGoodwill || settlement.Faction.PlayerGoodwill != -100)) {
                    int num = Find.WorldGrid.TraversalDistanceBetween(tile, settlement.Tile, false, maxDist);
                    if (num != 2147483647) {
                        int num2 = Mathf.RoundToInt(DiplomacyTuning.Goodwill_PerQuadrumFromSettlementProximity.Evaluate((float)num));
                        if (num2 != 0) {
                            outOffsets.Add(new Pair<Settlement, int>(settlement, num2));
                        }
                    }
                }
            }
        */
        }


        protected override void ScatterAt(IntVec3 loc, Map map, int count = 1) {
            float scavengersActivity = Rand.Value * 0.5f + 0.5f; //later will be based on other settlements proximity
            float ruinsAge = Rand.Range(1, 25);
            float deteriorationDegree = Rand.Value;
            int referenceRadius = Rand.Range(4, 12);
            new RuinsScatterer().ScatterRuinsAt(loc, map, referenceRadius, Rand.Range(0, 3), deteriorationDegree, scavengersActivity, ruinsAge);
        }

        protected override bool CanScatterAt(IntVec3 loc, Map map) {
            return true;
        }
    }

    class GenStep_ScatterLargeRealRuins : GenStep_Scatterer {
        public override int SeedPart {
            get {
                return 74293946;
            }
        }


        protected override void ScatterAt(IntVec3 loc, Map map, int count = 1) {
            float scavengersActivity = 0.5f + Rand.Value; //later will be based on other settlements proximity
            float ruinsAge = Rand.Range(1, 25);
            float deteriorationDegree = Rand.Value;
            int referenceRadius = Rand.Range(15, 35);
            new RuinsScatterer().ScatterRuinsAt(loc, map, referenceRadius, Rand.Range(0, 3), deteriorationDegree, scavengersActivity, ruinsAge);
        }

        protected override bool CanScatterAt(IntVec3 loc, Map map) {
            return true;
        }
    }
}
