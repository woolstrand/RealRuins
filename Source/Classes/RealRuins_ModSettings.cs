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
        public static bool allowInstantCaravanReform = false;
        public static int caravanReformType = 0; //0 - regular, 1 - instant, 2 - manual
        public static bool startWithoutRuins = false;
        public static bool preserveStandardRuins = false; //preservers standard ruins and adds real ruinson top of that.
        public static float forceMultiplier = 1.0f;
        public static float ruinsCostCap = 1.0e+9f; //absolute cost cap of each and every ruin
        public static float diskCacheLimit = 256.0f; //256mb cache by default, it's about 2000 to 10000 blueprints in average.
        public static int logLevel = 2; //0 = all, 1 = warnings, 2 = errors

        public static ScatterOptions defaultScatterOptions = ScatterOptions.Default;

        public override void ExposeData() {
            base.ExposeData();
            Scribe_Values.Look(ref offlineMode, "offlineMode", false, false);
            Scribe_Values.Look(ref allowDownloads, "allowDownloads", true, false);
            Scribe_Values.Look(ref allowUploads, "allowUploads", true, false);
            Scribe_Values.Look(ref diskCacheLimit, "diskCacheLimit", 256.0f, false);
            Scribe_Values.Look(ref allowInstantCaravanReform, "allowInstantCaravanReform", false, false);
            Scribe_Values.Look(ref caravanReformType, "caravanReformType", 0, false);
            Scribe_Values.Look(ref preserveStandardRuins, "preserveStandardRuins", false, false);
            Scribe_Values.Look(ref forceMultiplier, "forceMultiplier", 1.0f, false);
            Scribe_Values.Look(ref ruinsCostCap, "ruinsCostCap", 1.0e+10f, false);
            Scribe_Values.Look(ref startWithoutRuins, "startWithoutRuins", false, false);
            Scribe_Values.Look(ref logLevel, "logLevel", 2, false);
            Scribe_Deep.Look(ref defaultScatterOptions, "defaultScatterOptions");

            if (allowInstantCaravanReform == true) {
                allowInstantCaravanReform = false; //migrate settings
                caravanReformType = 1;
            }
        }
    }
}
    