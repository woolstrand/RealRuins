
using Verse;

namespace RealRuins {
    class Debug {
        public static void Message(string format, params object[] args) {
            string message = string.Format(format, args);
            Log.Message(message, true);
        }
    }
}
