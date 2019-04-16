using RimWorld.Planet;
using Verse;
using System;
using System.Linq;
using System.Collections.Generic;

using RimWorld;

namespace RealRuins {

    enum RuinedBaseState {
        Inactive = 0,
        WaitingForArrival,
        FightingWaves,
        WaitingForEnemiesToBeDefeated,
        WaitingTimeoutAfterEnemiesDefeat,
        WaitingToBeInformed,
        InformedWaitingForLeaving
    }

    public class RuinedBaseComp : WorldObjectComp {

        public string blueprintFileName = "";

        private RuinedBaseState state = RuinedBaseState.Inactive;
        public float unlockTargetTime = -1; //when all raids are done, the component sets random timeout after which you can exit the map
        public int currentCapCost = -1;
        public float raidersActivity = -1; //battle points amount of starting raiders group
        private bool scavenged = false;
        private bool ShouldRemoveWorldObjectNow => scavenged && !base.ParentHasMap;

        public bool mapExitLocked {
            get {
                return state != RuinedBaseState.InformedWaitingForLeaving;
            }
        }

        private int unlockHysteresisTimeout = 1000; //there is a gap between firing the last raid trigger and hostile presence detection (i.e. while drop pods are opening). So we have to wait a bit after switching the component into the other mode.

        //list of all triggers on the map. don't know if it does really influence performance, but it definitely does not make it worse.
        List<RaidTrigger> triggersCache;

        public override void Initialize(WorldObjectCompProperties props) {
            base.Initialize(props);
        }

        private void BuildRaidTriggersCache() {
            if (ParentHasMap) {
                triggersCache = new List<RaidTrigger>();
                foreach (Thing t in (parent as MapParent).Map.spawnedThings) {
                    if (t is RaidTrigger) {
                        triggersCache.Add(t as RaidTrigger);
                    }
                }
            }
        }

        private void CheckTriggers() {
            if (triggersCache == null) {
                BuildRaidTriggersCache();
            }

            foreach (RaidTrigger raidTrigger in triggersCache) {
                if (raidTrigger.IsTriggered() == false || raidTrigger.TicksLeft() > 0) return;
            }

            //we got here only when all triggers have fired.
            if (state == RuinedBaseState.FightingWaves) {
                if (unlockHysteresisTimeout == 0) {
                    state = RuinedBaseState.WaitingForEnemiesToBeDefeated;
                    //Debug.Message("All triggers fired and expired, and we've wait safety margin => can wait before hostiled defeating and map unlocking");
                } else {
                    unlockHysteresisTimeout--;
                }
            }

        }

        public override void PostMapGenerate() {
            base.PostMapGenerate();
            if (state == RuinedBaseState.WaitingForArrival) {
                state = RuinedBaseState.FightingWaves;
                BuildRaidTriggersCache();
                //Debug.Message("Built cache, cache has size of {0}", triggersCache.Count);
            }
        }

        public void StartScavenging(int initialCost) {
            currentCapCost = initialCost;
            raidersActivity = 0;
            state = RuinedBaseState.WaitingForArrival;
        }

        public override void PostExposeData() {
            
            base.PostExposeData();
            Scribe_Values.Look(ref blueprintFileName, "blueprintFileName", "");
            Scribe_Values.Look(ref currentCapCost, "currentCapCost", -1);
            Scribe_Values.Look(ref raidersActivity, "raidersActivity", -1);
            Scribe_Values.Look(ref unlockTargetTime, "unlockTargetTime", 0);
            Scribe_Values.Look(ref state, "state", RuinedBaseState.Inactive);
            
        }

        public override void CompTick() {
            base.CompTick();
            if (ShouldRemoveWorldObjectNow) {
                Find.WorldObjects.Remove(parent);
            }

            if (state == RuinedBaseState.Inactive) return;


            if (!ParentHasMap) {
                //base "do maradeur" act chance is once per 1.5 game days, but high activity can make it as often as once per game hour
                if (Rand.Chance(0.00001f + (float)raidersActivity / 50000000.0f) || raidersActivity * 10 > currentCapCost / 2) {
                    int stolenAmount = (int)(Rand.Range(0.05f, 0.2f) * (raidersActivity / 10000.0f) * currentCapCost);
                    currentCapCost -= Math.Max(stolenAmount, (int)(raidersActivity * 10));
                    raidersActivity = raidersActivity / Rand.Range(2.0f, 5.0f);
                }

                raidersActivity += Rand.Range(-.2f, 0.5f) * currentCapCost / 100000.0f;
                if (raidersActivity < 0) raidersActivity = 0;

                if ((currentCapCost < 20000 && Rand.Chance(0.00001f)) || currentCapCost <= 100) {
                    scavenged = true; //scavenging is finished
                }
            }

            if (!RealRuins_ModSettings.allowInstantCaravanReform && ParentHasMap) {
                if (state == RuinedBaseState.FightingWaves) {
                    CheckTriggers();
                } else if (state == RuinedBaseState.WaitingForEnemiesToBeDefeated) {
                    //AnyHostileActiveThreatToPlayer is a proxy call to AnyHostileActiveThreatTo, but it is postfixed with additional check by this mod.
                    //Here I want to do original check, without my postfix, so I call directly the checking method. So-so solution, but cant think of anything better AND worthwhile
                    if (!GenHostility.AnyHostileActiveThreatTo((parent as MapParent).Map, Faction.OfPlayer)) {
                        unlockTargetTime = Find.TickManager.TicksGame + Rand.Range(30000, 30000 + currentCapCost);
                        state = RuinedBaseState.WaitingTimeoutAfterEnemiesDefeat;
                        //Debug.Message("no more hostiles. time: {0}, unlocktime: {1}", Find.TickManager.TicksGame, unlockTargetTime);
                    }
                } else if (state == RuinedBaseState.WaitingTimeoutAfterEnemiesDefeat) {
                    if (Find.TickManager.TicksGame > unlockTargetTime) {
                        state = RuinedBaseState.WaitingToBeInformed;
                        string text = "RealRuins.NoMoreEnemies".Translate();
                        Messages.Message(text, parent, MessageTypeDefOf.PositiveEvent);
                        state = RuinedBaseState.InformedWaitingForLeaving;
                    }
                }
            }

        }

        public override string CompInspectStringExtra() {
            if (!ParentHasMap && state == RuinedBaseState.WaitingForArrival) {
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