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
using HugsLib;

namespace RealRuins
{
    [StaticConstructorOnStartup]
    class RealRuins : ModBase {

        public override string ModIdentifier => "RealRuins";

        static RealRuins() {
            DateTime startTime = DateTime.Now;
            Debug.Message("RealRuins started patching at {0}", startTime);
            var harmony = HarmonyInstance.Create("com.woolstrand.realruins");

            harmony.PatchAll(Assembly.GetExecutingAssembly());
            Debug.Message("RealRuins finished patching at {0} ({1} msec)", DateTime.Now, (DateTime.Now - startTime).TotalMilliseconds);

            if (RealRuins_ModSettings.allowDownloads) {
                SnapshotManager.Instance.LoadSomeSnapshots();
            }
            SnapshotStoreManager.Instance.CheckCacheSizeLimits();

        }

        static class SnapshotSaver {

            public static void SaveSnapshot() {
                if (!RealRuins_ModSettings.allowUploads && !RealRuins_ModSettings.offlineMode) return;
                if (Find.CurrentMap != null && !Find.CurrentMap.IsPlayerHome) return;
               
                Debug.Message("Not a temp incident, ok");
                SnapshotManager.Instance.UploadCurrentMapSnapshot();
            }
        }

        /*
        [HarmonyPatch(typeof(UIRoot_Entry), "Init", new Type[0])]
        static class UIRoot_Entry_Init_Patch {
            static void Postfix() {
                if (RealRuins_ModSettings.allowDownloads) {
                    SnapshotManager.Instance.LoadSomeSnapshots();
                }
                SnapshotStoreManager.Instance.CheckCacheSizeLimits();
            }
        }
        */

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

        [HarmonyPatch(typeof(TaleReference), "ExposeData")]
        class TaleReference_ExposeData_Patch {
            static void Postfix(TaleReference __instance) {
                if (__instance is BakedTaleReference reference) {
                    Scribe_Values.Look(ref reference.bakedTale, "bakedTale", "Default Baked Tale Whatever", false);
                }
            }
        }
        
        [HarmonyPatch(typeof(TaleReference), "GenerateText")]
        class TaleReference_GenerateText_Patch {
            static bool Prefix(TaleReference __instance, ref string __result) {
                if (!(__instance is BakedTaleReference reference)) return true;
                __result = reference.bakedTale;
               // Debug.Message("Set result of generate text to {0}", __result);
                return false;
            }
        }

    }
    
    public static class Art_Extensions {
         
        public static void InitializeArt(this CompArt art, string author, string title, string bakedTaleData) {
           // Debug.Message("Initializing custom art");
            var prop = art.GetType().GetField("taleRef", System.Reflection.BindingFlags.NonPublic
                                                  | System.Reflection.BindingFlags.Instance);
           // Debug.Message("Got prop");
            prop.SetValue(art, new BakedTaleReference(bakedTaleData));
           // Debug.Message("Set value");
            
            var titleProp = art.GetType().GetField("titleInt", System.Reflection.BindingFlags.NonPublic
                                                               | System.Reflection.BindingFlags.Instance);
            //Debug.Message("Got title prop");
            titleProp.SetValue(art, title);

            var authProp = art.GetType().GetField("authorNameInt", System.Reflection.BindingFlags.NonPublic
                                                               | System.Reflection.BindingFlags.Instance);
            //Debug.Message("Got author prop");
            authProp.SetValue(art, author);

        }
    }
}