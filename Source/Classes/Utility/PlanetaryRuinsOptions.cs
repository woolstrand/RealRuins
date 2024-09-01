using Verse;

namespace RealRuins;

public class PlanetaryRuinsOptions : IExposable
{
	public bool allowOnStart = true;

	public int downloadLimit = 0;

	public int transferLimit = 0;

	public bool excludePlainRuins = false;

	public float abandonedLocations = 0.2f;

	public void ExposeData()
	{
		Scribe_Values.Look(ref allowOnStart, "allowOnStart", defaultValue: true);
		Scribe_Values.Look(ref downloadLimit, "downloadLimit", 0);
		Scribe_Values.Look(ref transferLimit, "transferLimit", 0);
		Scribe_Values.Look(ref excludePlainRuins, "excludePlainRuins", defaultValue: false);
		Scribe_Values.Look(ref abandonedLocations, "abandonedLocations", 0.2f);
	}
}
