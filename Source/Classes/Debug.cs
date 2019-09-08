using System;
using Verse;

namespace RealRuins {
    class Debug {
        public static bool active = true;
        public static int logLevel = 0; //0: all, 1: warnings, 2: errors

        public static void Log(string format, params object[] args) {
            if (logLevel == 0) {
                Message(format, args);
            }
        }

        public static void Warning(string format, params object[] args) {
            if (logLevel < 2) {
                Message(format, args);
            }
        }

        public static void Error(string format, params object[] args) {
            if (logLevel < 3) {
                Message(format, args);
            }
        }


        private static void Message(string format, params object[] args) {
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
            Verse.Log.Message(message, true);
        }

        public static void PrintIntMap(int[,] map, string charMap = "#._23456789ABCDEFGHIJKLMNOPQRSTU", int delta = 0) {

            if (!active) return;

            string output = "============== INT MAP ============= \r\n";

            for (int i = 0; i < map.GetLength(0); i++) {
                for (int j = 0; j < map.GetLength(1); j++) {
                    int val = map[i, j] + delta;
                    if (val < 0 || val >= charMap.Length) {
                        output += '!';
                    } else {
                        char character = charMap[val];
                        output += character;
                    }
                }
                output += "\r\n";
            }

            Verse.Log.Message(output, true);
        }

        public static void PrintBoolMap(bool[,] map) {

            if (!active) return;

            string output = "============== BOOL MAP ============= \r\n";

            for (int i = 0; i < map.GetLength(0); i++) {
                for (int j = 0; j < map.GetLength(1); j++) {
                    if (map[i, j]) output += "#";
                    else output += " ";
                }
                output += "\r\n";
            }

            Verse.Log.Message(output, true);
        }

        public static void PrintNormalizedFloatMap(float[,] map, string charMap = " .,oO8") {

            if (!active) return;

            int scaleLength = charMap.Length;
            
            string output = "============== FLOAT MAP ============= \r\n";

            for (int i = 0; i < map.GetLength(0); i++) {
                for (int j = 0; j < map.GetLength(1); j++) {
                    float val = map[i, j];
                    int index = (int)Math.Round(val * scaleLength);
                    if (index >= charMap.Length) {
                        index = charMap.Length - 1;
                    }
                    char character = charMap[index];
                    output += character;
                }
                output += "\r\n";
            }

            Verse.Log.Message(output, true);
        }

        public static void PrintArray(object[] list) {
            var s = "";
            foreach (object o in list) {
                s += o.ToString();
                s += "\r\n";
            }
            Verse.Log.Message(s);
        }
    }
}
