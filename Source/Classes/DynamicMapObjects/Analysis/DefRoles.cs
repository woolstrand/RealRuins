using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

using RimWorld;
using Verse;

namespace RealRuins {
    // What a thing is for, as far as POI classification cares. Taken from def data only (classes, comps, designation
    // category, stats), so modded things are recognized the same way as vanilla ones, without lists of def names.
    class DefRole {
        public bool isBuilding; //placed building, except doors, fences and power conduits (walls are told by ItemTile.isWall)
        public bool isStructure; //door, fence or conduit: says nothing about what the place is for
        public bool turret;
        public bool mannable;
        public bool military; //weapons, armor, shells, turrets, traps, cover: the "military items" of a base
        public bool researchBench;
        public bool researchFacility; //multi-analyzer and the like: boosts research benches
        public bool worktable;
        public bool bed;
        public float powerOutput; //watts, generators only
        public bool battery;
        public bool comms; //comms console, trade beacon, long range scanner: talks to the world beyond
        public TechLevel techLevel;
    }

    // Safe to call from a background thread (the analyzer runs there).
    static class DefRoles {
        // Apparel with at least this sharp armor (default stuff) counts as armor: flak vest, plate armor, not a parka
        public const float armorSharpThreshold = 0.4f;

        private static readonly ConcurrentDictionary<ThingDef, DefRole> roles = new ConcurrentDictionary<ThingDef, DefRole>();
        private static DesignationCategoryDef security;
        private static bool securityResolved;

        public static DefRole Of(ThingDef def) {
            return roles.GetOrAdd(def, Calculate);
        }

        private static DesignationCategoryDef Security {
            get {
                if (!securityResolved) {
                    //the vanilla category every defensive building goes to, mods put theirs there too
                    security = DefDatabase<DesignationCategoryDef>.GetNamedSilentFail("Security");
                    securityResolved = true;
                }
                return security;
            }
        }

        private static DefRole Calculate(ThingDef def) {
            DefRole role = new DefRole { techLevel = TechLevelFilter.TechLevelOf(def) };
            bool isBuilding = def.category == ThingCategory.Building;
            bool conduit = HasComp(def, typeof(CompPowerTransmitter));
            role.isStructure = isBuilding && (def.IsDoor || def.IsFence || conduit);
            role.isBuilding = isBuilding && !role.isStructure;

            if (isBuilding) {
                role.turret = def.building?.IsTurret ?? false;
                role.mannable = HasComp(def, typeof(CompMannable));
                //traps, sandbags, barricades, embrasures too
                role.military = role.turret || (def.building?.isTrap ?? false) || (Security != null && def.designationCategory == Security && !role.isStructure);

                role.researchBench = typeof(Building_ResearchBench).IsAssignableFrom(def.thingClass);
                role.researchFacility = IsResearchFacility(def);
                role.worktable = def.IsWorkTable;
                role.bed = def.IsBed;
                role.battery = HasComp(def, typeof(CompPowerBattery));
                role.powerOutput = PowerOutput(def);
                role.comms = typeof(Building_CommsConsole).IsAssignableFrom(def.thingClass)
                    || typeof(Building_OrbitalTradeBeacon).IsAssignableFrom(def.thingClass)
                    || HasComp(def, typeof(CompLongRangeMineralScanner));
            } else {
                role.military = def.equipmentType == EquipmentType.Primary || def.IsShell || (def.IsApparel && SharpArmor(def) >= armorSharpThreshold);
            }
            return role;
        }

        // ThingDef.HasComp matches the exact class only, mods subclass comps
        private static bool HasComp(ThingDef def, Type compType) {
            if (def.comps == null) return false;
            foreach (CompProperties props in def.comps) {
                if (props.compClass != null && compType.IsAssignableFrom(props.compClass)) return true;
            }
            return false;
        }

        private static bool IsResearchFacility(ThingDef def) {
            CompProperties_Facility facility = def.GetCompProperties<CompProperties_Facility>();
            if (facility == null) return false;
            if (facility.statOffsets != null) {
                foreach (StatModifier modifier in facility.statOffsets) {
                    if (modifier.stat == StatDefOf.ResearchSpeedFactor) return true;
                }
            }
            if (facility.linkableBuildings != null) {
                foreach (ThingDef linkable in facility.linkableBuildings) {
                    if (typeof(Building_ResearchBench).IsAssignableFrom(linkable.thingClass)) return true;
                }
            }
            return false;
        }

        private static float PowerOutput(ThingDef def) {
            CompProperties_Power power = def.GetCompProperties<CompProperties_Power>();
            if (power == null || power.compClass == null || !typeof(CompPowerPlant).IsAssignableFrom(power.compClass)) return 0;
            return Math.Max(0, -power.PowerConsumption);
        }

        private static float SharpArmor(ThingDef def) {
            try {
                return def.GetStatValueAbstract(StatDefOf.ArmorRating_Sharp, def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null);
            } catch (Exception) {
                return 0;
            }
        }

        private static readonly ConcurrentDictionary<string, float> wallStrengths = new ConcurrentDictionary<string, float>();

        // Wall hit points relative to a vanilla steel wall (300): wood ~0.65, granite ~1.7, plasteel ~2.8, capped at 3
        public static float WallStrength(ThingDef def, ThingDef stuff) {
            return wallStrengths.GetOrAdd(def.defName + "|" + (stuff?.defName ?? ""), key => {
                try {
                    return Math.Max(0.1f, Math.Min(3, def.GetStatValueAbstract(StatDefOf.MaxHitPoints, stuff) / 300f));
                } catch (Exception) {
                    return 1;
                }
            });
        }
    }
}
