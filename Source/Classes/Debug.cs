
using Verse;

namespace RealRuins {
    class Debug {
        public static bool active = true;
        public static void Message(string format, params object[] args) {
            if (!active) return;

            object[] safeArgs = new object[args.Length];
            for (int i = 0; i < args.Length; i++) {
                if (args[i] != null) {
                    safeArgs[i] = args[i];
                } else {
                    safeArgs[i] = "[NULL]";
                }
            }

            string message = string.Format(format, safeArgs);
            Log.Message(message, true);
        }
    }
}
