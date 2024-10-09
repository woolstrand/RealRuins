using Verse;

namespace RealRuins;

internal class CoverageMap
{
	private bool[,] coverageMap;

	public static CoverageMap EmptyCoverageMap(Map map)
	{
		CoverageMap coverageMap = new CoverageMap();
		coverageMap.coverageMap = new bool[map.Size.x, map.Size.z];
		return coverageMap;
	}

	public bool isMarked(int x, int z)
	{
		return coverageMap[x, z];
	}

	public void Mark(int x, int z)
	{
		coverageMap[x, z] = true;
	}

	public void DebugPrint()
	{
		Debug.PrintBoolMap(coverageMap);
	}
}
