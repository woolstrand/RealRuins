using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.IO;

using Harmony;
using Verse;
using RimWorld;
using UnityEngine;
using RimWorld.Planet;

namespace RealRuins
{
    [StaticConstructorOnStartup]
    static class RealRuins {

        public static bool detectedConfigurableMaps;

        static RealRuins() {
            var harmony = HarmonyInstance.Create("com.woolstrand.realruins");

            if (ModsConfig.ActiveModsInLoadOrder.Any((ModMetaData mod) => mod.Name.Contains("Configurable Maps"))) {
                detectedConfigurableMaps = true;
            }

            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }

        static class SnapshotSaver {

            public static void SaveSnapshot() {
                if (!RealRuins_ModSettings.allowUploads && !RealRuins_ModSettings.offlineMode) return;

                SnapshotManager.Instance.UploadCurrentMapSnapshot();
            }
        }

        [HarmonyPatch(typeof(UIRoot_Entry), "Init", new Type[0])]
        static class UIRoot_Entry_Init_Patch {
            static void Postfix() {
                if (RealRuins_ModSettings.allowDownloads) {
                    SnapshotManager.Instance.LoadSomeSnapshots();
                }
                SnapshotStoreManager.Instance.CheckCacheSizeLimits();
            }
        }

        [HarmonyPatch(typeof(GameDataSaveLoader), "SaveGame")]
        class SaveGame_Patch {
            static void Postfix() {
                SnapshotSaver.SaveSnapshot();
            }
        }

        [HarmonyPatch(typeof(GenGameEnd), "EndGameDialogMessage", typeof(string), typeof(bool), typeof(Color))]
        class GameOver_Patch {
            static void Postfix() {
                SnapshotSaver.SaveSnapshot();
            }
        }

    }
}