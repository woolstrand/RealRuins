using System;
using Verse;

namespace RealRuins
{
    [StaticConstructorOnStartup]
    static class RealRuins_StartupHook {
        static RealRuins_StartupHook() {
            LongEventHandler.QueueLongEvent(() => {
                Debug.SysLog("RealRuins Startup hook triggered");

                if (RealRuins_ModSettings.allowDownloads && !RealRuins_ModSettings.offlineMode &&
                    SnapshotStoreManager.Instance.StoredSnapshotsCount() < 100) {
                    SnapshotManager.Instance.AggressiveLoadSnapshots();
                }

                SnapshotStoreManager.Instance.CheckCacheSizeLimits();
            }, "RealRuins_Init", false, null);
        }
    }
}

