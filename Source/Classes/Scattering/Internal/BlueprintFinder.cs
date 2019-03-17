using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

/**
 * This class provides blueprint based on some high-level requirements.
 * */

namespace RealRuins {
    class BlueprintFinder {
        public static Blueprint FindRandomBlueprintWithParameters(out string filename, int minArea = 100, float minDensity = 0.01f, int minCost = 0, int maxAttemptsCount = 5) {
            Blueprint bp = null;
            filename = null;
            int attemptCount = 0;
            while (attemptCount < maxAttemptsCount) {
                attemptCount++;
                filename = SnapshotStoreManager.Instance.RandomSnapshotFilename();
                Debug.Message("Loading blueprint named {0}", filename);
                bp = BlueprintLoader.LoadWholeBlueprintAtPath(filename);

                if (bp == null) {
                    Debug.Message("Blueprint is null");
                    continue;
                }

                bp.UpdateBlueprintStats();

                Debug.Message("size: {0}x{1} (needed {2}). density {3} (needed {4}). cost {5} (needed {6})", bp.width, bp.height, minArea, bp.itemsDensity, minDensity, bp.totalCost, minCost);

                if (bp.height * bp.width > minArea && bp.itemsDensity > minDensity && bp.totalCost >= minCost) {
                    Debug.Message("Success");
                    return bp;
                }
            }
            return bp;
        }
    }
}
