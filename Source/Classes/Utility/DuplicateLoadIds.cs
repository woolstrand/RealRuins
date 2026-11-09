using System.Collections.Generic;
using HarmonyLib;
using Verse;

namespace RealRuins {
    // Older versions left saves with duplicate load ids: every baked log entry had id 0 (same load id for the same timestamp),
    // and restored pawns kept the hediff ids of the blueprint's game. Vanilla logs an error for each duplicate on every load
    // and keeps the first object of an id, so the first one keeps its id here too and the others get a negative one:
    // vanilla ids are never negative, and the id manager may not be loaded yet while these are read.
    static class DuplicateLoadIds {
        private static readonly HashSet<int> logEntryTimestamps = new HashSet<int>();
        private static readonly HashSet<int> hediffIds = new HashSet<int>();
        private static int lastNegativeId = 0;

        public static void Reset() {
            logEntryTimestamps.Clear();
            hediffIds.Clear();
            lastNegativeId = 0;
        }

        // id of a baked log entry being loaded; only entries saved without an id (0) can collide
        public static int LogEntryId(int ticksAbs, int logID) {
            if (logID != 0 || logEntryTimestamps.Add(ticksAbs)) return logID;
            return --lastNegativeId;
        }

        [HarmonyPatch(typeof(Hediff), nameof(Hediff.ExposeData))]
        static class Hediff_ExposeData_Patch {
            static void Postfix(Hediff __instance) {
                if (Scribe.mode != LoadSaveMode.LoadingVars) return;
                if (!hediffIds.Add(__instance.loadID)) {
                    __instance.loadID = --lastNegativeId;
                }
            }
        }
    }
}
