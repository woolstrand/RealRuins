using System;
using System.Collections.Generic;
using Verse;
using RimWorld;
using UnityEngine;

namespace RealRuins.Settings
{
    public class NetworkCacheSettingsPage : SettingsPage
    {
        public override string TabLabel => "RealRuins.CacheSettings.TabCaption".Translate();

        public override float ContentHeight => contentHeight;

        private float contentHeight = 525f;

        // Planetary folders are scanned on disk only when the cache has changed since the last scan (at most once per
        // RescanInterval, so downloads running in the background don't cause a rescan every frame), and whenever the page
        // is shown again after not being drawn for a while (the files could have been touched from outside the game).
        private const float RescanInterval = 1f;
        private List<SnapshotStoreManager.PlanetaryCacheFolder> planetaryFolders;
        private int scannedCacheVersion;
        private float lastScanTime;
        private float lastDrawTime = -1000f;

        private const float RowHeight = 28f;
        private const float ColumnWidth = 95f;
        private const float ClearButtonWidth = 80f;

        public override void Draw(Rect rect)
        {
            Listing_Standard listing = new Listing_Standard();
            
            float contentWidth = rect.width;
            Rect contentRect = rect;
            contentRect.width = contentWidth;
            
            // the planetary table grows the page, so it must not wrap into a second (invisible) column
            listing.maxOneColumn = true;
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

            // Switching offline mode on deletes every downloaded blueprint once the settings are saved
            // (RealRuins_Mod.WriteSettings -> CheckCacheContents), so it is confirmed first. Switching it off needs no confirmation.
            bool offlineMode = RealRuins_ModSettings.offlineMode;
            listing.CheckboxLabeled(
                "RealRuins_ModOptions_OfflineMode".Translate(),
                ref offlineMode,
                "RealRuins_ModOptions_OfflineModeTooltip".Translate());
            if (offlineMode && !RealRuins_ModSettings.offlineMode)
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "RealRuins_ModOptions_OfflineModeConfirm".Translate(),
                    () => RealRuins_ModSettings.offlineMode = true,
                    true,
                    null));
            }
            else if (!offlineMode)
            {
                RealRuins_ModSettings.offlineMode = false;
            }

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

            listing.Gap(15f);

            // Export / Import settings buttons
            Rect importExportRect = listing.GetRect(30f);
            float halfW = (importExportRect.width - 10f) / 2f;
            Rect exportRect = new Rect(importExportRect.x, importExportRect.y, halfW, importExportRect.height);
            Rect importRect = new Rect(importExportRect.x + halfW + 10f, importExportRect.y, halfW, importExportRect.height);

            if (Widgets.ButtonText(exportRect, "RealRuins.Settings.ExportSettings".Translate()))
            {
                Find.WindowStack.Add(new Dialog_ImportExportSettings(true));
            }

            if (Widgets.ButtonText(importRect, "RealRuins.Settings.ImportSettings".Translate()))
            {
                Find.WindowStack.Add(new Dialog_ImportExportSettings(false));
            }

            listing.Gap(15f);
            listing.GapLine();

            DrawPlanetaryCache(listing);

            contentHeight = listing.CurHeight;
            listing.End();
        }

        private void RefreshPlanetaryFoldersIfNeeded()
        {
            float now = Time.realtimeSinceStartup;
            int version = SnapshotStoreManager.CacheVersion;
            bool reopened = now - lastDrawTime > RescanInterval;
            bool changed = version != scannedCacheVersion && now - lastScanTime >= RescanInterval;
            lastDrawTime = now;

            if (planetaryFolders == null || reopened || changed)
            {
                planetaryFolders = SnapshotStoreManager.Instance.GetPlanetaryCacheFolders();
                planetaryFolders.SortBy(f => f.seed, f => f.mapSize);
                scannedCacheVersion = version;
                lastScanTime = now;
            }
        }

        private void DrawPlanetaryCache(Listing_Standard listing)
        {
            RefreshPlanetaryFoldersIfNeeded();

            GameFont font = Text.Font;
            Text.Font = GameFont.Medium;
            listing.Label("RealRuins.CacheSettings.PlanetaryCaption".Translate());
            Text.Font = font;
            listing.Label("RealRuins.CacheSettings.PlanetaryDescription".Translate());
            listing.Gap(5f);

            if (planetaryFolders.Count == 0)
            {
                GUI.color = Color.gray;
                listing.Label("RealRuins.CacheSettings.PlanetaryEmpty".Translate());
                GUI.color = Color.white;
                return;
            }

            TextAnchor anchor = Text.Anchor;
            Text.Anchor = TextAnchor.MiddleLeft;

            GUI.color = Color.gray;
            DrawPlanetaryRow(listing.GetRect(RowHeight),
                "RealRuins.CacheSettings.Seed".Translate(),
                "RealRuins.CacheSettings.Coverage".Translate(),
                "RealRuins.CacheSettings.MapSize".Translate(),
                "RealRuins.CacheSettings.Blueprints".Translate(),
                "RealRuins.CacheSettings.FolderSize".Translate());
            GUI.color = Color.white;

            for (int i = 0; i < planetaryFolders.Count; i++)
            {
                SnapshotStoreManager.PlanetaryCacheFolder folder = planetaryFolders[i];
                Rect row = listing.GetRect(RowHeight);
                if (i % 2 == 0)
                {
                    Widgets.DrawLightHighlight(row);
                }
                Widgets.DrawHighlightIfMouseover(row);

                bool parsed = folder.mapSize >= 0;
                Rect clearRect = DrawPlanetaryRow(row,
                    folder.seed,
                    parsed ? folder.coveragePercent + "%" : "?",
                    parsed ? folder.mapSize + "x" + folder.mapSize : "?",
                    folder.blueprintCount.ToString(),
                    "<b>" + FormatSize(folder.totalSize) + "</b>");

                if (Widgets.ButtonText(clearRect.ContractedBy(2f), "RealRuins.CacheSettings.ClearFolder".Translate()))
                {
                    string folderName = folder.folderName;
                    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                        "RealRuins.CacheSettings.ClearFolderConfirm".Translate(folder.seed, folder.blueprintCount, FormatSize(folder.totalSize)),
                        () => SnapshotStoreManager.Instance.ClearPlanetaryCacheFolder(folderName),
                        true,
                        null));
                }
            }

            Text.Anchor = anchor;
        }

        // Lays out one table line and returns the rect left for the clear button.
        private Rect DrawPlanetaryRow(Rect row, string seed, string coverage, string mapSize, string blueprints, string size)
        {
            float x = row.xMax - ClearButtonWidth;
            Rect clearRect = new Rect(x, row.y, ClearButtonWidth, row.height);
            string[] columns = { size, blueprints, mapSize, coverage };
            foreach (string column in columns)
            {
                x -= ColumnWidth;
                Widgets.Label(new Rect(x, row.y, ColumnWidth, row.height), column);
            }
            Rect seedRect = new Rect(row.x + 5f, row.y, x - row.x - 10f, row.height);
            Widgets.Label(seedRect, seed.Truncate(seedRect.width));
            return clearRect;
        }

        private static string FormatSize(long bytes)
        {
            if (bytes < 1024 * 1024)
            {
                return (bytes / 1024f).ToString("0.#") + " KB";
            }
            return (bytes / (1024f * 1024f)).ToString("0.#") + " MB";
        }
    }
}
