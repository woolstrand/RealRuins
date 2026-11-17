using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RealRuins {
    // Added to research benches, the comms console and the long range scanner by 1.7/Patches/RuinsScanner_Patch.xml.
    // Any other building gets the same ability by adding this comp (it needs an interaction cell).
    public class CompProperties_RuinsScanner : CompProperties {
        public RuinsScanDef scanDef;

        public CompProperties_RuinsScanner() {
            compClass = typeof(CompRuinsScanner);
        }

        public override IEnumerable<string> ConfigErrors(ThingDef parentDef) {
            foreach (string error in base.ConfigErrors(parentDef)) {
                yield return error;
            }
            if (scanDef == null) {
                yield return "CompProperties_RuinsScanner needs a scanDef";
            }
        }
    }

    [StaticConstructorOnStartup]
    public class CompRuinsScanner : ThingComp {
        private static readonly Texture2D ScanIcon = ContentFinder<Texture2D>.Get("ruinedbase");

        // Toggled by the player. Pawns scan only at enabled buildings, unless ordered to ("Prioritize").
        public bool scanningEnabled;

        private CompPowerTrader power;

        public RuinsScanDef ScanDef => ((CompProperties_RuinsScanner)props).scanDef;

        public override void PostSpawnSetup(bool respawningAfterLoad) {
            base.PostSpawnSetup(respawningAfterLoad);
            power = parent.TryGetComp<CompPowerTrader>();
        }

        public AcceptanceReport CanScanNow {
            get {
                if (parent.Faction != Faction.OfPlayer) {
                    return false;
                }
                if (power != null && !power.PowerOn) {
                    return "NoPower".Translate().CapitalizeFirst();
                }
                if (!SnapshotStoreManager.Instance.CanFireLargeEvent()) {
                    return "RealRuins_RuinsScan_NotEnoughBlueprints".Translate();
                }
                if (RuinsScanTracker.Instance?.SignalPending ?? false) {
                    return "RealRuins_RuinsScan_Locating".Translate();
                }
                return true;
            }
        }

        public static IEnumerable<Building> ScannersOn(Map map, RuinsScanDef scanDef = null) {
            return map.listerBuildings.allBuildingsColonist.Where(b => {
                CompRuinsScanner comp = b.TryGetComp<CompRuinsScanner>();
                return comp != null && (scanDef == null || comp.ScanDef == scanDef);
            });
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra() {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra()) {
                yield return gizmo;
            }
            if (parent.Faction != Faction.OfPlayer) {
                yield break;
            }

            RuinsScanDef settings = ScanDef;
            var toggle = new Command_Toggle {
                defaultLabel = settings.LabelCap,
                defaultDesc = settings.description.Formatted(settings.minIntellectualSkill, settings.mtbDaysAtMaxSkill.ToString("0.#"), settings.mtbDaysAtMinSkill.ToString("0.#")),
                icon = ScanIcon,
                isActive = () => scanningEnabled,
                toggleAction = () => scanningEnabled = !scanningEnabled
            };
            // Never block switching it off
            if (!scanningEnabled && !SnapshotStoreManager.Instance.CanFireLargeEvent()) {
                toggle.Disable("RealRuins_RuinsScan_NotEnoughBlueprints".Translate());
            }
            yield return toggle;
        }

        public override string CompInspectStringExtra() {
            if (parent.Faction != Faction.OfPlayer) {
                return null;
            }
            if (RuinsScanTracker.Instance?.SignalPending ?? false) {
                return "RealRuins_RuinsScan_Locating".Translate();
            }
            if (scanningEnabled) {
                return "RealRuins_RuinsScan_Enabled".Translate(ScanDef.label);
            }
            return null;
        }

        public override void PostExposeData() {
            base.PostExposeData();
            Scribe_Values.Look(ref scanningEnabled, "realRuinsScanningEnabled", false);
        }
    }
}
