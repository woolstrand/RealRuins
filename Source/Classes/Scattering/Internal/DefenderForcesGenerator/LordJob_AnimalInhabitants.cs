using System.Linq;

using RimWorld;
using Verse;
using Verse.AI.Group;

namespace RealRuins {
    // Animal inhabitants of a ruin: they defend it for a few days, then become ordinary wild animals.
    // The defend duty lets them satisfy needs only near the defended point, so kept there for good they would starve.
    // Released animals have no faction, so they are no longer hostile (unless they go manhunter, like any wild animal).
    class LordJob_AnimalInhabitants : LordJob_DefendPoint {
        private int releaseTick = -1;

        public LordJob_AnimalInhabitants() {
        }

        public LordJob_AnimalInhabitants(IntVec3 point, int releaseTick) : base(point) {
            this.releaseTick = releaseTick;
        }

        public override void LordJobTick() {
            base.LordJobTick();
            if (releaseTick < 0 || Find.TickManager.TicksGame < releaseTick) return;
            //don't drop a fight halfway: wait until none of them has an enemy target
            if (lord.ownedPawns.Any(p => p.mindState?.enemyTarget != null)) return;

            Debug.Log(Debug.ForceGen, "Releasing {0} animal inhabitants into the wild", lord.ownedPawns.Count);
            //SetFaction removes the pawn from the lord, and the lord is destroyed once it has no pawns left
            foreach (Pawn pawn in lord.ownedPawns.ToList()) {
                if (pawn.Faction != null) pawn.SetFaction(null);
            }
        }

        public override void ExposeData() {
            base.ExposeData();
            Scribe_Values.Look(ref releaseTick, "releaseTick", -1);
        }
    }
}
