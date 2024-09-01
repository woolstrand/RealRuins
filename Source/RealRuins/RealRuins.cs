using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace RealRuins
{

    [StaticConstructorOnStartup]
    internal class RealRuins
    {
        private static class SnapshotSaver
        {
            public static void SaveSnapshot()
            {
                if ((RealRuins_ModSettings.allowUploads || RealRuins_ModSettings.offlineMode) && Find.CurrentMap != null && Find.CurrentMap.IsPlayerHome)
                {
                    SnapshotManager.Instance.UploadCurrentMapSnapshot();
                }
            }
        }

        [HarmonyPatch(typeof(UIRoot_Entry), "Init", new Type[] { })]
        private static class UIRoot_Entry_Init_Patch
        {
            private static void Postfix()
            {
                if (RealRuins_ModSettings.allowDownloads && !RealRuins_ModSettings.offlineMode && SnapshotStoreManager.Instance.StoredSnapshotsCount() < 100)
                {
                    SnapshotManager.Instance.AggressiveLoadSnapshots();
                }
                SnapshotStoreManager.Instance.CheckCacheSizeLimits();
            }
        }

        [HarmonyPatch(typeof(Page_SelectStartingSite), "PostOpen")]
        private static class Page_SelectStartingSite_PostOpen_Patch
        {
            private static void Postfix()
            {
                if (RealRuins_ModSettings.planetaryRuinsOptions.allowOnStart)
                {
                    Find.WindowStack.Add(new Page_PlanetaryRuinsLoader());
                    PlanetaryRuinsInitData.shared.state = PlanetaryRuinsState.configuring;
                    PlanetaryRuinsInitData.shared.selectedMapSize = Find.GameInitData.mapSize;
                }
            }
        }

        [HarmonyPatch(typeof(Window), "Close")]
        private static class Window_Close_Patch
        {
            private static void Postfix(Window __instance)
            {
                if (__instance is Dialog_AdvancedGameConfig && PlanetaryRuinsInitData.shared.state == PlanetaryRuinsState.configuring && Find.GameInitData.mapSize != PlanetaryRuinsInitData.shared.selectedMapSize)
                {
                    Find.WindowStack.Add(new Page_PlanetaryRuinsLoader(forceCleanup: true));
                    PlanetaryRuinsInitData.shared.state = PlanetaryRuinsState.configuring;
                    PlanetaryRuinsInitData.shared.selectedMapSize = Find.GameInitData.mapSize;
                }
            }
        }

        [HarmonyPatch(typeof(Page_SelectStartingSite), "CanDoNext")]
        private static class Page_SelectStartingSite_CanDoNext_Patch
        {
            private static bool Prefix(Page_SelectStartingSite __instance, ref bool __result)
            {
                int selectedTile = Find.WorldInterface.SelectedTile;
                List<WorldObject> selectedObjects = Find.WorldInterface.selector.SelectedObjects;
                WorldObject worldObject = null;
                if (selectedObjects.Count > 0)
                {
                    worldObject = selectedObjects.First();
                }
                if (selectedTile > 0 && worldObject == null)
                {
                    worldObject = Find.WorldObjects.WorldObjectAt<RealRuinsPOIWorldObject>(selectedTile);
                }
                if (worldObject != null && worldObject is RealRuinsPOIWorldObject)
                {
                    Debug.Log("StartGame", "found selected object, overriding");
                    Find.WorldInterface.SelectedTile = worldObject.Tile;
                    __result = true;
                    return false;
                }
                Debug.Log("StartGame", "not found selected object, not overriding");
                return true;
            }
        }

        [HarmonyPatch(typeof(Page_SelectStartingSite), "DoNext")]
        private static class Page_SelectStartingSite_DoNext_Patch
        {
            private static bool forceCallOriginal;

            private static bool Prefix(Page_SelectStartingSite __instance)
            {
                Debug.Log("StartGame", "DoNext prefix");
                int selectedTile = Find.WorldInterface.SelectedTile;
                if (selectedTile > 0)
                {
                    Debug.Log("StartGame", "Got selected tile");
                    RealRuinsPOIWorldObject selectedObject = Find.WorldObjects.WorldObjectAt<RealRuinsPOIWorldObject>(selectedTile);
                    if (selectedObject != null)
                    {
                        Debug.Log("StartGame", "Got POI Object");
                        if (selectedObject.Faction != null && selectedObject.Faction != Faction.OfPlayer && !forceCallOriginal)
                        {
                            Debug.Log("StartGame", "Got non-nil faction");
                            TaggedString taggedString = "RealRuins.FactionOwnedStartTile".Translate();
                            TaggedString taggedString2 = "RealRuins.FactionOwnedStartTileDesc".Translate();
                            TaggedString taggedString3 = "RealRuins.FactionOwnedStartTile.MakeAbandoned".Translate();
                            TaggedString taggedString4 = "RealRuins.FactionOwnedStartTile.CaptureUntouched".Translate();
                            TaggedString taggedString5 = "RealRuins.FactionOwnedStartTile.Attack".Translate();
                            TaggedString taggedString6 = "RealRuins.FactionOwnedStartTile.Cancel".Translate();
                            string[] actions = new string[4] { taggedString3, taggedString4, taggedString5, taggedString6 };
                            SmallQuestionDialog window = new SmallQuestionDialog(taggedString, taggedString2, actions, delegate (int selection)
                            {
                                PlanetaryRuinsInitData.shared.startingPOI = selectedObject;
                                switch (selection)
                                {
                                    default:
                                        return;
                                    case 0:
                                        PlanetaryRuinsInitData.shared.startingPOI.SetFaction(null);
                                        PlanetaryRuinsInitData.shared.settleMode = SettleMode.normal;
                                        break;
                                    case 1:
                                        PlanetaryRuinsInitData.shared.startingPOI.SetFaction(Faction.OfPlayer);
                                        PlanetaryRuinsInitData.shared.settleMode = SettleMode.takeover;
                                        break;
                                    case 2:
                                        PlanetaryRuinsInitData.shared.settleMode = SettleMode.attack;
                                        forceCallOriginal = true;
                                        break;
                                    case 3:
                                        PlanetaryRuinsInitData.shared.startingPOI = null;
                                        return;
                                }
                                if (selection != 3)
                                {
                                    Find.WorldObjects.Remove(selectedObject);
                                }
                                Debug.Log("StartGame", "Re-invoking original");
                                Type type = __instance.GetType();
                                MethodInfo method = type.GetMethod("DoNext", BindingFlags.Instance | BindingFlags.NonPublic);
                                method.Invoke(__instance, null);
                            });
                            Find.WindowStack.Add(window);
                            Debug.Log("StartGame", "Skpping original");
                            return false;
                        }
                        if (!forceCallOriginal)
                        {
                            Debug.Log("StartGame", "Got nil faction, settling in abandoned ruins");
                            PlanetaryRuinsInitData.shared.startingPOI = selectedObject;
                            PlanetaryRuinsInitData.shared.startingPOI.SetFaction(null);
                            PlanetaryRuinsInitData.shared.settleMode = SettleMode.normal;
                            Find.WorldObjects.Remove(selectedObject);
                        }
                    }
                }
                forceCallOriginal = false;
                Debug.Log("StartGame", "Falling through and going to original");
                return true;
            }
        }

        [HarmonyPatch(typeof(GameDataSaveLoader), "SaveGame", new Type[] { typeof(string) })]
        private class SaveGame_Patch
        {
            private static void Postfix()
            {
                SnapshotSaver.SaveSnapshot();
            }
        }

        [HarmonyPatch(typeof(Game), "LoadGame")]
        private class LoadGame_Patch
        {
            private static void Postfix()
            {
                List<string> list = new List<string>();
                foreach (WorldObject allWorldObject in Find.WorldObjects.AllWorldObjects)
                {
                    if (!(allWorldObject is RealRuinsPOIWorldObject))
                    {
                        continue;
                    }
                    RealRuinsPOIComp component = allWorldObject.GetComponent<RealRuinsPOIComp>();
                    if (component != null)
                    {
                        string blueprintName = component.blueprintName;
                        if (!SnapshotStoreManager.HasPlanetaryBlueprintForCurrentGame(blueprintName))
                        {
                            list.Add(blueprintName);
                        }
                    }
                }
                if (list.Count() > 0)
                {
                    SnapshotManager.Instance.AggressiveLoadSnaphotsFromList(list, SnapshotStoreManager.CurrentGamePath());
                }
            }
        }

        [HarmonyPatch(typeof(GenGameEnd), "EndGameDialogMessage", new Type[]
        {
        typeof(string),
        typeof(bool),
        typeof(Color)
        })]
        private class GameOver_Patch
        {
            private static void Postfix()
            {
                SnapshotSaver.SaveSnapshot();
            }
        }

        [HarmonyPatch(typeof(TaleReference), "ExposeData")]
        private class TaleReference_ExposeData_Patch
        {
            private static void Postfix(TaleReference __instance)
            {
                if (__instance is BakedTaleReference bakedTaleReference)
                {
                    Scribe_Values.Look(ref bakedTaleReference.bakedTale, "bakedTale", "Default Baked Tale Whatever");
                }
            }
        }

        [HarmonyPatch(typeof(GenStep_ScatterRuinsSimple), "ScatterAt", new Type[]
        {
        typeof(IntVec3),
        typeof(Map),
        typeof(GenStepParams),
        typeof(int)
        })]
        private class GenStep_ScatterRuinsSimple_ScatterAt_Patch
        {
            private static bool Prefix(GenStep_ScatterRuinsSimple __instance)
            {
                if (RealRuins_ModSettings.preserveStandardRuins)
                {
                    return true;
                }
                return false;
            }
        }

        [HarmonyPatch(typeof(MapGenerator), "GenerateMap")]
        private class MapGenerator_GenerateMap_Patch
        {
            private static void Prefix(ref IntVec3 mapSize, ref MapParent parent, ref MapGeneratorDef mapGenerator, ref IEnumerable<GenStepWithParams> extraGenStepDefs, ref Action<Map> extraInitBeforeContentGen)
            {
                int tile = parent.Tile;
                Debug.Log("StartGame", "Starting tile: {0}", tile);
                RealRuinsPOIWorldObject startingPOI = PlanetaryRuinsInitData.shared.startingPOI;
                if (startingPOI != null)
                {
                    Debug.Log("[MapGen]", "Found RR POI in game start context. Generator def name was {0}, changing to {1}", mapGenerator.defName, startingPOI.MapGeneratorDef.defName);
                    mapGenerator = startingPOI.MapGeneratorDef;
                    extraGenStepDefs.Concat(startingPOI.ExtraGenStepDefs);
                }
                else
                {
                    Debug.Log("[MapGen]", "Game start context is empty, generating map as usual");
                }
            }
        }

        [HarmonyPatch(typeof(TaleReference), "GenerateText")]
        private class TaleReference_GenerateText_Patch
        {
            private static bool Prefix(TaleReference __instance, ref TaggedString __result)
            {
                if (!(__instance is BakedTaleReference bakedTaleReference))
                {
                    return true;
                }
                __result = new TaggedString(bakedTaleReference.bakedTale);
                return false;
            }
        }

        [HarmonyPatch(typeof(Dialog_FormCaravan), "TryReformCaravan")]
        private class DialogFormCaravan_Patch
        {
            private static bool Prefix(Dialog_FormCaravan __instance, ref bool __result)
            {
                if (RealRuins_ModSettings.caravanReformType == 1)
                {
                    return true;
                }
                Map map = (Map)typeof(Dialog_FormCaravan).GetField("map", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(__instance);
                if (map.Parent is AbandonedBaseWorldObject)
                {
                    int num = (int)typeof(Dialog_FormCaravan).GetField("destinationTile", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(__instance);
                    if (num < 0)
                    {
                        Messages.Message("MessageMustChooseRouteFirst".Translate(), MessageTypeDefOf.RejectInput, historical: false);
                        return false;
                    }
                    if ((bool)typeof(Dialog_FormCaravan).GetMethod("TryFormAndSendCaravan", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(__instance, null))
                    {
                        __instance.Close(doCloseSound: false);
                    }
                    return false;
                }
                return true;
            }
        }

        public static string SingleFileName;

        public static bool SingleFile;

        //public override string ModIdentifier => "RealRuins";

        static RealRuins()
        {
            //IL_0039: Unknown result type (might be due to invalid IL or missing references)
            //IL_003f: Expected O, but got Unknown
            SingleFileName = null;
            SingleFile = SingleFileName != null;
            DateTime now = DateTime.Now;
            Debug.SysLog("RealRuins started patching at {0}", now);
            Harmony val = new Harmony("com.woolstrand.realruins");
            val.PatchAll(Assembly.GetExecutingAssembly());
            Debug.SysLog("RealRuins performing manual patching optional mods at {0}", now);
            if (LoadedModManager.RunningMods.ToList().Exists((ModContentPack m) => m.Name.Contains("SRTS")))
            {
                Debug.SysLog("SRTS found, patching...");
                PatchSRTS(val);
            }
            else
            {
                Debug.SysLog("SRTS not found, skipping...");
            }
            Debug.SysLog("RealRuins finished patching at {0} ({1} msec)", DateTime.Now, (DateTime.Now - now).TotalMilliseconds);
            if (RealRuins_ModSettings.allowDownloads && !RealRuins_ModSettings.offlineMode)
            {
                SnapshotManager.Instance.LoadSomeSnapshots();
            }
            SnapshotStoreManager.Instance.CheckCacheSizeLimits();
        }

        private static void PatchSRTS(Harmony harmony)
        {
            //IL_004b: Unknown result type (might be due to invalid IL or missing references)
            //IL_0057: Expected O, but got Unknown
            Type type = Type.GetType("SRTS.SRTSStatic, SRTS");
            MethodInfo method = type.GetMethod("getFM", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);
            if (method == null)
            {
                Debug.SysLog("NO getFM method found!");
            }
            else
            {
                harmony.Patch((MethodBase)method, (HarmonyMethod)null, new HarmonyMethod(typeof(RealRuins), "SRTS_getFM_Postfix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null);
            }
        }

        public static IEnumerable<FloatMenuOption> SRTS_getFM_Postfix(IEnumerable<FloatMenuOption> options, WorldObject wobj, IEnumerable<IThingHolder> ih, object comp, Caravan car)
        {
            if (options.Count() > 0)
            {
                return options;
            }
            IEnumerable<FloatMenuOption> enumerable = Enumerable.Empty<FloatMenuOption>();
            enumerable.ConcatIfNotNull(options);
            Type type = Type.GetType("SRTS.SRTSArrivalActionUtility, SRTS");
            MethodInfo method = type.GetMethod("GetFloatMenuOptions", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);
            MethodInfo methodInfo = method.MakeGenericMethod(typeof(TransportPodsArrivalAction_VisitRuinsPOI));
            MethodInfo methodInfo2 = method.MakeGenericMethod(typeof(TransportPodsArrivalAction_VisitRuins));
            Func<FloatMenuAcceptanceReport> func = () => FloatMenuAcceptanceReport.WasAccepted;
            Func<TransportPodsArrivalAction_VisitRuinsPOI> func2 = () => new TransportPodsArrivalAction_VisitRuinsPOI(wobj as MapParent, PawnsArrivalModeDefOf.CenterDrop);
            Func<TransportPodsArrivalAction_VisitRuinsPOI> func3 = () => new TransportPodsArrivalAction_VisitRuinsPOI(wobj as MapParent, PawnsArrivalModeDefOf.EdgeDrop);
            Func<TransportPodsArrivalAction_VisitRuins> func4 = () => new TransportPodsArrivalAction_VisitRuins(wobj as MapParent, PawnsArrivalModeDefOf.CenterDrop);
            Func<TransportPodsArrivalAction_VisitRuins> func5 = () => new TransportPodsArrivalAction_VisitRuins(wobj as MapParent, PawnsArrivalModeDefOf.EdgeDrop);
            if (wobj is RealRuinsPOIWorldObject)
            {
                object[] parameters = new object[6]
                {
                func,
                func3,
                wobj.Label + ": " + "DropAtEdge".Translate().RawText,
                comp,
                wobj.Tile,
                car
                };
                enumerable = methodInfo.Invoke(null, parameters) as IEnumerable<FloatMenuOption>;
                parameters = new object[6]
                {
                func,
                func2,
                wobj.Label + ": " + "DropInCenter".Translate().RawText,
                comp,
                wobj.Tile,
                car
                };
                enumerable = enumerable.Concat(methodInfo.Invoke(null, parameters) as IEnumerable<FloatMenuOption>);
            }
            else if (wobj is AbandonedBaseWorldObject)
            {
                object[] parameters2 = new object[6]
                {
                func,
                func5,
                wobj.Label + ": " + "DropAtEdge".Translate().RawText,
                comp,
                wobj.Tile,
                car
                };
                enumerable = methodInfo2.Invoke(null, parameters2) as IEnumerable<FloatMenuOption>;
                parameters2 = new object[6]
                {
                func,
                func4,
                wobj.Label + ": " + "DropInCenter".Translate().RawText,
                comp,
                wobj.Tile,
                car
                };
                enumerable = enumerable.Concat(methodInfo2.Invoke(null, parameters2) as IEnumerable<FloatMenuOption>);
            }
            return enumerable;
        }
    }
}