using System;
using Verse;
using UnityEngine;

namespace RealRuins.Settings
{
    public class NetworkCacheSettingsPage : SettingsPage
    {
        public override string TabLabel => "RealRuins.CacheSettings.TabCaption".Translate();

        public override void Draw(Rect rect)
        {
            Listing_Standard listing = new Listing_Standard();
            
            float contentWidth = rect.width * 2f / 3f;
            Rect contentRect = rect;
            contentRect.width = contentWidth;
            
            listing.Begin(contentRect);

            GameFont font = Text.Font;
            Text.Font = GameFont.Medium;
            listing.Label("RealRuins.CacheSettings.Caption".Translate());
            Text.Font = font;
            
            float descHeight = Text.LineHeight * 3f;
            Rect descRect = listing.GetRect(descHeight);
            string description = "RealRuins.CacheSettings.Description".Translate();
            Widgets.Label(descRect, description);
            listing.Gap(descHeight - Text.LineHeight);
            
            listing.GapLine();

            listing.Label(
                "RealRuins_ModOptions_CurrentCacheSize".Translate() + " " +
                SnapshotStoreManager.Instance.TotalSize() / (1024 * 1024) + " MB");

            listing.Label(
                "RealRuins_ModOptions_CurrentCacheCount".Translate() + " " +
                SnapshotStoreManager.Instance.StoredSnapshotsCount());

            listing.Label(
                "RealRuins_ModOptions_CacheSize".Translate() + "  " +
                (RealRuins_ModSettings.diskCacheLimit < 0 ? "RealRuins_ModOptions_NoLimit".Translate() : ((int)(RealRuins_ModSettings.diskCacheLimit)).ToString() + " MB"),
                -1f,
                "RealRuins_ModOptions_CacheSizeTooltip".Translate());

            listing.Gap(10f);

            float sliderValue = listing.Slider(
                RealRuins_ModSettings.diskCacheLimit,
                20.0f,
                4096.0f);
            
            if (sliderValue >= 4095.0f)
            {
                RealRuins_ModSettings.diskCacheLimit = -1f;
            }
            else
            {
                RealRuins_ModSettings.diskCacheLimit = sliderValue;
            }

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
                "RealRuins_ModOptions_OfflineMode".Translate(),
                ref RealRuins_ModSettings.offlineMode,
                "RealRuins_ModOptions_OfflineModeTooltip".Translate());

            listing.CheckboxLabeled(
                "RealRuins_ModOptions_AllowDownloads".Translate(),
                ref RealRuins_ModSettings.allowDownloads,
                "RealRuins_ModOptions_AllowDownloadsTooltip".Translate());

            listing.CheckboxLabeled(
                "RealRuins_ModOptions_AllowUploads".Translate(),
                ref RealRuins_ModSettings.allowUploads,
                "RealRuins_ModOptions_AllowUploadsTooltip".Translate());

            listing.Gap(15f);

            Rect buttonRect = listing.GetRect(40f);
            Color prevColor = GUI.color;
            GUI.color = new Color(1f, 0.3f, 0.3f);
            if (Widgets.ButtonText(buttonRect, "RealRuins_ModOptions_RemoveAll".Translate()))
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "RealRuins_ModOptions_ClearCacheConfirm".Translate(),
                    () => SnapshotStoreManager.Instance.ClearCache(),
                    true,
                    null));
            }
            GUI.color = prevColor;

            listing.End();
        }
    }
}
