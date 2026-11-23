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

        // Rare ticks (250 ticks) until the raid. Triggers on a ruins site get a delay that depends on how long the site
        // waited before the player came (see Reschedule).
        private int ticksLeft = ArrivalRareTicks(0);

        // The neighbours' troops set off when the ruins are discovered: few at first, then more and more, then fewer and fewer.
        // The time the site waited before the player came only picks the part of that bell the player still meets: none waited -
        // the whole bell, ~3 days - its steep right side, ~6 days - the gentle far slope. That part is spread over the time after
        // entering, MinRareTicks..MaxRareTicks. The two clocks are not aligned: the site loses value slowly while it waits,
        // the raids are dense once the player is there. The bell is a Cauchy one (peak BellPeak, half width BellWidth, in days of
        // waiting / WaitDaysPerUnit): its far slope flattens out, so long waits give an almost even spread.
        public const int MinRareTicks = 100; // 0.42 days
        public const int MaxRareTicks = 672; // 2.8 days
        private const float WaitDaysPerUnit = 2.5f;
        private const float BellPeak = 1.0f;
        private const float BellWidth = 0.4f;
        private const float BellWindow = 2.4f; // the part of the bell spread over Min..MaxRareTicks
        private const float MaxBellOffset = 4f; // 10 days of waiting

        public static int ArrivalRareTicks(int ticksWaited) {
            float offset = Math.Min(Math.Max(ticksWaited, 0) / (float)GenDate.TicksPerDay / WaitDaysPerUnit, MaxBellOffset);
            double low = Math.Atan((offset - BellPeak) / BellWidth);
            double high = Math.Atan((offset + BellWindow - BellPeak) / BellWidth);
            double x = BellPeak + BellWidth * Math.Tan(low + Rand.Value * (high - low));
            return MinRareTicks + (int)((x - offset) / BellWindow * (MaxRareTicks - MinRareTicks));
        }

        public void Reschedule(int ticksWaited) {
            ticksLeft = ArrivalRareTicks(ticksWaited);
            int now = Find.TickManager.TicksGame;
            Debug.Log(Debug.Generic, "Raid trigger {0} rescheduled for a site that waited {1} ticks: {2} rare ticks left, expected to fire at about tick {3}",
                thingIDNumber, ticksWaited, ticksLeft, now + (ticksLeft + 1) * GenTicks.TickRareInterval);
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