using Verse;
using RimWorld;
using System.Collections.Generic;
using System.Linq;

using Verse.AI;
using System;

namespace RealRuins {
    public class RaidTrigger : Thing, IAttackTarget {

        public Faction faction;
        public float value;
        private bool triggered;
        private int ticksLeft = 200; // shows how many ticks left before auto triggering (if trigger is not triggered yet) or before raid itself (if trigger was triggered by any means)
        private int referenceTimeoutAfterTriggered = 200;

        public Thing Thing => this;

        public LocalTargetInfo TargetCurrentlyAimingAt => null;

        public void SetTimeouts(int timeoutUntilAutoTrigger, int referenceTimeoutAfterTriggered = 200) {
            ticksLeft = timeoutUntilAutoTrigger;
            this.referenceTimeoutAfterTriggered = referenceTimeoutAfterTriggered;
        }

        public bool IsTriggered() {
            return triggered;
        }

        public int TicksLeft() {
            return ticksLeft;
        }
        
        public override void Tick() {
            if (Find.TickManager.TicksGame % 250 == 0)
            {
                TickRare();
            }
        }
        
        public override void TickRare()
        {
            if (Spawned) {
                ticksLeft--;
                if (!triggered) {
                    if (ticksLeft < 0) {
                        ticksLeft = (int)Math.Abs(Rand.Gaussian(0, referenceTimeoutAfterTriggered));
                        triggered = true;
                        Debug.Log("Battle", "Auto triggered raid at {0}, {1} of value {2} after {3} long ticks (approximately max speed seconds)", base.Position.x, base.Position.z, value, ticksLeft);
                    }

                    List<Thing> searchSet = PawnsFinder.AllMaps_FreeColonistsSpawned.ToList().ConvertAll(pawn => (Thing)pawn);

                    Thing thing = GenClosest.ClosestThing_Global(base.Position, searchSet, 10.0f);
                    if (thing != null) {
                        ticksLeft = (int)Math.Abs(Rand.Gaussian(0, 200));
                        triggered = true;
                        Debug.Log("Battle", "Triggered raid at {0}, {1} of value {2} after {3} long ticks (approximately max speed seconds)", base.Position.x, base.Position.z, value, ticksLeft);
                    }
                } else { 
                    if (ticksLeft < 0) {
                        IncidentDef incidentDef = IncidentDefOf.RaidEnemy;
                        IncidentParms parms = new IncidentParms {
                            faction = faction, points = value, target = base.Map
                        };
                        
                        incidentDef.Worker.TryExecute(parms);                        
                        
                        Destroy();
                    }
                }
            }
        }
 

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref faction, "faction", false);
            Scribe_Values.Look(ref value, "value", 0.0f, false);
            Scribe_Values.Look(ref triggered, "triggered", false);
            Scribe_Values.Look(ref ticksLeft, "ticksLeft", 0, false);
        }

        public bool ThreatDisabled(IAttackTargetSearcher disabledFor) {
            return true;
        }
    }
}