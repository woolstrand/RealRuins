using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Verse;

namespace RealRuins
{
    class RealRuins_ModSettings : ModSettings
    {
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
        public static bool useRuinsForcesGenerationV2 = true; // support fixed amount of troops on ruins maps
        public static int logLevel = 2; //0 = all, 1 = warnings, 2 = errors
        public static bool debugKeepSnapshotsAfterUpload = false; // debug: keep snapshot files after upload instead of deleting
        public static int debugTileFocusId = -1; // debug: tile ID to focus/select in world view
        public static List<string> debugExtras = new List<string>() { "BlueprintPawnDecoder" }; // debug: extra debug categories to log
        public static bool disableFriendlyRaids = false; // disable friendly raids on ruins
        public static bool enableAbandonedRuinsFoundEvent = true; // enable/disable the abandoned ruins found event
        public static bool enableCaravanFoundRuinsEvent = true; // enable/disable the caravan spotted small ruins event
        public static string spawnBlacklist = ""; // newline-separated list of defs to exclude from spawning
        public static string materialBlacklist = ""; // newline-separated list of materials to exclude from spawning
        public static string fallbackMaterial = "Wood"; // material to use when blacklisted material is encountered

        public static ScatterOptions defaultScatterOptions = ScatterOptions.Default;
        public static PlanetaryRuinsOptions planetaryRuinsOptions = new PlanetaryRuinsOptions();

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref offlineMode, "offlineMode", false, false);
            Scribe_Values.Look(ref allowDownloads, "allowDownloads", true, false);
            Scribe_Values.Look(ref allowUploads, "allowUploads", true, false);
            Scribe_Values.Look(ref diskCacheLimit, "diskCacheLimit", 256.0f, false);
            Scribe_Values.Look(ref allowInstantCaravanReform, "allowInstantCaravanReform", false, false);
            Scribe_Values.Look(ref caravanReformType, "caravanReformType", 0, false);
            Scribe_Values.Look(ref preserveStandardRuins, "preserveStandardRuins", false, false);
            Scribe_Values.Look(ref forceMultiplier, "forceMultiplier", 1.0f, false);
            Scribe_Values.Look(ref ruinsCostCap, "ruinsCostCap", 1.0e+9f, false);
            Scribe_Values.Look(ref startWithoutRuins, "startWithoutRuins", false, false);
            Scribe_Values.Look(ref useRuinsForcesGenerationV2, "useRuinsForcesGenerationV2", true, true);
            Scribe_Values.Look(ref logLevel, "logLevel", 2, false);
            Scribe_Values.Look(ref debugKeepSnapshotsAfterUpload, "debugKeepSnapshotsAfterUpload", false, false);
            Scribe_Values.Look(ref debugTileFocusId, "debugTileFocusId", -1, false);
            Scribe_Collections.Look(ref debugExtras, "debugExtras", LookMode.Value);
            Scribe_Values.Look(ref disableFriendlyRaids, "disableFriendlyRaids", false, false);
            Scribe_Values.Look(ref enableAbandonedRuinsFoundEvent, "enableAbandonedRuinsFoundEvent", true, false);
            Scribe_Values.Look(ref enableCaravanFoundRuinsEvent, "enableCaravanFoundRuinsEvent", true, false);
            Scribe_Values.Look(ref spawnBlacklist, "spawnBlacklist", "", false);
            Scribe_Values.Look(ref materialBlacklist, "materialBlacklist", "", false);
            Scribe_Values.Look(ref fallbackMaterial, "fallbackMaterial", "Wood", false);
            Scribe_Deep.Look(ref defaultScatterOptions, "defaultScatterOptions");
            Scribe_Deep.Look(ref planetaryRuinsOptions, "planetaryRuinsOptions");
        
            if (Scribe.mode == LoadSaveMode.LoadingVars) 
            {
                if (debugExtras == null)
                {
                    debugExtras = new List<string>() { "BlueprintPawnDecoder" };
                }
                if (defaultScatterOptions == null)
                {
                    defaultScatterOptions = ScatterOptions.Default;
                }
                if (planetaryRuinsOptions == null)
                {
                    planetaryRuinsOptions = new PlanetaryRuinsOptions();
                }
            }
        }

        public static void Reset()
        {
            defaultScatterOptions = new ScatterOptions();
            planetaryRuinsOptions = new PlanetaryRuinsOptions();

            offlineMode = false;
            allowDownloads = true;
            allowUploads = true;
            allowInstantCaravanReform = false;
            caravanReformType = 0;
            startWithoutRuins = false;
            preserveStandardRuins = false;
            forceMultiplier = 1.0f;
            ruinsCostCap = 1.0e+9f;
            logLevel = 2;
            debugKeepSnapshotsAfterUpload = false;
            debugTileFocusId = -1;
            debugExtras = new List<string>() { "BlueprintPawnDecoder" };
            disableFriendlyRaids = false;
            enableAbandonedRuinsFoundEvent = true;
            enableCaravanFoundRuinsEvent = true;
            spawnBlacklist = "";
            materialBlacklist = "";
            fallbackMaterial = "Wood";
        }
    }
}
