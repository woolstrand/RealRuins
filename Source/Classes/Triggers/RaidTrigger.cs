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
        
        protected override void Tick() {
            if (Find.TickManager.TicksGame % 250 == 0)
            {
                TickRare();
            }
        }
        
        public override void TickRare()
        {
            if (Spawned) {
                ticksLeft--;
                if (ticksLeft < 0) {
                    Debug.Log(Debug.Generic, "Raid trigger fired, faction: {0}", faction);
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