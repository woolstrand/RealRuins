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

        private int ticksLeft = (int) Math.Abs(Rand.Gaussian(0, 100));

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