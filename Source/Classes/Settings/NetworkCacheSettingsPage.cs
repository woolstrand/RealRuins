using System;
using Verse;
using RimWorld;
using UnityEngine;

namespace RealRuins.Settings
{
    public class NetworkCacheSettingsPage : SettingsPage
    {
        public override string TabLabel => "RealRuins.CacheSettings.TabCaption".Translate();

        public override void Draw(Rect rect)
        {
            Listing_Standard listing = new Listing_Standard();
            
            float contentWidth = rect.width;
            Rect contentRect = rect;
            contentRect.width = contentWidth;
            
            listing.Begin(contentRect);

            GameFont font = Text.Font;
            Text.Font = GameFont.Medium;
            listing.Label("RealRuins.CacheSettings.Caption".Translate());
            Text.Font = font;
            
            float descHeight = Text.CalcHeight("RealRuins.CacheSettings.Description".Translate(), rect.width);
            Rect descRect = listing.GetRect(descHeight);
            string description = "RealRuins.CacheSettings.Description".Translate();
            Widgets.Label(descRect, description);
            listing.Gap(5f);
            
            listing.GapLine();

            listing.Label(
                "RealRuins_ModOptions_CurrentCacheSize".Translate() + " " +
                SnapshotStoreManager.Instance.TotalSize() / (1024 * 1024) + " MB");

            listing.Label(
                "RealRuins_ModOptions_CurrentCacheCount".Translate() + " " +
                SnapshotStoreManager.Instance.StoredSnapshotsCount());

            string cacheLimitStr = RealRuins_ModSettings.diskCacheLimit < 0 ? (string)"RealRuins_ModOptions_NoLimit".Translate() : (((int)(RealRuins_ModSettings.diskCacheLimit)).ToString() + " MB");
            listing.Label(
                "RealRuins_ModOptions_CacheSize".Translate() + "  " + cacheLimitStr,
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

            // Three-column button layout
            Rect buttonRowRect = listing.GetRect(100f);
            float colWidth = buttonRowRect.width / 3f;

            // Column 1: Download buttons (50 and 500)
            Rect col1Rect = new Rect(buttonRowRect.x, buttonRowRect.y, colWidth - 5f, buttonRowRect.height);
            Rect download50Rect = new Rect(col1Rect.x, col1Rect.y, col1Rect.width, 40f);
            if (Widgets.ButtonText(download50Rect, "RealRuins_ModOptions_DownloadMore".Translate() + " (50)"))
            {
                SnapshotManager.Instance.LoadSomeSnapshots(5);
            }

            Rect download500Rect = new Rect(col1Rect.x, col1Rect.y + 45f, col1Rect.width, 40f);
            if (Widgets.ButtonText(download500Rect, "RealRuins_ModOptions_DownloadMore".Translate() + " (500)"))
            {
                for (int i = 0; i < 10; i++)
                {
                    SnapshotManager.Instance.LoadSomeSnapshots();
                }
            }

            // Column 2: Clear cache (double height, red)
            Rect col2Rect = new Rect(buttonRowRect.x + colWidth, buttonRowRect.y, colWidth - 5f, buttonRowRect.height);
            Rect clearCacheRect = new Rect(col2Rect.x, col2Rect.y, col2Rect.width, 85f);
            Color prevColor = GUI.color;
            GUI.color = new Color(1f, 0.3f, 0.3f);
            if (Widgets.ButtonText(clearCacheRect, "RealRuins_ModOptions_RemoveAll".Translate()))
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "RealRuins_ModOptions_ClearCacheConfirm".Translate(),
                    () => SnapshotStoreManager.Instance.ClearCache(),
                    true,
                    null));
            }
            GUI.color = prevColor;

            // Column 3: Manual upload (double height, green)
            Rect col3Rect = new Rect(buttonRowRect.x + colWidth * 2f, buttonRowRect.y, colWidth - 5f, buttonRowRect.height);
            Rect uploadRect = new Rect(col3Rect.x, col3Rect.y, col3Rect.width, 85f);
            GUI.color = new Color(0.3f, 0.8f, 0.3f);
            if (Widgets.ButtonText(uploadRect, "RealRuins.DebugSettings.ManualUpload".Translate()))
            {
                if (Find.CurrentMap != null)
                {
                    SnapshotManager.Instance.UploadCurrentMapSnapshot();
                    Messages.Message(
                        "RealRuins.DebugSettings.UploadTriggered".Translate(),
                        MessageTypeDefOf.NeutralEvent);
                }
                else
                {
                    Messages.Message(
                        "RealRuins.DebugSettings.NoMap".Translate(),
                        MessageTypeDefOf.RejectInput);
                }
            }
            GUI.color = prevColor;

            TooltipHandler.TipRegion(download50Rect, "RealRuins_ModOptions_DownloadMoreTooltip".Translate());
            TooltipHandler.TipRegion(clearCacheRect, "RealRuins_ModOptions_CacheSizeTooltip".Translate());
            TooltipHandler.TipRegion(uploadRect, "RealRuins.DebugSettings.ManualUploadTooltip".Translate());

            listing.End();
        }
    }
}
