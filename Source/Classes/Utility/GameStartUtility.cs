using Verse;

namespace RealRuins {
    static class GameStartUtility {
        // True while the starting map of a new game is being generated. Game.InitData exists only during new game setup
        // (vanilla clears it at the end of Game.InitNewGame), so maps generated later in the game, like new settlements, get false.
        public static bool GeneratingStartingMap => Current.Game?.InitData != null;
    }
}
