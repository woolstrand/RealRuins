using Verse;
using RimWorld;
using System.Collections.Generic;
using System.Linq;

using Verse.AI;
using System;

namespace RealRuins {
    // Previously the idea behind the trigger was to have it as a map object which could be triggered manually (by stepping on it)
    // or automatically after a timeout. It was done to spawn forces more quickly if the user actively explores the map. It turned out
    // to be useless and always worked in auto mode.
    public class RaidTrigger : Thing, IAttackTarget {

        public Faction faction;
        public float value;

        public float TargetPriorityFactor {
            get {
                return 0.0f;
            }
        }

        public Thing Thing => this;

        public LocalTargetInfo TargetCurrentlyAimingAt => null;

        // Rare ticks (250 ticks) until the raid. Triggers on a ruins site are rescheduled from the site's discovery (see Reschedule).
        private int ticksLeft = ArrivalRareTicks(0);

        // Troops of the neighbours set off when the ruins are discovered and reach them over ArrivalWindowDays: few at first,
        // most around the first day, fewer and fewer after (Beta(2,3) over the window, mode at 1 day, mean 1.2 days).
        // Raids are scheduled when the player enters, from what is left of that curve: no raid sooner than MinRareTicks after
        // entering, and the time that already passed counts only up to MaxElapsedDays, so a late arrival still faces
        // the tail of the arrivals (about 0.4-1.5 days after entering) instead of nothing.
        public const int MinRareTicks = 100; // 25000 ticks, 0.42 days
        private const float ArrivalWindowDays = 3f;
        private const float MaxElapsedDays = 1.5f;

        public static int ArrivalRareTicks(int ticksSinceDiscovery) {
            float rareTicksPerDay = GenDate.TicksPerDay / (float)GenTicks.TickRareInterval;
            float window = ArrivalWindowDays * rareTicksPerDay;
            float elapsed = Math.Min(Math.Max(ticksSinceDiscovery, 0) / (float)GenTicks.TickRareInterval, MaxElapsedDays * rareTicksPerDay);
            float earliest = elapsed + MinRareTicks;
            for (int i = 0; i < 1000; i++) {
                float arrival = window * BetaTwoThree();
                if (arrival >= earliest) return (int)(arrival - elapsed);
            }
            return (int)Rand.Range(MinRareTicks, window - elapsed);
        }

        // Beta(2,3): the second smallest of four uniform values
        private static float BetaTwoThree() {
            float[] values = { Rand.Value, Rand.Value, Rand.Value, Rand.Value };
            Array.Sort(values);
            return values[1];
        }

        public void Reschedule(int ticksSinceDiscovery) {
            ticksLeft = ArrivalRareTicks(ticksSinceDiscovery);
            int now = Find.TickManager.TicksGame;
            Debug.Log(Debug.Generic, "Raid trigger {0} rescheduled {1} ticks after discovery: {2} rare ticks left, expected to fire at about tick {3}",
                thingIDNumber, ticksSinceDiscovery, ticksLeft, now + (ticksLeft + 1) * GenTicks.TickRareInterval);
        }

        public int TicksLeft() {
            return ticksLeft;
        }
        
        // The def uses tickerType Rare, so the engine calls TickRare once per 250 ticks and ticksLeft counts rare ticks.
        public override void SpawnSetup(Map map, bool respawningAfterLoad) {
            base.SpawnSetup(map, respawningAfterLoad);
            int now = Find.TickManager.TicksGame;
            Debug.Log(Debug.Generic, "Raid trigger {0} spawned at tick {1} (after load: {2}), {3} rare ticks left, expected to fire at about tick {4}",
                thingIDNumber, now, respawningAfterLoad, ticksLeft, now + (ticksLeft + 1) * GenTicks.TickRareInterval);
        }

        public override void TickRare()
        {
            if (Spawned) {
                ticksLeft--;
                if (ticksLeft < 0) {
                    Debug.Log(Debug.Generic, "Raid trigger {0} fired at tick {1}, faction: {2}", thingIDNumber, Find.TickManager.TicksGame, faction);
                    IncidentDef incidentDef = IncidentDefOf.RaidEnemy;
                    IncidentParms parms = new IncidentParms {
                        faction = faction,
                        points = value,
                        target = base.Map
                    };

                    incidentDef.Worker.TryExecute(parms);

                    Destroy();
                }
            }
        }


        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref faction, "faction", false);
            Scribe_Values.Look(ref value, "value", 0.0f, false);
            Scribe_Values.Look(ref ticksLeft, "ticksLeft", 0, false);
        }

        public bool ThreatDisabled(IAttackTargetSearcher disabledFor) {
            return true;
        }
    }
}