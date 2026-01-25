using System;
using Verse;
using UnityEngine;

namespace RealRuins.Settings
{
    public class NetworkCacheSettingsPage : SettingsPage
    {
        public override string TabLabel => "RealRuins.CacheSettings.Caption".Translate();

        public override void Draw(Rect rect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(rect);

            GameFont font = Text.Font;
            Text.Font = GameFont.Medium;
            listing.Label("RealRuins.CacheSettings.Caption".Translate());
            Text.Font = font;
            listing.GapLine();

            listing.CheckboxLabeled(
                "RealRuins_ModOptions_OfflineMode".Translate(),
                ref RealRuins_ModSettings.offlineMode,
                "RealRuins_ModOptions_OfflineModeTooltip".Translate());

            listing.Label(
                "RealRuins_ModOptions_CurrentCacheSize".Translate() + " " +
                SnapshotStoreManager.Instance.TotalSize() / (1024 * 1024) + " MB");

            listing.Label(
                "RealRuins_ModOptions_CurrentCacheCount".Translate() + " " +
                SnapshotStoreManager.Instance.StoredSnapshotsCount());

            listing.Label(
                "RealRuins_ModOptions_CacheSize".Translate() + "  " +
                ((int)(RealRuins_ModSettings.diskCacheLimit)).ToString() + " MB",
                -1f,
                "RealRuins_ModOptions_CacheSizeTooltip".Translate());

            listing.Gap(15f);

            if (listing.ButtonText("RealRuins_ModOptions_DownloadMore".Translate() + " (50)", null))
            {
                SnapshotManager.Instance.LoadSomeSnapshots(5);
            }

            if (listing.ButtonText("RealRuins_ModOptions_DownloadMore".Translate() + "(500)", null))
            {
                for (int i = 0; i < 10; i++)
                {
                    SnapshotManager.Instance.LoadSomeSnapshots();
                }
            }

            listing.Gap(25f);

            listing.CheckboxLabeled(
                "RealRuins_ModOptions_AllowDownloads".Translate(),
                ref RealRuins_ModSettings.allowDownloads,
                "RealRuins_ModOptions_AllowDownloadsTooltip".Translate());

            listing.CheckboxLabeled(
                "RealRuins_ModOptions_AllowUploads".Translate(),
                ref RealRuins_ModSettings.allowUploads,
                "RealRuins_ModOptions_AllowUploadsTooltip".Translate());

            listing.Gap(15f);

            RealRuins_ModSettings.diskCacheLimit = listing.Slider(
                RealRuins_ModSettings.diskCacheLimit,
                20.0f,
                2048.0f);

            listing.Gap(10f);

            if (listing.ButtonText("RealRuins_ModOptions_RemoveAll".Translate(), null))
            {
                SnapshotStoreManager.Instance.ClearCache();
            }

            listing.End();
        }
    }
}
