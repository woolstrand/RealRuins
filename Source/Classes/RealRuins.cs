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

        public static string SingleFileName = null;
        //public static string SingleFileName = "C:/Users/dieworld/AppData/LocalLow/Ludeon Studios/RimWorld by Ludeon Studios\\RealRuins\\9900A83A-1DFA-433F-8441-E4E22077059C.bp";
        //public static string SingleFileName = "C:/Users/dieworld/AppData/LocalLow/Ludeon Studios/RimWorld by Ludeon Studios\\RealRuins\\20180936-13488551490-jeluder.bp";
        



        public static bool SingleFile = SingleFileName != null;

        static RealRuins() {
            DateTime startTime = DateTime.Now;
            Debug.Message("RealRuins started patching at {0}", startTime);
            var harmony = HarmonyInstance.Create("com.woolstrand.realruins");

            harmony.PatchAll(Assembly.GetExecutingAssembly());
            Debug.Message("RealRuins finished patching at {0} ({1} msec)", DateTime.Now, (DateTime.Now - startTime).TotalMilliseconds);

            if (RealRuins_ModSettings.allowDownloads && !RealRuins_ModSettings.offlineMode) {
                SnapshotManager.Instance.LoadSomeSnapshots();
            }
            SnapshotStoreManager.Instance.CheckCacheSizeLimits();

        }

        static class SnapshotSaver {

            public static void SaveSnapshot() {
                if (!RealRuins_ModSettings.allowUploads && !RealRuins_ModSettings.offlineMode) return;
                if (Find.CurrentMap != null && !Find.CurrentMap.IsPlayerHome) return;
               
                SnapshotManager.Instance.UploadCurrentMapSnapshot();
            }
        }

        
        [HarmonyPatch(typeof(UIRoot_Entry), "Init", new Type[0])]
        static class UIRoot_Entry_Init_Patch {
            static void Postfix() {
                if (RealRuins_ModSettings.allowDownloads && !RealRuins_ModSettings.offlineMode && SnapshotStoreManager.Instance.StoredSnapshotsCount() < 100) {
                    SnapshotManager.Instance.AggressiveLoadSnapshots();
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

        [HarmonyPatch(typeof(GenHostility), "AnyHostileActiveThreatToPlayer", typeof(Map))]
        class PlayerThreat_Patch {
            static bool Prefix(ref bool __result, Map map) {
                if (RealRuins_ModSettings.allowInstantCaravanReform) {
                    return true; //ignore if setting is off
                } else if (map.Parent is Site) {
                    if (((Site)(map.Parent))?.core?.def?.defName == "RuinedBaseSite") {
                        RuinedBaseComp comp = (map.Parent as WorldObject)?.GetComponent<RuinedBaseComp>();
                        if (comp?.mapExitLocked == true) {
                            __result = true; //Always think there is something hostile in an abandoned base event if it was not explicitly unlocket dy the map itself
                            return false; //prevent original method execution
                        }
                    }
                }
                return true;
            }
        }

/*
        [HarmonyPatch(typeof(Scenario), "GetFirstConfigPage")]
        class WindowContents_Patch {
            public static void DoWindowContentsPostfix(Rect rect, Page_ConfigureStartingPawns __instance) {
                //IL_00b4: Unknown result type (might be due to invalid IL or missing references)
                Vector2 vector = new Vector2(150f, 38f);
                float y = rect.height + 45f;
                if (Widgets.ButtonText(new Rect(rect.x + rect.width / 2f - vector.x / 2f, y, vector.x, vector.y), Translator.Translate("EdB.PC.Page.Button.PrepareCarefully"), true, false, true)) {
                    try {
                        Page_RealRuins pageRealRuins = new Page_RealRuins();
                        Find.UIRoot.windows.Add(pageRealRuins);
                    } catch (Exception ex) {
                        //Find.get_WindowStack().Add(new DialogInitializationError());
                        //SoundStarter.PlayOneShot(SoundDefOf.ClickReject, SoundInfo.op_Implicit(null));
                        throw ex;
                    }
                }
            }
        }*/
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