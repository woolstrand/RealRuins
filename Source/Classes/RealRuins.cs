using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.IO;

using HarmonyLib;
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
            Debug.SysLog("RealRuins started patching at {0}", startTime);
            var harmony = new Harmony("com.woolstrand.realruins");
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            Debug.SysLog("RealRuins finished patching at {0} ({1} msec)", DateTime.Now, (DateTime.Now - startTime).TotalMilliseconds);

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
        

        [HarmonyPatch(typeof(GameDataSaveLoader), "SaveGame", typeof(string))]
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
            static bool Prefix(TaleReference __instance, ref TaggedString __result) {
                if (!(__instance is BakedTaleReference reference)) return true;
                __result = new TaggedString(reference.bakedTale);
               // Debug.Message("Set result of generate text to {0}", __result);
                return false;
            }
        }

        //RimWorld caravan forming dialog has two modes: reform and form. 
        // * "Reform" shows all items including items which are currently in colonists' inventories, BUT reformed caravan leaves instantly.
        // * "Form" makes colonists go through the map and pick up all items, but it shows only items which are stored in home area.
        // I need this dialog to show all items, but without instant caravan reforming. Unfortunately, it can be done either with extensive copy-pasting of original code or via method swizzlilng via reflections.
        [HarmonyPatch(typeof(Dialog_FormCaravan), "TryReformCaravan")]
        class DialogFormCaravan_Patch {
            static bool Prefix(Dialog_FormCaravan __instance, ref bool __result) {
                if (RealRuins_ModSettings.caravanReformType == 1) {
                    return true; //proceed with instant reforming if instant reforming is on
                } else {
                    Map map = (Map)(typeof(Dialog_FormCaravan).GetField("map", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(__instance));
                    if (map.Parent is AbandonedBaseWorldObject) {
                        //if this is a pristine ruins event map, invoke regular caravan forming INSTEAD of instant caravan forming.
                        int destinationTile = (int)(typeof(Dialog_FormCaravan).GetField("destinationTile", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(__instance));
                        if (destinationTile < 0) {
                            Messages.Message("MessageMustChooseRouteFirst".Translate(), MessageTypeDefOf.RejectInput, historical: false);
                            return false;
                        }

                        bool result = (bool)(typeof(Dialog_FormCaravan).GetMethod("TryFormAndSendCaravan", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(__instance, null));
                        if (result) {
                            __instance.Close(doCloseSound: false);
                        }
                        return false;
                    }
                }
                return true;
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
            titleProp.SetValue(art, new TaggedString(title));

            var authProp = art.GetType().GetField("authorNameInt", System.Reflection.BindingFlags.NonPublic
                                                               | System.Reflection.BindingFlags.Instance);
            //Debug.Message("Got author prop");
            authProp.SetValue(art, new TaggedString(author));

        }
    }
}