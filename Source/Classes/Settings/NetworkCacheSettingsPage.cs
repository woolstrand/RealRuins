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

        protected override void DrawContents(Listing_Standard listing)
        {
            SettingsControls.PageHeader(listing, "RealRuins.CacheSettings.Caption", "RealRuins.CacheSettings.Description");

            if (RealRuins_ModSettings.useStagingEnvironment)
            {
                GUI.color = new Color(1f, 0.3f, 0.3f);
                listing.Label("RealRuins.CacheSettings.StagingWarning".Translate());
                GUI.color = Color.white;
            }

            SettingsControls.ValueRow(listing, "RealRuins_ModOptions_CurrentCacheSize".Translate(),
                SnapshotStoreManager.Instance.TotalSize() / (1024 * 1024) + " MB");
            SettingsControls.ValueRow(listing, "RealRuins_ModOptions_CurrentCacheCount".Translate(),
                SnapshotStoreManager.Instance.StoredSnapshotsCount().ToString());

            string cacheLimitStr = RealRuins_ModSettings.diskCacheLimit < 0 ? (string)"RealRuins_ModOptions_NoLimit".Translate() : (((int)(RealRuins_ModSettings.diskCacheLimit)).ToString() + " MB");
            float sliderValue = SettingsControls.SliderRow(listing,
                "RealRuins_ModOptions_CacheSize".Translate().ToString().TrimEnd() + " " + cacheLimitStr,
                RealRuins_ModSettings.diskCacheLimit < 0 ? 4096.0f : RealRuins_ModSettings.diskCacheLimit,
                20.0f, 4096.0f,
                "RealRuins_ModOptions_CacheSizeTooltip".Translate());
            RealRuins_ModSettings.diskCacheLimit = sliderValue >= 4095.0f ? -1f : sliderValue;

            SettingsControls.Section(listing, "RealRuins.CacheSettings.NetworkSection");

            // Switching offline mode on deletes every downloaded blueprint once the settings are saved
            // (RealRuins_Mod.WriteSettings -> CheckCacheContents), so it is confirmed first. Switching it off needs no confirmation.
            bool offlineMode = RealRuins_ModSettings.offlineMode;
            SettingsControls.Checkbox(listing, "RealRuins_ModOptions_OfflineMode", ref offlineMode,
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

            SettingsControls.Checkbox(listing, "RealRuins_ModOptions_AllowDownloads", ref RealRuins_ModSettings.allowDownloads,
                "RealRuins_ModOptions_AllowDownloadsTooltip".Translate());
            SettingsControls.Checkbox(listing, "RealRuins_ModOptions_AllowUploads", ref RealRuins_ModSettings.allowUploads,
                "RealRuins_ModOptions_AllowUploadsTooltip".Translate());

            listing.Gap(8f);

            string downloadTooltip = "RealRuins_ModOptions_DownloadMoreTooltip".Translate();
            int clicked = SettingsControls.ButtonRow(listing,
                new string[] {
                    "RealRuins_ModOptions_DownloadMore".Translate() + " (50)",
                    "RealRuins_ModOptions_DownloadMore".Translate() + " (500)",
                    "RealRuins_ModOptions_RemoveAll".Translate()
                },
                new Color?[] { null, null, SettingsControls.DangerColor },
                new string[] { downloadTooltip, downloadTooltip, "RealRuins_ModOptions_CacheSizeTooltip".Translate() });
            switch (clicked)
            {
                case 0:
                    SnapshotManager.Instance.LoadSomeSnapshots(5);
                    break;
                case 1:
                    for (int i = 0; i < 10; i++)
                    {
                        SnapshotManager.Instance.LoadSomeSnapshots();
                    }
                    break;
                case 2:
                    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                        "RealRuins_ModOptions_ClearCacheConfirm".Translate(),
                        () => SnapshotStoreManager.Instance.ClearCache(),
                        true,
                        null));
                    break;
            }

            DrawPlanetaryCache(listing);
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

            SettingsControls.Section(listing, "RealRuins.CacheSettings.PlanetaryCaption");
            SettingsControls.Description(listing, "RealRuins.CacheSettings.PlanetaryDescription".Translate());
            listing.Gap(8f);

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
