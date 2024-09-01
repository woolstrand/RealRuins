namespace RealRuins;

internal class PlanetaryRuinsInitData
{
	public static PlanetaryRuinsInitData shared = new PlanetaryRuinsInitData();

	public int selectedMapSize;

	public string selectedSeed;

	public PlanetaryRuinsState state;

	public RealRuinsPOIWorldObject startingPOI;

	public SettleMode settleMode;

	public void Cleanup()
	{
		startingPOI = null;
		selectedMapSize = 0;
		selectedSeed = null;
		state = PlanetaryRuinsState.spawned;
	}
}
