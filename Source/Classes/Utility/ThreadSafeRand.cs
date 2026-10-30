using System;

using Verse;

namespace RealRuins {
    // Verse.Rand is not thread-safe: calling it off the main thread corrupts the game's RNG state.
    // Blueprint loading and analysis can run on a background thread, so they use this instead.
    static class ThreadSafeRand {
        [ThreadStatic]
        private static Random threadRandom;

        private static Random ThreadRandom => threadRandom ?? (threadRandom = new Random(Guid.NewGuid().GetHashCode()));

        public static bool Chance(float chance) {
            if (UnityData.IsInMainThread) return Rand.Chance(chance);
            return ThreadRandom.NextDouble() < chance;
        }

        // max is exclusive, same as Rand.Range(int, int)
        public static int Range(int min, int max) {
            if (UnityData.IsInMainThread) return Rand.Range(min, max);
            if (max <= min) return min;
            return ThreadRandom.Next(min, max);
        }
    }
}
