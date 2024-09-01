namespace RealRuins;

internal class BlueprintFinder
{
	public static Blueprint FindRandomBlueprintWithParameters(out string filename, int minArea = 100, float minDensity = 0.01f, int minCost = 0, int maxAttemptsCount = 5, bool removeNonQualified = false)
	{
		Blueprint blueprint = null;
		filename = null;
		int num = 0;
		while (num < maxAttemptsCount)
		{
			num++;
			filename = SnapshotStoreManager.Instance.RandomSnapshotFilename();
			blueprint = BlueprintLoader.LoadWholeBlueprintAtPath(filename);
			if (blueprint == null)
			{
				Debug.Error("Store", "Corrupted XML at path {0}, removing", filename);
				SnapshotStoreManager.Instance.RemoveBlueprintWithName(filename);
				continue;
			}
			blueprint.UpdateBlueprintStats(includeCost: true);
			if (blueprint.height * blueprint.width > minArea && blueprint.itemsDensity > minDensity && blueprint.totalCost >= (float)minCost)
			{
				Debug.Log("Store", "Acceptable blueprint of size: {0}x{1} (needed {2}). density {3} (needed {4}). cost {5} (needed {6}), using.", blueprint.width, blueprint.height, minArea, blueprint.itemsDensity, minDensity, blueprint.totalCost, minCost);
				return blueprint;
			}
			Debug.Log("Store", "Unacceptable blueprint of size: {0}x{1} (needed {2}). density {3} (needed {4}). cost {5} (needed {6}), skipping...", blueprint.width, blueprint.height, minArea, blueprint.itemsDensity, minDensity, blueprint.totalCost, minCost);
			if (removeNonQualified)
			{
				Debug.Log("Store", "Non-qualified XML at path {0}, removing", filename);
				SnapshotStoreManager.Instance.RemoveBlueprintWithName(filename);
			}
		}
		filename = null;
		return null;
	}
}
