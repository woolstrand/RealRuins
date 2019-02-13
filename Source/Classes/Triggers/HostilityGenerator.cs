using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using RimWorld;
using Verse;
using Verse.AI;

namespace RealRuins {
    class HostilityGenerator : Building, IAttackTarget, IAttackTargetSearcher {
        public Thing Thing => this;

        public LocalTargetInfo TargetCurrentlyAimingAt => null;

        private readonly Verb verbInt = new Verb_LaunchProjectile();
        public Verb CurrentEffectiveVerb => verbInt;

        public LocalTargetInfo LastAttackedTarget => null;

        public int LastAttackTargetTick => 0;

     

        public bool ThreatDisabled(IAttackTargetSearcher disabledFor) {
            return true;
        }

        public override void Tick() {

        }



        internal void SetFaction(Faction faction) {
            if (faction != null) {
                base.SetFaction(faction);
            }
        }
    }
}
