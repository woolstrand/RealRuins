using Verse;

namespace RealRuins;

internal class RealRuins_ModSettings : ModSettings
{
	public static bool offlineMode = false;

	public static bool allowDownloads = true;

	public static bool allowUploads = true;

	public static bool allowInstantCaravanReform = false;

	public static int caravanReformType = 0;

	public static bool startWithoutRuins = false;

	public static bool preserveStandardRuins = false;

	public static float forceMultiplier = 1f;

	public static float ruinsCostCap = 1E+09f;

	public static float diskCacheLimit = 256f;

	public static int logLevel = 2;

	public static ScatterOptions defaultScatterOptions = ScatterOptions.Default;

	public static PlanetaryRuinsOptions planetaryRuinsOptions = new PlanetaryRuinsOptions();

	public override void ExposeData()
	{
		base.ExposeData();
		Scribe_Values.Look(ref offlineMode, "offlineMode", defaultValue: false);
		Scribe_Values.Look(ref allowDownloads, "allowDownloads", defaultValue: true);
		Scribe_Values.Look(ref allowUploads, "allowUploads", defaultValue: true);
		Scribe_Values.Look(ref diskCacheLimit, "diskCacheLimit", 256f);
		Scribe_Values.Look(ref allowInstantCaravanReform, "allowInstantCaravanReform", defaultValue: false);
		Scribe_Values.Look(ref caravanReformType, "caravanReformType", 0);
		Scribe_Values.Look(ref preserveStandardRuins, "preserveStandardRuins", defaultValue: false);
		Scribe_Values.Look(ref forceMultiplier, "forceMultiplier", 1f);
		Scribe_Values.Look(ref ruinsCostCap, "ruinsCostCap", 1E+09f);
		Scribe_Values.Look(ref startWithoutRuins, "startWithoutRuins", defaultValue: false);
		Scribe_Values.Look(ref logLevel, "logLevel", 2);
		Scribe_Deep.Look(ref defaultScatterOptions, "defaultScatterOptions");
		Scribe_Deep.Look(ref planetaryRuinsOptions, "planetaryRuinsOptions");
		if (allowInstantCaravanReform)
		{
			allowInstantCaravanReform = false;
			caravanReformType = 1;
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
		forceMultiplier = 1f;
		ruinsCostCap = 1E+09f;
		logLevel = 2;
	}
}
