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
        private int ticksLeft;

        public Thing Thing => this;

        public LocalTargetInfo TargetCurrentlyAimingAt => null;

        public bool IsTriggered() {
            return triggered;
        }
        
        public override void Tick() {
            if (Find.TickManager.TicksGame % 250 == 0)
            {
                TickRare();
            }
        }
        
        public override void TickRare()
        {
            if (base.Spawned) {
                if (!triggered) {
                    List<Thing> searchSet = PawnsFinder.AllMaps_FreeColonistsSpawned.ToList().ConvertAll(pawn => (Thing)pawn);

                    Thing thing = GenClosest.ClosestThing_Global(base.Position, searchSet, 10.0f);
                    if (thing != null) {
                        RuinedBaseComp parentComponent = base.Map.Parent.GetComponent<RuinedBaseComp>();

                        ticksLeft = (int)Math.Abs(Rand.Gaussian(0, 200));
                        
                        triggered = true;
                        Debug.Message("Triggered raid at {0}, {1} of value {2} after {3} long ticks (approximately max speed seconds)", base.Position.x, base.Position.z, value, ticksLeft);
                    }
                } else { 
                    ticksLeft--;
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