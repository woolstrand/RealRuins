using Verse;

namespace RealRuins;

internal class ScatterOptions : IExposable
{
	public float densityMultiplier = 1f;

	public int minRadius = 8;

	public int maxRadius = 16;

	public float deteriorationMultiplier = 0f;

	public float scavengingMultiplier = 1f;

	public int itemCostLimit = 1000;

	public bool disableSpawnItems = false;

	public bool wallsDoorsOnly = false;

	public bool claimableBlocks = true;

	public bool enableProximity = true;

	public float decorationChance = 0.0001f;

	public float trapChance = 0.001f;

	public float hostileChance = 0.1f;

	public float uncoveredCost = 0f;

	public int[,] roomMap;

	public IntVec3 bottomLeft = new IntVec3(-1000, 0, -1000);

	public CellRect blueprintRect = default(CellRect);

	public int minimumAreaRequired = 0;

	public float minimumDensityRequired = 0.1f;

	public int minimumCostRequired = 0;

	public int costCap = -1;

	public int startingPartyPoints = 0;

	public bool shouldKeepDefencesAndPower = false;

	public bool shouldLoadPartOnly = true;

	public bool shouldAddRaidTriggers = false;

	public bool enableInstantCaravanReform = false;

	public bool allowFriendlyRaids = true;

	public bool enableDeterioration = true;

	public bool forceFullHitPoints = false;

	public bool canHaveFood = true;

	public bool shouldAddFilth = true;

	public IntVec3 overridePosition = new IntVec3(0, 0, 0);

	public bool overwritesEverything = false;

	public bool centerIfExceedsBounds = false;

	public string blueprintFileName = null;

	public bool deleteLowQuality = true;

	public static readonly ScatterOptions Default = new ScatterOptions();

	public void ExposeData()
	{
		Scribe_Values.Look(ref densityMultiplier, "densityMultiplier", 1f);
		Scribe_Values.Look(ref minRadius, "minRadius", 8);
		Scribe_Values.Look(ref maxRadius, "maxRadius", 16);
		Scribe_Values.Look(ref deteriorationMultiplier, "deteriorationMultiplier", 0f);
		Scribe_Values.Look(ref scavengingMultiplier, "scavengingMultiplier", 1f);
		Scribe_Values.Look(ref itemCostLimit, "itemCostLimit", 1000);
		Scribe_Values.Look(ref disableSpawnItems, "disableSpawnItems", defaultValue: false);
		Scribe_Values.Look(ref wallsDoorsOnly, "wallsDoorsOnly", defaultValue: false);
		Scribe_Values.Look(ref claimableBlocks, "claimableBlocks", defaultValue: true);
		Scribe_Values.Look(ref enableProximity, "enableProximity", defaultValue: true);
		Scribe_Values.Look(ref decorationChance, "decorationChance", 0.0001f);
		Scribe_Values.Look(ref trapChance, "trapChance", 0.001f);
		Scribe_Values.Look(ref hostileChance, "hostileChance", 0.1f);
		Scribe_Values.Look(ref enableInstantCaravanReform, "enableInstantCaravanReform", defaultValue: false);
	}

	public ScatterOptions Copy()
	{
		return new ScatterOptions
		{
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
	}

	public static ScatterOptions asIs()
	{
		ScatterOptions @default = Default;
		@default.overwritesEverything = true;
		@default.canHaveFood = true;
		@default.scavengingMultiplier = 0f;
		@default.decorationChance = 0f;
		@default.enableDeterioration = false;
		@default.forceFullHitPoints = true;
		@default.shouldAddFilth = false;
		@default.trapChance = 0f;
		@default.startingPartyPoints = -1;
		@default.minimumCostRequired = 0;
		@default.minimumDensityRequired = 0f;
		@default.minimumAreaRequired = 0;
		return @default;
	}
}
