using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

/**
 * This class provides blueprint based on some high-level requirements.
 * */

namespace RealRuins {
    class BlueprintFinder {
        public static Blueprint FindRandomBlueprintWithParameters(int minArea, float minDensity, out string filename, int maxAttemptsCount = 5) {
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

                Debug.Message("size: {0}x{1} (needed {2}). density {3} (needed {4})", bp.width, bp.height, minArea, bp.itemsDensity, minDensity);

                if (bp.height * bp.width > minArea && bp.itemsDensity > minDensity) {
                    Debug.Message("Success");
                    return bp;
                }
            }
            return bp;
        }
    }
}
