using RimWorld.Planet;
using Verse;
using System;
using System.Linq;
using System.Collections.Generic;

using RimWorld;
using Verse.Noise;

namespace RealRuins {

    enum RuinedBaseState {
        Inactive = 0,
        WaitingForArrival,
        FightingWaves,
        WaitingForEnemiesToBeDefeated,
        WaitingTimeoutAfterEnemiesDefeat,
        WaitingToBeInformed,
        InformedWaitingForLeaving,
        ScavengedCompletely
    }

    public class RuinedBaseComp : WorldObjectComp {

        public string blueprintFileName = ""; //only the name is saved, the blueprint is loaded again on map generation (see RuinsScatterer.Scatter)

        private RuinedBaseState state = RuinedBaseState.Inactive;
        public float unlockTargetTime = -1; //when all raids are done, the component sets random timeout after which you can exit the map
        public int currentCapCost = -1;
        public float raidersActivity = -1; //battle points amount of starting raiders group
        // Map resource wealth at arrival, floored at 150k: the reference for the unlock timer.
        // currentCapCost is left as the off-map scavenging simulation made it (blueprint cost units).
        private float referenceWealth = -1;
        // Saves made before referenceWealth existed overwrote currentCapCost with this value on arrival.
        private float ReferenceWealth => referenceWealth > 0 ? referenceWealth : currentCapCost;
        private bool ShouldRemoveWorldObjectNow => state == RuinedBaseState.ScavengedCompletely && !base.ParentHasMap;
        public bool isActive => state != RuinedBaseState.Inactive;
        // Signal tags for quest completion. Can be null/empty in these cases:
        // 1. World object created via IncidentWorker_RuinsFound (not quest-based) - starts as null
        // 2. After save/load if signals weren't saved - becomes empty string "" due to Scribe_Values.Look default
        // 3. If QuestGenUtility.HardcodedSignalWithQuestID returns null/empty (unlikely)
        // Empty/null signals can match Lords with null/empty inSignalLeave tags, causing unintended "are leaving" messages
        public string expireSignal = null;
        public string successSignal = null;
        // Continuous (V2) raids: the next one may start at this tick. -1: as soon as the map is below the hostile cap.
        private int nextSmallRaidTick = -1;
        // Gap between continuous raids: 7200 +- 1800 ticks, i.e. about 95% of them 60-180 s apart at 1x speed (60 ticks/s), never under 30 s.
        private const float SmallRaidIntervalMean = 7200f;
        private const float SmallRaidIntervalWidth = 1800f;
        private const int SmallRaidIntervalMin = 1800;

        // When the ruins were discovered (StartScavenging): raid triggers are scheduled from it on arrival. -1: unknown (older saves).
        private int discoveryTick = -1;
        // Continuous (V2) raids scale with the share of the map's items still there: resource wealth now / at arrival.
        // Updated after each raid; 1.0 = nothing taken, 0.0 = everything gone.
        private float initialItemWealth = -1;
        private float itemsLeft = 0.0f;
        private const float SmallRaidMinPoints = 50f;
        private const float SmallRaidMaxPoints = 400f;

        // True while raids may still come. In manual reform mode this blocks reforming (see FormCaravanComp_CanFormOrReform_Patch),
        // so the only way out is the map edge.
        public bool mapExitLocked {
            get {
                return state == RuinedBaseState.FightingWaves
                    || state == RuinedBaseState.WaitingForEnemiesToBeDefeated
                    || state == RuinedBaseState.WaitingTimeoutAfterEnemiesDefeat
                    || state == RuinedBaseState.WaitingToBeInformed;
            }
        }

        //there is a gap between firing the last raid trigger and hostile presence detection (i.e. while drop pods are opening). So we have to wait a bit after switching the component into the other mode.
        private const int UnlockHysteresisTicks = 1000;
        private int allTriggersFiredTick = -1;

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
                if (raidTrigger.TicksLeft() > 0) return;
            }

            //we got here only when all triggers have fired.
            if (state == RuinedBaseState.FightingWaves) {
                if (allTriggersFiredTick < 0) {
                    allTriggersFiredTick = Find.TickManager.TicksGame;
                } else if (Find.TickManager.TicksGame - allTriggersFiredTick >= UnlockHysteresisTicks) {
                    state = RuinedBaseState.WaitingForEnemiesToBeDefeated;
                    //Debug.Message("All triggers fired and expired, and we've wait safety margin => can wait before hostiled defeating and map unlocking");
                }
            }

        }

        public override void PostMapGenerate() {
            base.PostMapGenerate();
            if (state == RuinedBaseState.WaitingForArrival) {
                state = RuinedBaseState.FightingWaves;
                BuildRaidTriggersCache();
                int sinceDiscovery = discoveryTick >= 0 ? Find.TickManager.TicksGame - discoveryTick : 0;
                foreach (RaidTrigger trigger in triggersCache) {
                    trigger.Reschedule(sinceDiscovery);
                }
            }

            var map = (parent as MapParent)?.Map;
            var currentCost = GetTotalMapWealth(map);
            // For smaller bases we want to limit upcoming forces, so let's assume that a reference wealth is 150k, and neighbours
            // are not really interested in smaller wealths that much.
            referenceWealth = Math.Max(currentCost, 150000);
            initialItemWealth = currentCost;
            itemsLeft = 1.0f;
            Debug.Log(Debug.Generic, "Generated map. Cap cost: {0}, actual cost: {1}, reference wealth for forces: {2}.", currentCapCost, currentCost, referenceWealth);
        }

        // Dev Explore / Reset (DevSiteTools, via AbandonedBaseWorldObject): the scavenging state before the visit
        private class DevState {
            public RuinedBaseState state;
            public int currentCapCost;
            public float raidersActivity;
        }

        public object DevSaveState() {
            return new DevState { state = state, currentCapCost = currentCapCost, raidersActivity = raidersActivity };
        }

        public void DevRestoreState(object saved) {
            if (saved is DevState devState) {
                state = devState.state;
                currentCapCost = devState.currentCapCost;
                raidersActivity = devState.raidersActivity;
            } else if (state != RuinedBaseState.Inactive && state != RuinedBaseState.ScavengedCompletely) {
                state = RuinedBaseState.WaitingForArrival;
            }
            //what the visit set up, PostMapGenerate sets it again on the next one
            unlockTargetTime = -1;
            allTriggersFiredTick = -1;
            triggersCache = null;
            referenceWealth = -1;
            initialItemWealth = -1;
            itemsLeft = 0;
            nextSmallRaidTick = -1;
        }

        public void StartScavenging(int initialCost) {
            currentCapCost = initialCost;
            raidersActivity = 0;
            discoveryTick = Find.TickManager.TicksGame;
            state = RuinedBaseState.WaitingForArrival;
        }

        public override void PostExposeData() {
            base.PostExposeData();
            Scribe_Values.Look(ref blueprintFileName, "blueprintFileName", "");
            Scribe_Values.Look(ref currentCapCost, "currentCapCost", -1);
            Scribe_Values.Look(ref raidersActivity, "raidersActivity", -1);
            Scribe_Values.Look(ref referenceWealth, "referenceWealth", -1);
            Scribe_Values.Look(ref unlockTargetTime, "unlockTargetTime", 0);
            Scribe_Values.Look(ref state, "state", RuinedBaseState.Inactive);
            Scribe_Values.Look(ref expireSignal, "expireSignal", "");
            Scribe_Values.Look(ref successSignal, "successSignal", "");
            Scribe_Values.Look(ref allTriggersFiredTick, "allTriggersFiredTick", -1);
            Scribe_Values.Look(ref discoveryTick, "discoveryTick", -1);
            Scribe_Values.Look(ref initialItemWealth, "initialItemWealth", -1);
            Scribe_Values.Look(ref itemsLeft, "itemsLeft", 0.0f);
            Scribe_Values.Look(ref nextSmallRaidTick, "nextSmallRaidTick", -1);
        }

        public override void CompTick() {
            base.CompTick();
            if (ShouldRemoveWorldObjectNow) {
                var signalTag = expireSignal;
                // Only send signal if it's not null or empty to avoid triggering unintended Lord jobs
                // Signals are global and can match Lords from other quests/raids, causing "are leaving" messages
                if (!signalTag.NullOrEmpty()) {
                    Debug.Log("Quest", "Sending expiration signal: {0}", signalTag);
                    Find.SignalManager.SendSignal(new Signal(signalTag));
                }
                Find.WorldObjects.Remove(parent);
            }

            if (state == RuinedBaseState.Inactive) return;

            if (!ParentHasMap) {
                //base "do maradeur" act chance is once per 1.5 game days, but high activity can make it as often as once per game hour
                if (Rand.Chance(0.00001f + (float)raidersActivity / 50000000.0f) || raidersActivity * 4 > currentCapCost / 2) {
                    int stolenAmount = (int)(Rand.Range(0.05f, 0.2f) * (raidersActivity / 12000.0f) * currentCapCost);
                    currentCapCost -= Math.Max(stolenAmount, (int)(raidersActivity * 4));
                    raidersActivity = raidersActivity / Rand.Range(2.0f, 5.0f);
                }

                raidersActivity += Rand.Range(-.1f, 0.25f) * currentCapCost / 100000.0f;
                if (raidersActivity < 0) raidersActivity = 0;

                if ((currentCapCost < 20000 && Rand.Chance(0.00001f)) || currentCapCost <= 100) {
                    state = RuinedBaseState.ScavengedCompletely;
                }
            }

            if (Find.TickManager.TicksGame % 60 == 12) {
                DoRareTask();
            }
        }

        // Support constnt density of forces on the map
        private void DoRareTask() {
            if (state == RuinedBaseState.FightingWaves) {
                if (RealRuins_ModSettings.useRuinsForcesGenerationV2) {
                    var map = (parent as MapParent)?.Map;
                    if (initialItemWealth < 0) {
                        //saves made before initialItemWealth existed: count from the items on the map now
                        initialItemWealth = GetTotalMapWealth(map);
                        itemsLeft = 1.0f;
                    }
                    int hostileCount = map.mapPawns.AllPawnsSpawned.Count(p => p.HostileTo(Faction.OfPlayer));
                    if (hostileCount < 15 * itemsLeft * RealRuins_ModSettings.forceMultiplier && map.mapPawns.AllPawnsSpawned.Count < 90) {
                        int now = Find.TickManager.TicksGame;
                        if (now >= nextSmallRaidTick) {
                            // a nuisance for unarmed haulers and animals, not a threat to armed pawns
                            float points = Rand.Range(SmallRaidMinPoints, SmallRaidMaxPoints) * itemsLeft * RealRuins_ModSettings.forceMultiplier;
                            Debug.Log(Debug.Generic, "Triggering small raid for {0} points", points);
                            TriggerRaid(points);

                            itemsLeft = initialItemWealth > 0 ? Math.Min(1.0f, GetTotalMapWealth(map) / initialItemWealth) : 0;
                            nextSmallRaidTick = now + Math.Max(SmallRaidIntervalMin, (int)Rand.Gaussian(SmallRaidIntervalMean, SmallRaidIntervalWidth));
                            Debug.Log(Debug.Generic, "Items left: {0}, next small raid not before tick {1}", itemsLeft, nextSmallRaidTick);
                        }
                    }
                }
            }

            //The waves lifecycle is the same for every caravan reform mode. Only manual mode uses it to lock the exit (see mapExitLocked).
            if (ParentHasMap) {
                if (state == RuinedBaseState.FightingWaves) {
                    CheckTriggers();
                } else if (state == RuinedBaseState.WaitingForEnemiesToBeDefeated) {
                    if (!GenHostility.AnyHostileActiveThreatTo((parent as MapParent).Map, Faction.OfPlayer)) {
                        unlockTargetTime = Find.TickManager.TicksGame + Rand.Range(45000, 60000 + (int)ReferenceWealth);
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
                    result += "RealRuins.RuinsWealth".Translate() + WealthUtility.WealthDescription(currentCapCost);
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

        // Triggers small raid to keep constant number of defenders
        private void TriggerRaid(float points) {
            var map = (parent as MapParent)?.Map;
            if (map == null) return;

            // Get the raid incident def
            IncidentDef raidDef = IncidentDefOf.RaidEnemy;

            IncidentParms parms = StorytellerUtility.DefaultParmsNow(raidDef.category, map);
            parms.forced = true;
            parms.target = map;
            parms.points = points;
            parms.faction = FactionSelector.GetRandomFaction();
            if (parms.faction == null) return;
            // below the faction's cheapest group the raid generates nobody
            float minPoints = parms.faction.def.MinPointsToGeneratePawnGroup(PawnGroupKindDefOf.Combat);
            if (minPoints < SmallRaidMaxPoints) parms.points = Math.Max(points, minPoints);
            parms.raidStrategy = RaidStrategyDefOf.ImmediateAttack;
            parms.canSteal = true;
            parms.canTimeoutOrFlee = true;
            parms.sendLetter = false;

            // Fire the incident
            raidDef.Worker.TryExecute(parms);
        }

        private float GetTotalMapWealth(Map map) {
            return WealthUtility.MapWealth(map, t => t.def.CountAsResource);
        }
    }
}