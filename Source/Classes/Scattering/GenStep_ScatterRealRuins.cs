using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;

using System.Reflection;

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

        private float multiplier = 1.0f;

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



        public override void Generate(Map map, GenStepParams parms) {
            Debug.Message("Overridden generate");
            if (allowInWaterBiome || !map.TileInfo.WaterCovered) {

                /*if (RealRuins.detectedConfigurableMaps) {
                    Type t = Type.GetType("ConfigurableMaps.Settings.ThingsSettings,ConfigurableMaps");
                    FieldInfo fi = t.GetField("ruinsLevel");
                    object ruinsLevelObject = fi.GetValue(null);
                    float ruinsLevel = float.Parse(ruinsLevelObject.ToString());
                    Debug.Message("Original ruins level: {0}", ruinsLevel);

                    if (ruinsLevel > 8.99) {
                        ruinsLevel = 9.0f * Rand.Value;
                    } else {
                        ruinsLevel /= 1.5f; //on some reason Configurable Maps tells that vanilla multiplier is not x1.0, but x1.5 to x2.0. why?
                        if (ruinsLevel > 2.0) {
                            ruinsLevel *= 2;//adding a bit of non-linearity lol
                        }
                    }

                    multiplier = ruinsLevel;
                }*/

                float multiplier = RealRuins_ModSettings.defaultScatterOptions.densityMultiplier;

                FloatRange per10k = new FloatRange(countPer10kCellsRange.min * multiplier, countPer10kCellsRange.max * multiplier);
                int num = CountFromPer10kCells(per10k.RandomInRange, map, -1);

                Debug.Message("Spawning {0} ruin chunks", num);

                for (int i = 0; i < num; i++) {
                    if (!TryFindScatterCell(map, out IntVec3 result)) {
                        return;
                    }
                    ScatterAt(result, map, 1);
                    usedSpots.Add(result);
                }
                usedSpots.Clear();
            }
        }


        protected override void ScatterAt(IntVec3 loc, Map map, int count = 1) {
            float scavengersActivity = Rand.Value * 0.5f + 0.5f; //later will be based on other settlements proximity
            float ruinsAge = Rand.Range(1, 25);
            float deteriorationDegree = Rand.Value;
            int referenceRadius = Rand.Range(4 + (int)(multiplier / 3), 12 + (int)multiplier);

            new RuinsScatterer().ScatterRuinsAt(loc, map, RealRuins_ModSettings.defaultScatterOptions);
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
/*            float scavengersActivity = 0.5f + Rand.Value; //later will be based on other settlements proximity
            float ruinsAge = Rand.Range(1, 25);
            float deteriorationDegree = Rand.Value;
            int referenceRadius = Rand.Range(15, 35);
            new RuinsScatterer().ScatterRuinsAt(loc, map, referenceRadius, Rand.Range(0, 3), deteriorationDegree, scavengersActivity, ruinsAge);*/
        }

        protected override bool CanScatterAt(IntVec3 loc, Map map) {
            return true;
        }
    }
}
