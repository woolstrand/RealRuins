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
using RimWorld.BaseGen;

namespace RealRuins
{
    [StaticConstructorOnStartup]
    class RealRuins
    {

        public string ModIdentifier => "RealRuins";

        public static string SingleFileName = null;
        //public static string SingleFileName = "C:/Users/dieworld/AppData/LocalLow/Ludeon Studios/RimWorld by Ludeon Studios\\RealRuins\\9900A83A-1DFA-433F-8441-E4E22077059C.bp";
        //public static string SingleFileName = "C:/Users/dieworld/AppData/LocalLow/Ludeon Studios/RimWorld by Ludeon Studios\\RealRuins\\20180936-13488551490-jeluder.bp";




        public static bool SingleFile = SingleFileName != null;

        static RealRuins()
        {
            DateTime startTime = DateTime.Now;
            Debug.SysLog("RealRuins started patching at {0}", startTime);

            var harmony = new Harmony("com.woolstrand.realruins");
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            Debug.SysLog("RealRuins finished patching at {0} ({1} msec)", DateTime.Now, (DateTime.Now - startTime).TotalMilliseconds);

            if (RealRuins_ModSettings.allowDownloads && !RealRuins_ModSettings.offlineMode)
            {
                SnapshotManager.Instance.LoadSomeSnapshots();
            }
            SnapshotStoreManager.Instance.CheckCacheSizeLimits();
            SnapshotManager.Instance.UploadPendingSnapshots();

        }

        static class SnapshotSaver
        {

            public static void SaveSnapshot()
            {
                if (!RealRuins_ModSettings.allowUploads && !RealRuins_ModSettings.offlineMode) return;
                if (Find.CurrentMap == null)
                {
                    return;
                }
                else
                {
                    if (!Find.CurrentMap.IsPlayerHome)
                    {
                        return;
                    }
                    else
                    {
                        SnapshotManager.Instance.UploadCurrentMapSnapshot();
                    }
                }
            }

            //Final snapshot of a lost colony: ignores the upload frequency limit and falls back to any home map
            //if the player is looking at some other map when the game ends.
            public static void SaveGameOverSnapshot()
            {
                if (!RealRuins_ModSettings.allowUploads && !RealRuins_ModSettings.offlineMode) return;
                //a raided planetary POI is a player home too, but it is never uploaded (see UploadMapSnapshot), so skip it here
                Map map = Find.CurrentMap;
                if (map == null || !map.IsPlayerHome || map.Parent is RealRuinsPOIWorldObject)
                {
                    map = Find.Maps.FirstOrDefault(m => m.IsPlayerHome && !(m.Parent is RealRuinsPOIWorldObject));
                }
                if (map == null) return;
                SnapshotManager.Instance.UploadMapSnapshot(map, force: true);
            }
        }

        // This patch shows planetary ruins config dialog when selecting a starting site for a new game
        [HarmonyPatch(typeof(Page_SelectStartingSite), "PostOpen")]
        static class Page_SelectStartingSite_PostOpen_Patch
        {
            static void Postfix()
            {
                if (RealRuins_ModSettings.planetaryRuinsOptions.allowOnStart == false)
                {
                    return;
                }

                Find.WindowStack.Add(new Page_PlanetaryRuinsLoader());
                PlanetaryRuinsInitData.shared.state = PlanetaryRuinsState.configuring;
                PlanetaryRuinsInitData.shared.selectedMapSize = Find.GameInitData.mapSize;
            }
        }

        // This patch shows planetary ruins configuration options after closing "map size" selector (if map size was changed)
        [HarmonyPatch(typeof(Verse.Window), "Close")]
        static class Window_Close_Patch
        {
            static void Postfix(Verse.Window __instance)
            {
                if (__instance is Dialog_AdvancedGameConfig)
                {
                    if (PlanetaryRuinsInitData.shared.state == PlanetaryRuinsState.configuring &&
                        Find.GameInitData.mapSize != PlanetaryRuinsInitData.shared.selectedMapSize)
                    {
                        //add dialog yes/no

                        Find.WindowStack.Add(new Page_PlanetaryRuinsLoader(forceCleanup: true));
                        PlanetaryRuinsInitData.shared.state = PlanetaryRuinsState.configuring;
                        PlanetaryRuinsInitData.shared.selectedMapSize = Find.GameInitData.mapSize;
                    }
                }
            }
        }

        // This patch enables pressing "next" to select a starting site on a tile with RealRuins POI inside
        [HarmonyPatch(typeof(Page_SelectStartingSite), "CanDoNext")]
        static class Page_SelectStartingSite_CanDoNext_Patch
        {
            static bool Prefix(Page_SelectStartingSite __instance, ref bool __result)
            {
                // We want to allow selecting tiles with ruins on them so you can start your new game on someone's else's base.
                int selectedTile = Find.WorldInterface.SelectedTile;
                var objects = Find.WorldInterface.selector.SelectedObjects;
                WorldObject selectedObject = null;
                if (objects.Count > 0)
                {
                    selectedObject = objects.First();
                }
                if (selectedTile > 0 && selectedObject == null)
                {
                    selectedObject = Find.WorldObjects.WorldObjectAt<RealRuinsPOIWorldObject>(selectedTile);
                }

                if (selectedObject != null && selectedObject is RealRuinsPOIWorldObject)
                {
                    Debug.Log("StartGame", "found selected object, overriding");
                    Find.WorldInterface.SelectedTile = selectedObject.Tile;
                    __result = true;
                    return false;
                }

                Debug.Log("StartGame", "not found selected object, not overriding");
                return true;
            }
        }

        // This patch asks user how he want to proceed with settling in ruins if the user plans to settle in occupied ruins
        // It aborts original method in order to wait user's input, and after the choice is made, calls original method again.
        // The chosen POI is removed from the world before that call, so the second pass finds no POI and falls through.
        // It also falls through right away if the user selects regular location or abandoned ruins.
        [HarmonyPatch(typeof(Page_SelectStartingSite), "DoNext")]
        static class Page_SelectStartingSite_DoNext_Patch
        {
            static bool Prefix(Page_SelectStartingSite __instance)
            {
                Debug.Log("StartGame", "DoNext prefix");
                int selectedTile = Find.WorldInterface.SelectedTile;

                // A POI chosen earlier (then "Back" and another tile, or an abandoned setup) must not apply to a different tile.
                // The re-invocation after the dialog below keeps it: the POI is removed from the world, but its tile is the selected one.
                RealRuinsPOIWorldObject previousPOI = PlanetaryRuinsInitData.shared.startingPOI;
                if (previousPOI != null && previousPOI.Tile != selectedTile)
                {
                    Debug.Log("StartGame", "Discarding starting POI chosen for another tile");
                    PlanetaryRuinsInitData.shared.startingPOI = null;
                }

                if (selectedTile > 0)
                {
                    Debug.Log("StartGame", "Got selected tile");
                    RealRuinsPOIWorldObject selectedObject = Find.WorldObjects.WorldObjectAt<RealRuinsPOIWorldObject>(selectedTile);
                    if (selectedObject != null)
                    {
                        Debug.Log("StartGame", "Got POI Object");

                        if (selectedObject.Faction != null && selectedObject.Faction != Faction.OfPlayer)
                        {
                            Debug.Log("StartGame", "Got non-nil faction");
                            //If selected tile contains non-abandoned ruins object we have to ask user what to do
                            var title = "RealRuins.FactionOwnedStartTile".Translate();
                            var text = "RealRuins.FactionOwnedStartTileDesc".Translate();
                            var b1 = "RealRuins.FactionOwnedStartTile.MakeAbandoned".Translate();
                            var b2 = "RealRuins.FactionOwnedStartTile.CaptureUntouched".Translate();
                            var b3 = "RealRuins.FactionOwnedStartTile.Attack".Translate();
                            var b4 = "RealRuins.FactionOwnedStartTile.Cancel".Translate();
                            var actions = new string[] { b1, b2, b3, b4 };
                            SmallQuestionDialog dialog = new SmallQuestionDialog(title, text, actions, delegate (int selection)
                            {
                                PlanetaryRuinsInitData.shared.startingPOI = selectedObject;
                                switch (selection)
                                {
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
                                        break;
                                    case 3:
                                        PlanetaryRuinsInitData.shared.startingPOI = null;
                                        return;
                                    default:
                                        return;
                                }

                                if (selection != 3)
                                {
                                    // If did not cancel
                                    // store selected POI in game start context and remove it from the world map to not interfere with
                                    // regular map creation.
                                    Find.WorldObjects.Remove(selectedObject);
                                }

                                // when selection is done, call DoNext again, now everything is settled.
                                Debug.Log("StartGame", "Re-invoking original");

                                Type thisType = __instance.GetType();
                                MethodInfo doNext = thisType
                                    .GetMethod("DoNext", BindingFlags.NonPublic | BindingFlags.Instance);
                                doNext.Invoke(__instance, null);
                            });

                            Find.WindowStack.Add(dialog);
                            // and we DO NOT need to call original DoNext, we will call it later when everything is settled
                            Debug.Log("StartGame", "Skipping original");
                            return false;
                        }
                        else
                        {
                            Debug.Log("StartGame", "Got nil faction, settling in abandoned ruins");
                            PlanetaryRuinsInitData.shared.startingPOI = selectedObject;
                            PlanetaryRuinsInitData.shared.startingPOI.SetFaction(null);
                            PlanetaryRuinsInitData.shared.settleMode = SettleMode.normal;
                            Find.WorldObjects.Remove(selectedObject);
                        }
                    }
                }
                Debug.Log("StartGame", "Falling through and going to original");
                return true;
            }
        }

        // Old saves contain a RealRuins.MapRuinsStore world component that no longer exists; strip it so the load is clean and the next save omits it.
        [HarmonyPatch(typeof(World), "ExposeComponents")]
        class World_ExposeComponents_RemoveLegacyStore_Patch
        {
            static void Prefix()
            {
                if (Scribe.mode != LoadSaveMode.LoadingVars) return;

                var componentsNode = Scribe.loader?.curXmlParent?["components"];
                if (componentsNode == null) return;

                for (int i = componentsNode.ChildNodes.Count - 1; i >= 0; i--)
                {
                    var node = componentsNode.ChildNodes[i];
                    if (node.Attributes?["Class"]?.Value == "RealRuins.MapRuinsStore")
                    {
                        componentsNode.RemoveChild(node);
                    }
                }
            }
        }

        // This patch saves and sends blueprint when the game is saved
        [HarmonyPatch(typeof(GameDataSaveLoader), "SaveGame", typeof(string))]
        class SaveGame_Patch
        {
            static void Postfix()
            {
                SnapshotSaver.SaveSnapshot();
            }
        }

        // This patch checks if there are missing blueprints for existing planetary ruins and loads missing files automatically
        [HarmonyPatch(typeof(Verse.Game), "LoadGame")]
        class LoadGame_Patch
        {
            static void Postfix()
            {
                List<string> missingBlueprintNames = new List<string>();
                foreach (WorldObject obj in Find.WorldObjects.AllWorldObjects)
                {
                    if (obj is RealRuinsPOIWorldObject)
                    {
                        RealRuinsPOIComp comp = obj.GetComponent<RealRuinsPOIComp>();
                        if (comp != null)
                        {
                            string filename = comp.blueprintName;
                            if (!SnapshotStoreManager.HasPlanetaryBlueprintForCurrentGame(filename))
                            {
                                missingBlueprintNames.Add(filename);
                            }
                        }
                    }
                }

                if (missingBlueprintNames.Count() > 0)
                {
                    SnapshotManager.Instance.AggressiveLoadSnaphotsFromList(missingBlueprintNames, SnapshotStoreManager.CurrentGamePath());
                }
            }
        }

        // This patch saves the last save when game is over (game over dialog is shown only by some endings like the planetkiller)
        [HarmonyPatch(typeof(GenGameEnd), "EndGameDialogMessage", typeof(string), typeof(bool), typeof(Color))]
        class GameOver_Patch
        {
            static void Postfix()
            {
                SnapshotSaver.SaveGameOverSnapshot();
            }
        }

        // "Everyone is dead" game over is announced with a letter when the countdown reaches zero, without the dialog above
        [HarmonyPatch(typeof(GameEnder), "GameEndTick")]
        class GameEndTick_Patch
        {
            static void Postfix(bool ___gameEnding, int ___ticksToGameOver)
            {
                if (___gameEnding && ___ticksToGameOver == 0)
                {
                    SnapshotSaver.SaveGameOverSnapshot();
                }
            }
        }

        // Ruins raids and starting parties can come from non-hostile factions (they fight each other for the loot). Their steal duty
        // ignores ownership, so they would carry away the player's gravship parts and cargo. Hostile thieves are left vanilla.
        [HarmonyPatch(typeof(StealAIUtility), nameof(StealAIUtility.TryFindBestItemToSteal))]
        static class StealAIUtility_TryFindBestItemToSteal_Patch
        {
            private static bool searching = false;

            static void Postfix(IntVec3 root, Map map, float maxDist, ref Thing item, Pawn thief, List<Thing> disallowed, ref bool __result)
            {
                if (searching || !__result || !IsPlayerPropertyForNonHostile(item, thief)) return;

                //the best candidate is off limits: search again without it
                searching = true;
                try
                {
                    List<Thing> excluded = disallowed != null ? new List<Thing>(disallowed) : new List<Thing>();
                    for (int attempt = 0; attempt < 10 && __result && IsPlayerPropertyForNonHostile(item, thief); attempt++)
                    {
                        excluded.Add(item);
                        __result = StealAIUtility.TryFindBestItemToSteal(root, map, maxDist, out item, thief, excluded);
                    }
                    if (__result && IsPlayerPropertyForNonHostile(item, thief))
                    {
                        item = null;
                        __result = false;
                    }
                }
                finally
                {
                    searching = false;
                }
            }

            private static bool IsPlayerPropertyForNonHostile(Thing thing, Pawn thief)
            {
                if (thing == null || thief?.Faction == null || thief.Faction.IsPlayer || thief.HostileTo(Faction.OfPlayer)) return false;
                if (thing.Faction == Faction.OfPlayer) return true;
                return thing.Spawned && (thing.Map.terrainGrid.FoundationAt(thing.Position)?.IsSubstructure ?? false); //gravship cargo
            }
        }

        // This patch adds baked tale description to the art object when saving it
        [HarmonyPatch(typeof(TaleReference), "ExposeData")]
        class TaleReference_ExposeData_Patch
        {
            static void Postfix(TaleReference __instance)
            {
                if (__instance is BakedTaleReference reference)
                {
                    Scribe_Values.Look(ref reference.bakedTale, "bakedTale", "Default Baked Tale Whatever", false);
                }
            }
        }

        // Real ruins replace vanilla surface ruins unless "preserve standard ruins" is on.
        // Other ScatterRuinsSimple gensteps (e.g. Underground_ScatterRuinsSimple on cave maps) are left alone.
        [HarmonyPatch(typeof(GenStep_ScatterRuinsSimple), "ScatterAt", typeof(IntVec3), typeof(Map), typeof(GenStepParams), typeof(int))]
        class GenStep_ScatterRuinsSimple_ScatterAt_Patch
        {
            static bool Prefix(GenStep_ScatterRuinsSimple __instance)
            {
                if (RealRuins_ModSettings.preserveStandardRuins) return true;
                return __instance.def?.defName != "ScatterRuinsSimple";
            }
        }

        // This patch alters map generation if you're settling on top of existing ruins during game start
        [HarmonyPatch(typeof(MapGenerator), "GenerateMap")]
        class MapGenerator_GenerateMap_Patch
        {
            static void Prefix(ref IntVec3 mapSize, ref MapParent parent, ref MapGeneratorDef mapGenerator, ref IEnumerable<GenStepWithParams> extraGenStepDefs, ref Action<Map> extraInitBeforeContentGen)
            {
                int tile = parent.Tile;
                Debug.Log("StartGame", "Starting tile: {0}", tile);

                // check if there is a starting poi in global context to start at
                RealRuinsPOIWorldObject poi = PlanetaryRuinsInitData.shared.startingPOI;

                // The context is static and survives leaving game setup for the main menu. Only the starting map of a new game
                // may use it, otherwise the next map of a loaded game (e.g. an attacked enemy base) would become the starting POI.
                if (poi != null && Current.ProgramState == ProgramState.Playing)
                {
                    Debug.Log("[MapGen]", "Discarding stale RR POI game start context, game is already running");
                    PlanetaryRuinsInitData.shared.Cleanup();
                    poi = null;
                }

                if (poi != null)
                {
                    Debug.Log("[MapGen]", "Found RR POI in game start context. Generator def name was {0}, changing to {1}", mapGenerator.defName, poi.MapGeneratorDef.defName);
                    mapGenerator = poi.MapGeneratorDef;
                    extraGenStepDefs = extraGenStepDefs.Concat(poi.ExtraGenStepDefs);
                }
                else
                {
                    Debug.Log("[MapGen]", "Game start context is empty, generating map as usual");
                }
            }
        }

        // This patch alters art text generation if baked story is saved along with the art object
        [HarmonyPatch(typeof(TaleReference), "GenerateText")]
        class TaleReference_GenerateText_Patch
        {
            static bool Prefix(TaleReference __instance, ref TaggedString __result)
            {
                if (!(__instance is BakedTaleReference reference)) return true;
                __result = new TaggedString(reference.bakedTale);
                return false;
            }
        }

        //Manual caravan reform mode: no reforming on a pristine ruins map until the raids are over. Vanilla turns ExitMapGrid on
        //whenever reforming is not possible, so the player can still leave by walking off the map edge with what pawns carry.
        [HarmonyPatch(typeof(FormCaravanComp), nameof(FormCaravanComp.CanFormOrReformCaravanNow), MethodType.Getter)]
        class FormCaravanComp_CanFormOrReform_Patch
        {
            static void Postfix(FormCaravanComp __instance, ref bool __result)
            {
                if (__result && RealRuins_ModSettings.caravanReformType == 2 && (__instance.parent.GetComponent<RuinedBaseComp>()?.mapExitLocked ?? false))
                {
                    __result = false;
                }
            }
        }

        //RimWorld caravan forming dialog has two modes: reform and form.
        // * "Reform" shows all items including items which are currently in colonists' inventories, BUT reformed caravan leaves instantly.
        // * "Form" makes colonists go through the map and pick up all items, but it shows only items which are stored in home area.
        //On a pristine ruins map the reform button (FormCaravanFromRuinsComp) opens the reform dialog, and in automatic and manual modes
        //this patch sends it as a regular forming caravan instead: the chosen pawns haul the loot and leave via the map edge.
        //In automatic mode this works under fire on purpose: caravaners haul in the middle of a fight while the others defend.
        //Neither the dialog nor the forming lord (LordJob_FormAndSendCaravan) checks for hostiles, the same as forming at home during a raid.
        [HarmonyPatch(typeof(Dialog_FormCaravan), "TryReformCaravan")]
        class DialogFormCaravan_Patch
        {
            private static readonly AccessTools.FieldRef<Dialog_FormCaravan, Map> mapRef =
                AccessTools.FieldRefAccess<Dialog_FormCaravan, Map>("map");
            private static readonly AccessTools.FieldRef<Dialog_FormCaravan, PlanetTile> destinationTileRef =
                AccessTools.FieldRefAccess<Dialog_FormCaravan, PlanetTile>("destinationTile");
            private static readonly Func<Dialog_FormCaravan, bool> tryFormAndSendCaravan =
                AccessTools.MethodDelegate<Func<Dialog_FormCaravan, bool>>(AccessTools.Method(typeof(Dialog_FormCaravan), "TryFormAndSendCaravan"));
            private static readonly Action<Dialog_FormCaravan> flashMass =
                AccessTools.MethodDelegate<Action<Dialog_FormCaravan>>(AccessTools.Method(typeof(Dialog_FormCaravan), "FlashMass"));

            static bool Prefix(Dialog_FormCaravan __instance, ref bool __result)
            {
                if (RealRuins_ModSettings.caravanReformType == 1)
                {
                    return true; //instant reforming, vanilla (the button is disabled while hostiles are around)
                }

                Map map = mapRef(__instance);
                if (!(map?.Parent is AbandonedBaseWorldObject))
                {
                    return true;
                }

                __result = false;
                PlanetTile destinationTile = destinationTileRef(__instance);
                if (!destinationTile.Valid)
                {
                    Messages.Message("MessageMustChooseRouteFirst".Translate(), MessageTypeDefOf.RejectInput, historical: false);
                    return false;
                }

                //the reform dialog skips the mass check (reformed caravans get everything put into inventories), forming must not
                if (__instance.MassUsage > __instance.MassCapacity)
                {
                    flashMass(__instance);
                    Messages.Message("TooBigCaravanMassUsage".Translate(), MessageTypeDefOf.RejectInput, historical: false);
                    return false;
                }

                __result = tryFormAndSendCaravan(__instance); //the dialog closes itself on success
                return false;
            }
        }

    }

    public static class Art_Extensions
    {

        public static void InitializeArt(this CompArt art, string author, string title, string bakedTaleData)
        {
            var prop = art.GetType().GetField("taleRef", System.Reflection.BindingFlags.NonPublic
                                                  | System.Reflection.BindingFlags.Instance);
            prop.SetValue(art, new BakedTaleReference(bakedTaleData));

            var titleProp = art.GetType().GetField("titleInt", System.Reflection.BindingFlags.NonPublic
                                                               | System.Reflection.BindingFlags.Instance);
            titleProp.SetValue(art, new TaggedString(title));

            var authProp = art.GetType().GetField("authorNameInt", System.Reflection.BindingFlags.NonPublic
                                                               | System.Reflection.BindingFlags.Instance);
            authProp.SetValue(art, new TaggedString(author));

        }
    }
}