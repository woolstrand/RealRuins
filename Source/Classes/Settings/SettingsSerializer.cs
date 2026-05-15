using System;
using System.Text;

namespace RealRuins.Settings
{
    /// <summary>
    /// Serializes and deserializes mod settings to/from a Base64-encoded string.
    /// Format: key=value pairs separated by | then Base64-encoded.
    /// </summary>
    public static class SettingsSerializer
    {
        public static string Export()
        {
            var sb = new StringBuilder();

            void Add(string key, string value)
            {
                if (sb.Length > 0) sb.Append('|');
                sb.Append(key).Append('=').Append(value);
            }

            // Flat settings
            Add("offlineMode",                      RealRuins_ModSettings.offlineMode.ToString());
            Add("allowDownloads",                   RealRuins_ModSettings.allowDownloads.ToString());
            Add("allowUploads",                     RealRuins_ModSettings.allowUploads.ToString());
            Add("caravanReformType",                RealRuins_ModSettings.caravanReformType.ToString());
            Add("startWithoutRuins",                RealRuins_ModSettings.startWithoutRuins.ToString());
            Add("preserveStandardRuins",            RealRuins_ModSettings.preserveStandardRuins.ToString());
            Add("forceMultiplier",                  RealRuins_ModSettings.forceMultiplier.ToString("R"));
            Add("ruinsCostCap",                     RealRuins_ModSettings.ruinsCostCap.ToString("R"));
            Add("diskCacheLimit",                   RealRuins_ModSettings.diskCacheLimit.ToString("R"));
            Add("useRuinsForcesGenerationV2",       RealRuins_ModSettings.useRuinsForcesGenerationV2.ToString());
            Add("logLevel",                         RealRuins_ModSettings.logLevel.ToString());
            Add("disableFriendlyRaids",             RealRuins_ModSettings.disableFriendlyRaids.ToString());
            Add("enableAbandonedRuinsFoundEvent",   RealRuins_ModSettings.enableAbandonedRuinsFoundEvent.ToString());
            Add("enableCaravanFoundRuinsEvent",     RealRuins_ModSettings.enableCaravanFoundRuinsEvent.ToString());
            Add("spawnBlacklist",                   Uri.EscapeDataString(RealRuins_ModSettings.spawnBlacklist ?? ""));
            Add("materialBlacklist",                Uri.EscapeDataString(RealRuins_ModSettings.materialBlacklist ?? ""));
            Add("fallbackMaterial",                 Uri.EscapeDataString(RealRuins_ModSettings.fallbackMaterial ?? "Wood"));

            // ScatterOptions
            var so = RealRuins_ModSettings.defaultScatterOptions;
            if (so != null)
            {
                Add("so.densityMultiplier",         so.densityMultiplier.ToString("R"));
                Add("so.minRadius",                 so.minRadius.ToString());
                Add("so.maxRadius",                 so.maxRadius.ToString());
                Add("so.deteriorationMultiplier",   so.deteriorationMultiplier.ToString("R"));
                Add("so.scavengingMultiplier",       so.scavengingMultiplier.ToString("R"));
                Add("so.itemCostLimit",              so.itemCostLimit.ToString());
                Add("so.disableSpawnItems",          so.disableSpawnItems.ToString());
                Add("so.wallsDoorsOnly",             so.wallsDoorsOnly.ToString());
                Add("so.claimableBlocks",            so.claimableBlocks.ToString());
                Add("so.enableProximity",            so.enableProximity.ToString());
                Add("so.decorationChance",           so.decorationChance.ToString("R"));
                Add("so.trapChance",                 so.trapChance.ToString("R"));
                Add("so.hostileChance",              so.hostileChance.ToString("R"));
            }

            // PlanetaryRuinsOptions
            var po = RealRuins_ModSettings.planetaryRuinsOptions;
            if (po != null)
            {
                Add("po.allowOnStart",              po.allowOnStart.ToString());
                Add("po.downloadLimit",             po.downloadLimit.ToString());
                Add("po.transferLimit",             po.transferLimit.ToString());
                Add("po.excludePlainRuins",         po.excludePlainRuins.ToString());
                Add("po.abandonedLocations",        po.abandonedLocations.ToString("R"));
                Add("po.disableSpawnFriendlyLocations", po.disableSpawnFriendlyLocations.ToString());
            }

            return Convert.ToBase64String(Encoding.UTF8.GetBytes(sb.ToString()));
        }

        public static bool TryImport(string encoded)
        {
            if (string.IsNullOrEmpty(encoded)) return false;
            try
            {
                string raw = Encoding.UTF8.GetString(Convert.FromBase64String(encoded.Trim()));
                string[] pairs = raw.Split('|');

                foreach (string pair in pairs)
                {
                    int sep = pair.IndexOf('=');
                    if (sep < 0) continue;
                    string key = pair.Substring(0, sep).Trim();
                    string val = pair.Substring(sep + 1).Trim();

                    switch (key)
                    {
                        case "offlineMode":                     TryParseBool(val, ref RealRuins_ModSettings.offlineMode); break;
                        case "allowDownloads":                  TryParseBool(val, ref RealRuins_ModSettings.allowDownloads); break;
                        case "allowUploads":                    TryParseBool(val, ref RealRuins_ModSettings.allowUploads); break;
                        case "caravanReformType":               TryParseInt(val, ref RealRuins_ModSettings.caravanReformType); break;
                        case "startWithoutRuins":               TryParseBool(val, ref RealRuins_ModSettings.startWithoutRuins); break;
                        case "preserveStandardRuins":           TryParseBool(val, ref RealRuins_ModSettings.preserveStandardRuins); break;
                        case "forceMultiplier":                 TryParseFloat(val, ref RealRuins_ModSettings.forceMultiplier); break;
                        case "ruinsCostCap":                    TryParseFloat(val, ref RealRuins_ModSettings.ruinsCostCap); break;
                        case "diskCacheLimit":                  TryParseFloat(val, ref RealRuins_ModSettings.diskCacheLimit); break;
                        case "useRuinsForcesGenerationV2":      TryParseBool(val, ref RealRuins_ModSettings.useRuinsForcesGenerationV2); break;
                        case "logLevel":                        TryParseInt(val, ref RealRuins_ModSettings.logLevel); break;
                        case "disableFriendlyRaids":            TryParseBool(val, ref RealRuins_ModSettings.disableFriendlyRaids); break;
                        case "enableAbandonedRuinsFoundEvent":  TryParseBool(val, ref RealRuins_ModSettings.enableAbandonedRuinsFoundEvent); break;
                        case "enableCaravanFoundRuinsEvent":    TryParseBool(val, ref RealRuins_ModSettings.enableCaravanFoundRuinsEvent); break;
                        case "spawnBlacklist":                  RealRuins_ModSettings.spawnBlacklist = Uri.UnescapeDataString(val); break;
                        case "materialBlacklist":               RealRuins_ModSettings.materialBlacklist = Uri.UnescapeDataString(val); break;
                        case "fallbackMaterial":                RealRuins_ModSettings.fallbackMaterial = Uri.UnescapeDataString(val); break;

                        case "so.densityMultiplier":        TryParseFloat(val, ref RealRuins_ModSettings.defaultScatterOptions.densityMultiplier); break;
                        case "so.minRadius":                TryParseInt(val, ref RealRuins_ModSettings.defaultScatterOptions.minRadius); break;
                        case "so.maxRadius":                TryParseInt(val, ref RealRuins_ModSettings.defaultScatterOptions.maxRadius); break;
                        case "so.deteriorationMultiplier":  TryParseFloat(val, ref RealRuins_ModSettings.defaultScatterOptions.deteriorationMultiplier); break;
                        case "so.scavengingMultiplier":     TryParseFloat(val, ref RealRuins_ModSettings.defaultScatterOptions.scavengingMultiplier); break;
                        case "so.itemCostLimit":            TryParseInt(val, ref RealRuins_ModSettings.defaultScatterOptions.itemCostLimit); break;
                        case "so.disableSpawnItems":        TryParseBool(val, ref RealRuins_ModSettings.defaultScatterOptions.disableSpawnItems); break;
                        case "so.wallsDoorsOnly":           TryParseBool(val, ref RealRuins_ModSettings.defaultScatterOptions.wallsDoorsOnly); break;
                        case "so.claimableBlocks":          TryParseBool(val, ref RealRuins_ModSettings.defaultScatterOptions.claimableBlocks); break;
                        case "so.enableProximity":          TryParseBool(val, ref RealRuins_ModSettings.defaultScatterOptions.enableProximity); break;
                        case "so.decorationChance":         TryParseFloat(val, ref RealRuins_ModSettings.defaultScatterOptions.decorationChance); break;
                        case "so.trapChance":               TryParseFloat(val, ref RealRuins_ModSettings.defaultScatterOptions.trapChance); break;
                        case "so.hostileChance":            TryParseFloat(val, ref RealRuins_ModSettings.defaultScatterOptions.hostileChance); break;

                        case "po.allowOnStart":             TryParseBool(val, ref RealRuins_ModSettings.planetaryRuinsOptions.allowOnStart); break;
                        case "po.downloadLimit":            TryParseInt(val, ref RealRuins_ModSettings.planetaryRuinsOptions.downloadLimit); break;
                        case "po.transferLimit":            TryParseInt(val, ref RealRuins_ModSettings.planetaryRuinsOptions.transferLimit); break;
                        case "po.excludePlainRuins":        TryParseBool(val, ref RealRuins_ModSettings.planetaryRuinsOptions.excludePlainRuins); break;
                        case "po.abandonedLocations":       TryParseFloat(val, ref RealRuins_ModSettings.planetaryRuinsOptions.abandonedLocations); break;
                        case "po.disableSpawnFriendlyLocations": TryParseBool(val, ref RealRuins_ModSettings.planetaryRuinsOptions.disableSpawnFriendlyLocations); break;
                    }
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void TryParseBool(string val, ref bool field)
        {
            if (bool.TryParse(val, out bool result)) field = result;
        }

        private static void TryParseInt(string val, ref int field)
        {
            if (int.TryParse(val, out int result)) field = result;
        }

        private static void TryParseFloat(string val, ref float field)
        {
            if (float.TryParse(val, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float result))
            {
                field = result;
            }
        }
    }
}
