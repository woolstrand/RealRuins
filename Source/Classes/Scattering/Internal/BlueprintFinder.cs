using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

/**
 * This class provides blueprint based on some high-level requirements.
 * */

namespace RealRuins {
    class BlueprintFinder {
        public static Blueprint FindRandomBlueprintWithParameters(out string filename, int minArea = 100, float minDensity = 0.01f, int minCost = 0, int maxAttemptsCount = 5, bool removeNonQualified = false) {
            Blueprint bp = null;
            filename = null;
            int attemptCount = 0;
            while (attemptCount < maxAttemptsCount) {
                attemptCount++;
                filename = SnapshotStoreManager.Instance.RandomSnapshotFilename();
                bp = BlueprintLoader.LoadWholeBlueprintAtPath(filename);
                

                if (bp == null) {
                    Debug.Error(Debug.Store, "Corrupted XML at path {0}, removing", filename);
                    SnapshotStoreManager.Instance.RemoveBlueprintWithName(filename);
                    continue;
                }

                bp.UpdateBlueprintStats(includeCost: true);


                if (bp.height * bp.width > minArea && bp.itemsDensity > minDensity && bp.totalCost >= minCost) {
                    Debug.Log(Debug.Store, "Acceptable blueprint of size: {0}x{1} (needed {2}). density {3} (needed {4}). cost {5} (needed {6}), using.", bp.width, bp.height, minArea, bp.itemsDensity, minDensity, bp.totalCost, minCost);
                    return bp;
                } else {
                    Debug.Log(Debug.Store, "Unacceptable blueprint of size: {0}x{1} (needed {2}). density {3} (needed {4}). cost {5} (needed {6}), skipping...", bp.width, bp.height, minArea, bp.itemsDensity, minDensity, bp.totalCost, minCost);
                    if (removeNonQualified) {
                        Debug.Log(Debug.Store, "Non-qualified XML at path {0}, removing", filename);
                        SnapshotStoreManager.Instance.RemoveBlueprintWithName(filename);
                    }
                }
            }
            filename = null;
            return null;
        }
    }
}
