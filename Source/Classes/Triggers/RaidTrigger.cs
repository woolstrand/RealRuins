using Verse;
using RimWorld;
using System.Collections.Generic;
using System.Linq;

namespace RealRuins {
    public class RaidTrigger : Thing {

        public Faction faction;
        public float value;
        private bool triggered;
        private int ticksLeft;
        
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
                        triggered = true;
                        ticksLeft = Rand.Range(10, 800);
                        Debug.Message("Triggered raid at {0}, {1} of value {2} after {3} long ticks (approximately max speed seconds)", base.Position.x, base.Position.z, value, ticksLeft);
                    }
                } else {
//                    Debug.Message("tick rare: {0}", ticksLeft);
                    ticksLeft--;
                    if (ticksLeft < 0) {
                        IncidentDef incidentDef = IncidentDefOf.RaidEnemy;
                        IncidentParms parms = new IncidentParms {
                            faction = faction, points = value, target = Find.CurrentMap
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
        }
    }
}