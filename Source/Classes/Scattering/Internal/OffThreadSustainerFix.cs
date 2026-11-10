using System;

using HarmonyLib;
using RimWorld;
using Verse;

namespace RealRuins {
    // New game map generation runs on a worker thread. Ruins can bring buildings whose power comes on while they spawn there
    // (a geothermal generator's power plant switches itself on in PostSpawnSetup), and the comps that react to it start their
    // sound right away. A sustainer made off the main thread creates a GameObject the 1.7 engine doesn't track, and starting
    // its sample afterwards throws "Object is not tracked by the Unity world" (an error popup, and the sound never plays).
    // Vanilla defers these starts itself when it knows it runs during loading; here they are deferred whenever they come from
    // another thread, and the conditions are checked again on the main thread.
    [HarmonyPatch(typeof(CompAmbientSound), nameof(CompAmbientSound.StartSustanier))]
    static class CompAmbientSound_StartSustanier_Patch {
        static bool Prefix(CompAmbientSound __instance) {
            if (UnityData.IsInMainThread) return true;
            LongEventHandler.ExecuteWhenFinished(() => {
                ThingWithComps parent = __instance.parent;
                if (parent == null || !parent.Spawned) return;
                if ((__instance.props as CompProperties_AmbientSound)?.disabledOnUnpowered == true) {
                    CompPowerTrader power = parent.GetComp<CompPowerTrader>();
                    if (power != null && !power.PowerOn) return;
                }
                __instance.StartSustanier();
            });
            return false;
        }
    }

    [HarmonyPatch(typeof(CompPowerTrader), "StartSustainerPoweredIfInactive")]
    static class CompPowerTrader_StartSustainerPoweredIfInactive_Patch {
        private static readonly Action<CompPowerTrader> startSustainer =
            AccessTools.MethodDelegate<Action<CompPowerTrader>>(AccessTools.Method(typeof(CompPowerTrader), "StartSustainerPoweredIfInactive"));

        static bool Prefix(CompPowerTrader __instance) {
            if (UnityData.IsInMainThread) return true;
            LongEventHandler.ExecuteWhenFinished(() => {
                if (__instance.parent == null || !__instance.parent.Spawned || !__instance.PowerOn) return;
                startSustainer(__instance);
            });
            return false;
        }
    }
}
