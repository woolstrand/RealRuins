using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace RealRuins {
    // Research work type, ranked above research: a bench switched to scanning is used for scanning, not research.
    // One work giver per RuinsScanDef (RuinsScanDef.workGiver), so each method has its own work label.
    public class WorkGiver_ScanForRuins : WorkGiver_Scanner {
        private RuinsScanDef scanDef;

        private RuinsScanDef ScanDef => scanDef ?? (scanDef = DefDatabase<RuinsScanDef>.AllDefs.First(d => d.workGiver == def));

        public override PathEndMode PathEndMode => PathEndMode.InteractionCell;

        // All scanner buildings of this method, not just enabled ones: "Prioritize" lists only things found here.
        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn) {
            return CompRuinsScanner.ScannersOn(pawn.Map, ScanDef).Cast<Thing>();
        }

        public override bool ShouldSkip(Pawn pawn, bool forced = false) {
            if (RuinsScanTracker.Instance?.SignalPending ?? true) {
                return true;
            }
            if (!forced && !CompRuinsScanner.ScannersOn(pawn.Map, ScanDef).Any(b => b.GetComp<CompRuinsScanner>().scanningEnabled)) {
                return true;
            }
            return false;
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false) {
            CompRuinsScanner comp = t.TryGetComp<CompRuinsScanner>();
            if (comp == null || comp.ScanDef != ScanDef || t.Faction != pawn.Faction) {
                return false;
            }
            if (!forced && !comp.scanningEnabled) {
                return false;
            }

            AcceptanceReport report = comp.CanScanNow;
            if (!report.Accepted) {
                if (!report.Reason.NullOrEmpty()) {
                    JobFailReason.Is(report.Reason);
                }
                return false;
            }

            report = ScanDef.PawnCanScan(pawn);
            if (!report.Accepted) {
                JobFailReason.Is(report.Reason);
                return false;
            }

            if (t.IsBurning() || t.IsForbidden(pawn) || !pawn.CanReserve(t, 1, -1, null, forced)) {
                return false;
            }
            if (t.def.hasInteractionCell && !pawn.CanReserveSittableOrSpot(t.InteractionCell, t, forced)) {
                return false;
            }
            return true;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false) {
            return JobMaker.MakeJob(ScanDef.job, t);
        }
    }
}
