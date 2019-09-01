using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Verse;

namespace RealRuins {
    class ScatterOptions : IExposable {
        // Those options are accessible from settings and are taken from settings for regular ruins generation. 
        // For special event ruins generation those settings are overrided
        // Those options should be saved in Scribe subsystem to keep a record of a default template object which is represented on the settings page
        public float densityMultiplier = 1.0f;
        public int minRadius = 8;
        public int maxRadius = 16;
        public float deteriorationMultiplier = 0.0f;
        public float scavengingMultiplier = 1.0f;

        public int itemCostLimit = 1000;
        public bool disableSpawnItems = false;
        public bool wallsDoorsOnly = false;
        public bool claimableBlocks = true;
        public bool enableProximity = true;

        public float decorationChance = 0.0001f; //probability PER CELL
        public float trapChance = 0.001f; //probability PER CELL
        public float hostileChance = 0.1f; //probability PER CHUNK

        // Those settings are either internal settings not accessible for user, or internal blueprint trasferring/processing state representation

        public float uncoveredCost = 0; //Cost uncovered by any raid triggers
        public int[,] roomMap; // Room traversal map.
        public IntVec3 bottomLeft = new IntVec3(-1000,0,-1000);
        public CellRect blueprintRect = new CellRect();

        public int minimumAreaRequired = 0;
        public float minimumDensityRequired = 0.1f;
        public int minimumCostRequired = 0;
        public int costCap = -1;
        public int startingPartyPoints = 0;
        public bool shouldKeepDefencesAndPower = false;
        public bool shouldLoadPartOnly = true; //indicates if the loader should load only a part of the blueprint or the whole one
        public bool shouldAddRaidTriggers = false;
        public bool enableInstantCaravanReform = false; //only for large events
        public bool allowFriendlyRaids = true; // friendly factions hostile to environment, but friendly to you
        public bool enableDeterioration = true; //enables BLOCK REMOVING due to deterioration. HP control is the next option
        public bool forceFullHitPoints = false; //forces all HP to be maxed
        public bool canHaveFood = true;
        public bool shouldAddFilth = true;
        public IntVec3 overridePosition = IntVec3.Zero;
        public bool overwritesEverything = false; //if true, each item, terrain and even empty cell inside a room removes everything from that tile
        public bool centerIfExceedsBounds = false;
        public string blueprintFileName = null;
  

        
        public bool deleteLowQuality = true;

        public static readonly ScatterOptions Default = new ScatterOptions();


        public void ExposeData() {
            //Regex: public [a-z]* ([a-zA-Z]*) = [^;]*;     =>     Scribe_Values.Look(ref $1, "$1", 0.0f, false);

            Scribe_Values.Look(ref densityMultiplier, "densityMultiplier", 1.0f, false);
            Scribe_Values.Look(ref minRadius, "minRadius", 8, false);
            Scribe_Values.Look(ref maxRadius, "maxRadius", 16, false);
            Scribe_Values.Look(ref deteriorationMultiplier, "deteriorationMultiplier", 0.0f, false);
            Scribe_Values.Look(ref scavengingMultiplier, "scavengingMultiplier", 1.0f, false);

            Scribe_Values.Look(ref itemCostLimit, "itemCostLimit", 1000, false);
            Scribe_Values.Look(ref disableSpawnItems, "disableSpawnItems", false, false);
            Scribe_Values.Look(ref wallsDoorsOnly, "wallsDoorsOnly", false, false);
            Scribe_Values.Look(ref claimableBlocks, "claimableBlocks", true, false);
            Scribe_Values.Look(ref enableProximity, "enableProximity", true, false);

            Scribe_Values.Look(ref decorationChance, "decorationChance", 0.0001f, false);
            Scribe_Values.Look(ref trapChance, "trapChance", 0.001f, false);
            Scribe_Values.Look(ref hostileChance, "hostileChance", 0.1f, false);
            Scribe_Values.Look(ref enableInstantCaravanReform, "enableInstantCaravanReform", false, false);

        }

        public ScatterOptions Copy() {
            ScatterOptions copy = new ScatterOptions {
                deteriorationMultiplier = deteriorationMultiplier,
                claimableBlocks = claimableBlocks,
                decorationChance = decorationChance,
                densityMultiplier = densityMultiplier,
                hostileChance = hostileChance,
                scavengingMultiplier = scavengingMultiplier,
                trapChance = trapChance,
                disableSpawnItems = disableSpawnItems,
                itemCostLimit = itemCostLimit,
                minRadius = minRadius,
                maxRadius = maxRadius,
                wallsDoorsOnly = wallsDoorsOnly,
                enableProximity = enableProximity,
                minimumAreaRequired = minimumAreaRequired,
                minimumDensityRequired = minimumDensityRequired,
                minimumCostRequired = minimumCostRequired,
                deleteLowQuality = deleteLowQuality,
                shouldKeepDefencesAndPower = shouldKeepDefencesAndPower,
                shouldLoadPartOnly = shouldLoadPartOnly,
                shouldAddRaidTriggers = shouldAddRaidTriggers,
                uncoveredCost = uncoveredCost,
                enableInstantCaravanReform = enableInstantCaravanReform,
                shouldAddFilth = shouldAddFilth,
                roomMap = roomMap,
                bottomLeft = bottomLeft,
                blueprintRect = blueprintRect,
                allowFriendlyRaids = allowFriendlyRaids,
                enableDeterioration = enableDeterioration,
                forceFullHitPoints = forceFullHitPoints,
                canHaveFood = canHaveFood,
                blueprintFileName = blueprintFileName,
                centerIfExceedsBounds = centerIfExceedsBounds,
                overwritesEverything = overwritesEverything
    };

            return copy;
        }


        public static ScatterOptions asIs() {
            var options = Default;
            options.overwritesEverything = true;
            options.canHaveFood = true;
            options.scavengingMultiplier = 0.0f;
            options.decorationChance = 0.0f;
            options.enableDeterioration = false;
            options.forceFullHitPoints = true;
            options.shouldAddFilth = false;
            options.trapChance = 0.0f;

            options.startingPartyPoints = -1;
            options.minimumCostRequired = 0;
            options.minimumDensityRequired = 0.0f;
            options.minimumAreaRequired = 0;


            return options;
        }

    }
}
