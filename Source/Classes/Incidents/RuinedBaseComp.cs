using RimWorld.Planet;
using Verse;
using System;

namespace RealRuins {
    public class RuinedBaseComp : WorldObjectComp {

        public string blueprintFileName;

        private bool active = false;
        public bool mapExitLocked = true;
        public float unlockTargetTime = -1; //when all raids are done, the component sets random timeout after which you can exit the map
        public int currentCapCost = -1;
        public float raidersActivity = -1; //battle points amount of starting raiders group
        private bool scavenged = false;
        private bool ShouldRemoveWorldObjectNow => scavenged && !base.ParentHasMap;
        


        public void StartScavenging(int initialCost) {
            currentCapCost = initialCost;
            raidersActivity = 0;
            active = true;
        }

        public override void PostExposeData() {
            base.PostExposeData();
            Scribe_Values.Look(ref blueprintFileName, "blueprintFileName", "");
            Scribe_Values.Look(ref currentCapCost, "currentCapCost", -1);
            Scribe_Values.Look(ref raidersActivity, "raidersActivity", -1);
            Scribe_Values.Look(ref mapExitLocked, "mapExitLocked", true);
        }

        public override void CompTick() {
            base.CompTick();
            if (ShouldRemoveWorldObjectNow) {
                Find.WorldObjects.Remove(parent);
            }

            if (!active) return;


            //base "do maradeur" act chance is once per 1.5 game days, but high activity can make it as often as once per game hour
            if (Rand.Chance(0.00001f + (float)raidersActivity / 50000000.0f) || raidersActivity * 10 > currentCapCost / 2) {
                int stolenAmount = (int)(Rand.Range(0.05f, 0.2f) * (raidersActivity / 10000.0f) * currentCapCost);
                currentCapCost -= Math.Max(stolenAmount, (int)(raidersActivity * 10));
                raidersActivity = raidersActivity / Rand.Range(2.0f, 5.0f);
            }

            raidersActivity += Rand.Range(-1.0f, 2.5f) * currentCapCost / 100000.0f;
            if (raidersActivity < 0) raidersActivity = 0;

            if ((currentCapCost < 20000 && Rand.Chance(0.00001f)) || currentCapCost <= 100) {
               scavenged = true; //scavenging is finished
            }

        }

        public override string CompInspectStringExtra() {
            if (!base.ParentHasMap && active) {
                string result = "";
                if (currentCapCost > 0) {
                    string wealthDesc = "";

                    int[] costThresholds = {0, 10000, 100000, 1000000, 10000000 };
                    if (currentCapCost > costThresholds[costThresholds.Length - 1]) wealthDesc = ("RealRuins.RuinsWealth." + (costThresholds.Length - 1)).Translate();
                    for (int i = 0; i < costThresholds.Length - 1; i++) {
                        if (currentCapCost > costThresholds[i] && currentCapCost <= costThresholds[i + 1]) {
                            wealthDesc = ("RealRuins.RuinsWealth." + i.ToString()).Translate();
                        }
                    }
                    

                    result += "RealRuins.RuinsWealth".Translate() + wealthDesc;
                    if (Prefs.DevMode) { result += " (" + currentCapCost + ")"; }
                }

                if (raidersActivity > 0) {
                    if (result.Length > 0) result += "\r\n";

                    string activityDesc = "";

                    int[] activityThresholds = { 0, 800, 3000, 6000, 12000 };
                    if (raidersActivity > activityThresholds[activityThresholds.Length - 1]) activityDesc = ("RealRuins.RuinsActivity." + (activityThresholds.Length - 1)).Translate();
                    for (int i = 0; i < activityThresholds.Length - 1; i++) {
                        if (raidersActivity > activityThresholds[i] && raidersActivity <= activityThresholds[i + 1]) {
                            activityDesc = ("RealRuins.RuinsActivity." + i).Translate();
                        }
                    }
                    result += "RealRuins.RuinsActivity".Translate() + activityDesc;
                    if (Prefs.DevMode) { result += " (" + raidersActivity + ")"; }
                }
                if (result.Length > 0) return result;
            }
            return null;
        }
    }
}