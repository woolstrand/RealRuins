using System;
using System.Collections.Generic;
using System.Globalization;
using Verse;

namespace RealRuins.Settings
{
    /// <summary>
    /// A partial set of settings, in the export string format (see SettingsSerializer). Applying it changes only the keys it lists.
    /// </summary>
    public class SettingsPreset
    {
        public readonly string labelKey;
        public readonly Dictionary<string, string> values;

        public SettingsPreset(string labelKey, Dictionary<string, string> values)
        {
            this.labelKey = labelKey;
            this.values = values;
        }

        public string Label => labelKey.Translate();
        public string Tooltip => (labelKey + "TT").Translate();

        public void Apply()
        {
            foreach (KeyValuePair<string, string> pair in values)
            {
                SettingsSerializer.ApplyValue(pair.Key, pair.Value);
            }
        }

        public bool Matches(Dictionary<string, string> current)
        {
            foreach (KeyValuePair<string, string> pair in values)
            {
                if (!current.TryGetValue(pair.Key, out string value) || !SameValue(value, pair.Value)) return false;
            }
            return true;
        }

        // Floats are exported with "R", so "5000" and "5000.0" must match; bools are case insensitive
        private static bool SameValue(string a, string b)
        {
            if (float.TryParse(a, NumberStyles.Float, CultureInfo.InvariantCulture, out float fa) &&
                float.TryParse(b, NumberStyles.Float, CultureInfo.InvariantCulture, out float fb))
            {
                return Math.Abs(fa - fb) <= Math.Max(Math.Abs(fa), Math.Abs(fb)) * 1e-5f;
            }
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Play style and tech level presets. The two rows set disjoint keys, so the order you pick them in doesn't matter:
    /// tech level presets set only TechKeys, play style presets never do. No preset touches cache, network, debug or the
    /// item blacklist.
    /// </summary>
    public static class SettingsPresets
    {
        // Reserved for tech level presets. The material blacklist and fallback material are reserved too, for when the
        // tech presets get material lists.
        public static readonly HashSet<string> TechKeys = new HashSet<string> {
            "techLimitEnabled", "techLimitMaxLevel", "techLimitStrict", "techLimitRuins", "techLimitPOIs", "techLimitEvents",
            "materialBlacklist", "fallbackMaterial"
        };

        // Never touched by any preset: they are about the player's machine or own lists, not about the way to play
        public static readonly HashSet<string> PersonalKeys = new HashSet<string> {
            "offlineMode", "allowDownloads", "allowUploads", "diskCacheLimit", "logLevel", "spawnBlacklist"
        };

        // The defaults of RealRuins_ModSettings, ScatterOptions and PlanetaryRuinsOptions. Every play style starts from them and
        // overrides some, so all play styles set the same keys and switching between them leaves nothing of the previous one.
        private static readonly Dictionary<string, string> DefaultPlayStyle = new Dictionary<string, string> {
            { "caravanReformType", "0" },
            { "startWithoutRuins", "False" },
            { "preserveStandardRuins", "False" },
            { "forceMultiplier", "1" },
            { "ruinsCostCap", "1E+09" },
            { "ruinsWealthPerMap", "5000" },
            { "ruinsWealthScaleWithProximity", "False" },
            { "ruinsWealthIgnoreStructure", "True" },
            { "ruinsWealthCountDamage", "False" },
            { "useRuinsForcesGenerationV2", "True" },
            { "disableFriendlyRaids", "False" },
            { "spawnRoofs", "True" },
            { "enableAbandonedRuinsFoundEvent", "True" },
            { "enableCaravanFoundRuinsEvent", "True" },
            { "caravanRuinsCostCap", "25000" },
            { "ruinsScaleWithColonyWealth", "False" },
            { "ruinsReferenceColonyWealth", "100000" },
            { "poiRuinsCostCap", "15000" },
            { "so.densityMultiplier", "1" },
            { "so.minRadius", "8" },
            { "so.maxRadius", "16" },
            { "so.deteriorationMultiplier", "0" },
            { "so.scavengingMultiplier", "1" },
            { "so.itemCostLimit", "1000" },
            { "so.disableSpawnItems", "False" },
            { "so.wallsDoorsOnly", "False" },
            { "so.enableProximity", "True" },
            { "so.decorationChance", "0.0001" },
            { "so.trapChance", "0.001" },
            { "so.hostileChance", "0.1" },
            { "po.allowOnStart", "True" },
            { "po.downloadLimit", "0" },
            { "po.transferLimit", "0" },
            { "po.excludePlainRuins", "False" },
            { "po.abandonedLocations", "20" },
            { "po.disableSpawnFriendlyLocations", "False" },
        };

        public static readonly List<SettingsPreset> PlayStyles = new List<SettingsPreset> {
            new SettingsPreset("RealRuins.Presets.Default", PlayStyle()),

            // Living off the land: more and richer ruins, less picked over, caravan finds worth the trip, more abandoned locations
            new SettingsPreset("RealRuins.Presets.Nomadic", PlayStyle(
                ("ruinsWealthPerMap", "15000"),
                ("caravanRuinsCostCap", "50000"),
                ("poiRuinsCostCap", "40000"),
                ("so.densityMultiplier", "2"),
                ("so.maxRadius", "24"),
                ("so.scavengingMultiplier", "0.5"),
                ("so.hostileChance", "0.2"),
                ("po.abandonedLocations", "50"))),

            // Strict caps scaled with the colony: ruins help, but never hand over what the colony should make itself
            new SettingsPreset("RealRuins.Presets.Balanced", PlayStyle(
                ("ruinsWealthPerMap", "3000"),
                ("caravanRuinsCostCap", "10000"),
                ("ruinsCostCap", "50000"),
                ("poiRuinsCostCap", "8000"),
                ("so.itemCostLimit", "300"),
                ("ruinsScaleWithColonyWealth", "True"),
                ("so.deteriorationMultiplier", "0.1"),
                ("so.scavengingMultiplier", "1.5"))),

            // Lots of bigger regular ruins everywhere, with a moderate wealth limit
            new SettingsPreset("RealRuins.Presets.Dense", PlayStyle(
                ("ruinsWealthPerMap", "10000"),
                ("so.densityMultiplier", "4"),
                ("so.minRadius", "12"),
                ("so.maxRadius", "32"),
                ("so.enableProximity", "False"),
                ("so.itemCostLimit", "500"))),
        };

        private static Dictionary<string, string> PlayStyle(params (string key, string value)[] overrides)
        {
            Dictionary<string, string> values = new Dictionary<string, string>(DefaultPlayStyle);
            foreach (var (key, value) in overrides)
            {
                if (!values.ContainsKey(key)) Log.Error("[RealRuins] play style override of unknown key " + key);
                values[key] = value;
            }
            return values;
        }

        public static readonly List<SettingsPreset> TechLevels = new List<SettingsPreset> {
            new SettingsPreset("RealRuins.Presets.TechNormal", new Dictionary<string, string> {
                { "techLimitEnabled", "False" },
            }),
            new SettingsPreset("RealRuins.Presets.TechNeolithic", TechCap("Neolithic", strict: true)),
            new SettingsPreset("RealRuins.Presets.TechMedieval", TechCap("Medieval", strict: true)),
            new SettingsPreset("RealRuins.Presets.TechPostApocalyptic", TechCap("Medieval", strict: false)),
        };

        private static Dictionary<string, string> TechCap(string level, bool strict)
        {
            return new Dictionary<string, string> {
                { "techLimitEnabled", "True" },
                { "techLimitMaxLevel", level },
                { "techLimitStrict", strict.ToString() },
                { "techLimitRuins", "True" },
                { "techLimitPOIs", "True" },
                { "techLimitEvents", "True" },
            };
        }

        public static Dictionary<string, string> CurrentValues()
        {
            Dictionary<string, string> current = new Dictionary<string, string>();
            foreach (KeyValuePair<string, string> pair in SettingsSerializer.CurrentValues())
            {
                current[pair.Key] = pair.Value;
            }
            return current;
        }

        /// <summary>Every key set by any preset of the list.</summary>
        public static HashSet<string> KeysOf(List<SettingsPreset> presets)
        {
            HashSet<string> keys = new HashSet<string>();
            foreach (SettingsPreset preset in presets)
            {
                keys.UnionWith(preset.values.Keys);
            }
            return keys;
        }

        /// <summary>The given keys of the current settings, as "key=value" lines. Values are numbers, booleans or enum names.</summary>
        public static string Pack(Dictionary<string, string> current, HashSet<string> keys)
        {
            List<string> lines = new List<string>();
            foreach (string key in keys)
            {
                if (current.TryGetValue(key, out string value)) lines.Add(key + "=" + value);
            }
            lines.Sort(StringComparer.Ordinal);
            return string.Join("\n", lines.ToArray());
        }

        public static Dictionary<string, string> Unpack(string packed)
        {
            Dictionary<string, string> values = new Dictionary<string, string>();
            if (packed.NullOrEmpty()) return values;
            foreach (string line in packed.Split('\n'))
            {
                int eq = line.IndexOf('=');
                if (eq > 0) values[line.Substring(0, eq)] = line.Substring(eq + 1);
            }
            return values;
        }

        /// <summary>The first preset of the list that matches the current settings, or null.</summary>
        public static SettingsPreset Matching(List<SettingsPreset> presets, Dictionary<string, string> current)
        {
            foreach (SettingsPreset preset in presets)
            {
                if (preset.Matches(current)) return preset;
            }
            return null;
        }

        /// <summary>Logs every preset that breaks the key rules above. Called from dev mode only.</summary>
        public static void Validate()
        {
            foreach (SettingsPreset preset in PlayStyles)
            {
                foreach (string key in preset.values.Keys)
                {
                    if (TechKeys.Contains(key) || PersonalKeys.Contains(key))
                        Log.Error("[RealRuins] play style preset " + preset.labelKey + " sets reserved key " + key);
                }
            }
            foreach (SettingsPreset preset in TechLevels)
            {
                foreach (string key in preset.values.Keys)
                {
                    if (!TechKeys.Contains(key))
                        Log.Error("[RealRuins] tech level preset " + preset.labelKey + " sets non-tech key " + key);
                }
            }
        }
    }
}
