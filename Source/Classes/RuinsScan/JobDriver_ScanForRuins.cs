using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace RealRuins {
    // Scanning has no progress: every tick of work is an independent roll (mean time between discoveries from RuinsScanDef),
    // so interrupted work loses nothing and nobody, the player included, knows when the signal will come.
    public class JobDriver_ScanForRuins : JobDriver {
        private const int JobEndInterval = 4000;

        private CompRuinsScanner Scanner => TargetThingA.TryGetComp<CompRuinsScanner>();

        public override bool TryMakePreToilReservations(bool errorOnFailed) {
            if (!pawn.Reserve(TargetThingA, job, 1, -1, null, errorOnFailed)) {
                return false;
            }
            if (TargetThingA.def.hasInteractionCell) {
                return pawn.ReserveSittableOrSpot(TargetThingA.InteractionCell, job, errorOnFailed);
            }
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils() {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOnBurningImmobile(TargetIndex.A);
            this.FailOn(() => Scanner == null || !Scanner.CanScanNow.Accepted);
            this.FailOn(() => !job.playerForced && !Scanner.scanningEnabled);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell);

            RuinsScanDef settings = Scanner.ScanDef;
            Toil scan = ToilMaker.MakeToil("ScanForRuins");
            scan.tickIntervalAction = delegate (int delta) {
                Pawn actor = scan.actor;
                actor.skills.Learn(SkillDefOf.Intellectual, settings.xpPerTick * delta);
                actor.GainComfortFromCellIfPossible(delta, chairsOnly: true);

                if (RuinsScanTracker.debugForceDiscovery || Rand.MTBEventOccurs(settings.MtbDaysFor(actor), GenDate.TicksPerDay, delta)) {
                    RuinsScanTracker.debugForceDiscovery = false;
                    RuinsScanTracker.Instance.SignalFound(actor, TargetThingA);
                    EndJobWith(JobCondition.Succeeded);
                }
            };
            scan.FailOnCannotTouch(TargetIndex.A, PathEndMode.InteractionCell);
            scan.WithEffect(EffecterDefOf.Research, TargetIndex.A);
            scan.defaultCompleteMode = ToilCompleteMode.Delay;
            scan.defaultDuration = JobEndInterval;
            scan.activeSkill = () => SkillDefOf.Intellectual;
            yield return scan;
            yield return Toils_General.Wait(2);
        }
    }
}
