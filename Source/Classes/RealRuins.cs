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
using RimWorld.BaseGen;

using SRTS;

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

            Debug.SysLog("RealRuins performing manual patching optional mods at {0}", startTime);

            var srtsExists = LoadedModManager.RunningMods.ToList().Exists(m => m.Name.Contains("SRTS"));
            if (srtsExists) {
                Debug.SysLog("SRTS found, patching...");
                PatchSRTS(harmony);
            } else {
                Debug.SysLog("SRTS not found, skipping...");
            }

            Debug.SysLog("RealRuins finished patching at {0} ({1} msec)", DateTime.Now, (DateTime.Now - startTime).TotalMilliseconds);

            if (RealRuins_ModSettings.allowDownloads && !RealRuins_ModSettings.offlineMode) {
                SnapshotManager.Instance.LoadSomeSnapshots();
            }
            SnapshotStoreManager.Instance.CheckCacheSizeLimits();

        }

        static void PatchSRTS(Harmony harmony) {
            Type classType = Type.GetType("SRTS.SRTSStatic, SRTS");
            MethodInfo originalMethod = classType.GetMethod("getFM", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);
            if (originalMethod == null) {
                Debug.SysLog("NO getFM method found!");
                return;
            }

            harmony.Patch(
                original: originalMethod,
                prefix: null,
                postfix: new HarmonyMethod(typeof(RealRuins), nameof(SRTS_getFM_Postfix)));
        }

        public static IEnumerable<FloatMenuOption> SRTS_getFM_Postfix(IEnumerable<FloatMenuOption> options, WorldObject wobj, IEnumerable<IThingHolder> ih, object comp, Caravan car) {
            // On some reason this method is called TWICE: once with empty collection (as expected), the second time with what I returned just before.
            // So to avoid doubling I have to skip all code if there already are options. Not sure how it will work in future, but hope everything will be fine.
            if (options.Count() > 0) {
                return options;
            }

            IEnumerable<FloatMenuOption> newOptions = Enumerable.Empty<FloatMenuOption>();
            newOptions.ConcatIfNotNull(options);

            //Get type, don't forget to specify assembly name
            Type t = Type.GetType("SRTS.SRTSArrivalActionUtility, SRTS");

            //Get generic method
            var m = t.GetMethod("GetFloatMenuOptions", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);

            //Create a specific method for a specific type from the generic above
            var mGenericPOI = m.MakeGenericMethod(typeof(TransportPodsArrivalAction_VisitRuinsPOI));
            var mGenericRuins = m.MakeGenericMethod(typeof(TransportPodsArrivalAction_VisitRuins));

            // Lambdas are not objects, so we can't pass them into object[] array, we have to make them into delegates
            var accReport = new Func<FloatMenuAcceptanceReport>(() => FloatMenuAcceptanceReport.WasAccepted);
            var arrivalActionPOICenter = new Func<TransportPodsArrivalAction_VisitRuinsPOI>(() => new TransportPodsArrivalAction_VisitRuinsPOI(wobj as MapParent, PawnsArrivalModeDefOf.CenterDrop));
            var arrivalActionPOIEdge = new Func<TransportPodsArrivalAction_VisitRuinsPOI>(() => new TransportPodsArrivalAction_VisitRuinsPOI(wobj as MapParent, PawnsArrivalModeDefOf.EdgeDrop));
            var arrivalActionRuinsCenter = new Func<TransportPodsArrivalAction_VisitRuins>(() => new TransportPodsArrivalAction_VisitRuins(wobj as MapParent, PawnsArrivalModeDefOf.CenterDrop));
            var arrivalActionRuinsEdge = new Func<TransportPodsArrivalAction_VisitRuins>(() => new TransportPodsArrivalAction_VisitRuins(wobj as MapParent, PawnsArrivalModeDefOf.EdgeDrop));

            if (wobj is RealRuinsPOIWorldObject) {
                object[] parameters = new object[] {
                    accReport,
                    arrivalActionPOIEdge,
                    wobj.Label + ": " + Translator.Translate("DropAtEdge").RawText,
                    comp,
                    wobj.Tile,
                    car };
                newOptions = mGenericPOI.Invoke(null, parameters) as IEnumerable<FloatMenuOption>;

                parameters = new object[] {
                    accReport, 
                    arrivalActionPOICenter,
                    wobj.Label + ": " + Translator.Translate("DropInCenter").RawText,
                    comp,
                    wobj.Tile,
                    car };
                newOptions = newOptions.Concat(mGenericPOI.Invoke(null, parameters) as IEnumerable<FloatMenuOption>);

            } else if (wobj is AbandonedBaseWorldObject) {

                object[] parameters = new object[] {
                    accReport,
                    arrivalActionRuinsEdge,
                    wobj.Label + ": " + Translator.Translate("DropAtEdge").RawText,
                    comp,
                    wobj.Tile,
                    car };
                newOptions = mGenericRuins.Invoke(null, parameters) as IEnumerable<FloatMenuOption>;

                parameters = new object[] {
                    accReport,
                    arrivalActionRuinsCenter,
                    wobj.Label + ": " + Translator.Translate("DropInCenter").RawText,
                    comp,
                    wobj.Tile,
                    car };
                newOptions = newOptions.Concat(mGenericRuins.Invoke(null, parameters) as IEnumerable<FloatMenuOption>);
            }

            return newOptions;
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

        [HarmonyPatch(typeof(Page_SelectStartingSite), "PostOpen")]
        static class Page_SelectStartingSite_PostOpen_Patch {
            static void Postfix() {
                Find.WindowStack.Add(new Page_PlanetaryRuinsLoader());
                PlanetaryRuinsInitData.shared.state = PlanetaryRuinsState.configuring;
                PlanetaryRuinsInitData.shared.selectedMapSize = Find.GameInitData.mapSize;
            }
        }

        [HarmonyPatch(typeof(Verse.Window), "Close")]
        static class Window_Close_Patch {
            static void Postfix(Verse.Window __instance) {
                if (__instance is Dialog_AdvancedGameConfig) {
                    if (PlanetaryRuinsInitData.shared.state == PlanetaryRuinsState.configuring &&
                        Find.GameInitData.mapSize != PlanetaryRuinsInitData.shared.selectedMapSize) {
                        //add dialog yes/no
                        Find.WindowStack.Add(new Page_PlanetaryRuinsLoader());
                    }
                }
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

        
        [HarmonyPatch(typeof(GenStep_ScatterRuinsSimple), "ScatterAt", typeof(IntVec3), typeof(Map), typeof(GenStepParams), typeof(int))]
        class GenStep_ScatterRuinsSimple_ScatterAt_Patch {
            static bool Prefix(GenStep_ScatterRuinsSimple __instance) {
                if (RealRuins_ModSettings.preserveStandardRuins) return true;
                else return false;
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