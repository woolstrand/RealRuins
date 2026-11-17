using HarmonyLib;
using RimWorld;
using Verse;

namespace RealRuins {
    // The one place where static state that belongs to a single game session ends its life. Statics outlive Game objects:
    // without this a game start context or dev site states would leak into the next game (loaded or new) or the main menu.
    // Owners of such statics expose a reset and are listed in Reset.
    static class SessionState {
        // True while Game.InitNewGame runs, i.e. while the starting map(s) of a new game are generated.
        // Maps generated later (new settlements, caravan encounters) get false.
        public static bool GeneratingStartingMap { get; private set; }

        private static void Reset() {
            PlanetaryRuinsInitData.Reset();
            DevSiteTools.Reset();
            DuplicateLoadIds.Reset();
        }

        // A loaded game never uses the start context of a game setup that was abandoned before it.
        [HarmonyPatch(typeof(Game), nameof(Game.LoadGame))]
        static class Game_LoadGame_Patch {
            static void Prefix() => Reset();
        }

        // InitNewGame consumes the start context (starting POI): nothing of it is valid once the game runs.
        [HarmonyPatch(typeof(Game), nameof(Game.InitNewGame))]
        static class Game_InitNewGame_Patch {
            static void Prefix() => GeneratingStartingMap = true;

            static void Finalizer() {
                GeneratingStartingMap = false;
                Reset();
            }
        }

        // A new game setup starts from the scenario page. Backing out of a setup to the main menu doesn't go through GoToMainMenu.
        [HarmonyPatch(typeof(Page_SelectScenario), MethodType.Constructor)]
        static class Page_SelectScenario_Ctor_Patch {
            static void Postfix() => Reset();
        }

        [HarmonyPatch(typeof(GenScene), nameof(GenScene.GoToMainMenu))]
        static class GenScene_GoToMainMenu_Patch {
            static void Prefix() => Reset();
        }
    }
}
