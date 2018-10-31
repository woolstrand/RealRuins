using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Verse;

namespace RealRuins {
    class RealRuins_ModSettings : ModSettings {
        public static bool offlineMode = false;
        public static bool allowDownloads = true;
        public static bool allowUploads = true;
        public static float diskCacheLimit = 256.0f; //256mb cache by defaut, it's about 200 to 1000 blueprints in average.

        public override void ExposeData() {
            base.ExposeData();
            Scribe_Values.Look(ref offlineMode, "offlineMode", false, false);
            Scribe_Values.Look(ref allowDownloads, "allowDownloads", true, false);
            Scribe_Values.Look(ref allowUploads, "allowUploads", true, false);
            Scribe_Values.Look(ref diskCacheLimit, "diskCacheLimit", 256.0f, false);
        }
    }
}
