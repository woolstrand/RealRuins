using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

using Verse;
using RimWorld;
using RimWorld.Planet;
using System.Threading;
using System.Collections;
using System.Collections.Concurrent;

namespace RealRuins {
    enum RuinsPageState {
        Idle = 0,
        LoadingHeader,
        LoadedHeader,
        LoadingBlueprints,
        LoadedBlueprints,
        ProcessingBlueprints,
        Completed
    }

    enum RuinsPageMode {
        Manual,
        Default,
        FullAuto
    }

    class Page_PlanetaryRuinsLoader : Window {

        public override Vector2 InitialSize => new Vector2(650, 410 + 45 + 38);

        private RuinsPageState pageState = RuinsPageState.Idle;
        private int blueprintsTotalCount = 0;
        private int blueprintsToLoadCount = 0;
        private int blueprintsLoadedCount = 0;
        private int blueprintsToProcessCount = 0;
        private int blueprintsProcessedCount = 0; // also incremented from the transfer thread, use Interlocked
        private int blueprintsUsed = 0;

        private int abandonedPercentage = (int)RealRuins_ModSettings.planetaryRuinsOptions.abandonedLocations;
        private bool biomeStrict = true;
        private bool costStrict = false;
        private bool areaStrict = false;
        private bool aggressiveDiscard = RealRuins_ModSettings.planetaryRuinsOptions.excludePlainRuins;
        private bool disableFriendlyLocations = RealRuins_ModSettings.planetaryRuinsOptions.disableSpawnFriendlyLocations;

        private bool forceStopLoading = false;
        private volatile bool forceStopTransfer = false;

        private bool showLoaderOptions = false;
        private bool showTranferrerOptions = false;
        private RuinsPageMode mode = RuinsPageMode.Default;

        // How many maps will be downloaded in case available number is really large.
        private int downloadLimit = RealRuins_ModSettings.planetaryRuinsOptions.downloadLimit;
        private string downloadLimitString = RealRuins_ModSettings.planetaryRuinsOptions.downloadLimit.ToString();

        // How many maps will be transferred
        private int transferLimit = RealRuins_ModSettings.planetaryRuinsOptions.transferLimit;
        private string transferLimitString = RealRuins_ModSettings.planetaryRuinsOptions.transferLimit.ToString();

        private List<string> blueprintIds = null;
        private List<string> filteredList = null;
        private List<PlanetTileInfo> mapTiles;

        private APIService service = new APIService();

        public Page_PlanetaryRuinsLoader(bool forceCleanup = false) {
            doCloseX = true;
            if (forceCleanup) {
                PlanetaryRuinsInitData.shared.Cleanup();
                RemoveSites();
            }
        }

        public void DoWindowContents(Rect rect, bool standalone) {
            Listing_Standard list = new Listing_Standard();
            list.Begin(rect);

            if (standalone) {
                Text.Font = GameFont.Medium;
                list.Label("RealRuins.LoadBlueprintsCaption".Translate());
                Text.Font = GameFont.Small;
            }

            if (Find.World?.info?.Seed == null) {
                Text.Font = GameFont.Medium;
                list.Label("RealRuins.NeedsGameInProgress".Translate());
                list.End();
                return;
            }

            if (showLoaderOptions && showTranferrerOptions) {
                list.Label("RealRuins.OptionsDescription".Translate());
            } else {
                list.Label("RealRuins.LoadBlueprintsDescription".Translate());
                list.Label(new TaggedString("RealRuins.MapSizeChangeWarning".Translate().Colorize(Color.yellow)));
            }
            list.Gap();

            DrawLoader(list);
            DrawTransferrer(list);

            bool canRemove = blueprintsUsed > 0 && pageState == RuinsPageState.Completed;
            Rect bottomRect = rect.BottomPartPixels(canRemove ? 65 : 30);
            Listing_Standard bottomButtons = new Listing_Standard();
            bottomButtons.Begin(bottomRect);

            if (canRemove) {
                if (bottomButtons.ButtonText("RealRuins.RemoveSites".Translate())) {
                    RemoveSites();
                }
            }

            if (standalone) {
                GUI.color = pageState == RuinsPageState.Completed ? new Color(0.3f, 1f, 0.3f) : new Color(1f, 0.3f, 0.35f);
                if (bottomButtons.ButtonText("RealRuins.Close".Translate())) {
                    Find.WindowStack.TryRemove(this);
                }
                GUI.color = Color.white;
            }

            bottomButtons.End();

            list.End();
        }

        public void DrawLoader(Listing_Standard list) {
            Text.Font = GameFont.Small;
            string blueprintStats = "RealRuins.Loading".Translate();
            string skipped = "RealRuins.DroppedMaps".Translate() + " --- ";
            string used = "RealRuins.UsedMaps".Translate() + " --- ";

            switch (pageState) {
                case RuinsPageState.Idle:
                    if (showLoaderOptions) {
                        DrawLoadingOptions(list);
                        if (list.ButtonText("RealRuins.GetCount".Translate())) {
                            StartLoadingList();
                        }
                    } else {
                        var sublist = list.BeginSection(40, 4, 4);
                        sublist.Gap(4);
                        sublist.maxOneColumn = false;
                        sublist.ColumnWidth = (list.ColumnWidth - 20) / 2;
                        if (sublist.ButtonText("RealRuins.StartAuto".Translate())) {
                            mode = RuinsPageMode.Default;
                            StartLoadingList();
                        }
                        sublist.NewColumn();
                        sublist.Gap(4);
                        if (sublist.ButtonText("RealRuins.MoreOptions".Translate())) {
                            showLoaderOptions = true;
                            showTranferrerOptions = true;
                            mode = RuinsPageMode.Manual;
                        }
                        list.EndSection(sublist);
                    }

                    break;

                case RuinsPageState.LoadingHeader:
                    blueprintStats = "RealRuins.Searching".Translate();
                    list.Label(blueprintStats);
                    break;

                case RuinsPageState.LoadedHeader:
                    if (showLoaderOptions) {
                        string loadedText = string.Format("RealRuins.LoadedAmount".Translate(), blueprintsTotalCount);
                        list.Label(loadedText);
                        DrawLoadingOptions(list);
                        if (blueprintsTotalCount < 30) {
                            list.Label(new TaggedString("RealRuins.LowCountWarning".Translate().Colorize(Color.yellow)));
                        }
                        if (list.ButtonText("RealRuins.LoadBlueprints".Translate())) {
                            LoadItems();
                        }
                    }
                    break;

                case RuinsPageState.LoadingBlueprints:
                    blueprintStats = "RealRuins.Loading".Translate() + blueprintsLoadedCount + " / " + blueprintsToLoadCount;
                    list.Label(blueprintStats);
                    string buttonTitle = "RealRuins.StopButtonTitle1".Translate();
                    if (blueprintsLoadedCount > 500) { buttonTitle = "RealRuins.StopButtonTitle2".Translate(); }
                    if (blueprintsLoadedCount > 1300) { buttonTitle = "RealRuins.StopButtonTitle3".Translate(); }
                    if (list.ButtonText(buttonTitle)) {
                        forceStopLoading = true;
                    }
                    break;

                case RuinsPageState.LoadedBlueprints:
                case RuinsPageState.ProcessingBlueprints:
                case RuinsPageState.Completed:
                    blueprintStats = "RealRuins.Loading".Translate() + "RealRuins.Completed".Translate();
                    list.Label(blueprintStats);
                    break;
            }
            list.GapLine();
        }

        public void DrawTransferrer(Listing_Standard list) {

            if (showLoaderOptions || mode == RuinsPageMode.Manual || pageState > RuinsPageState.LoadingBlueprints) {
                Text.Font = GameFont.Medium;
                list.Label("RealRuins.TransferringOptions".Translate());
                Text.Font = GameFont.Small;
            }

            string blueprintStats = "RealRuins.Loading".Translate();
            string skipped = "RealRuins.DroppedMaps".Translate() + " --- ";
            string used = "RealRuins.UsedMaps".Translate() + " --- ";

            switch (pageState) {
                case RuinsPageState.Idle:
                case RuinsPageState.LoadedHeader:
                    if (showTranferrerOptions) {
                        DrawTransferOptions(list);
                        if (list.ButtonText("RealRuins.LoadAndSpawnBlueprints".Translate())) {
                            mode = RuinsPageMode.FullAuto;
                            if (pageState == RuinsPageState.Idle) {
                                StartLoadingList();
                            } else {
                                LoadItems();
                            }
                        }
                    }
                    break;
                case RuinsPageState.LoadingHeader:
                case RuinsPageState.LoadingBlueprints:
                    if (showTranferrerOptions) {
                        DrawTransferOptions(list);
                    }
                    break;

                case RuinsPageState.LoadedBlueprints:
                    if (showTranferrerOptions) {
                        DrawTransferOptions(list);
                        if (list.ButtonText("RealRuins.TransferBlueprints".Translate())) {
                            CreateSites();
                        }
                    }

                    break;
                case RuinsPageState.ProcessingBlueprints:
                    blueprintStats = "RealRuins.Processing".Translate() + blueprintsProcessedCount + " / " + blueprintsToProcessCount;
                    skipped = "RealRuins.DroppedMaps".Translate() + (blueprintsProcessedCount - blueprintsUsed);
                    used = "RealRuins.UsedMaps".Translate() + blueprintsUsed;
                    list.Label(blueprintStats);
                    list.Label(skipped);
                    list.Label(used);
                    break;

                case RuinsPageState.Completed:
                    blueprintStats = "RealRuins.Transferring".Translate() + "RealRuins.Completed".Translate();
                    list.Label(blueprintStats);
                    break;
            }
        }

        private void DrawLoadingOptions(Listing_Standard list) {
            downloadLimitString = list.TextEntryLabeled("RealRuins.DownloadLimit".Translate(), downloadLimitString);
            if (int.TryParse(downloadLimitString, out int number)) {
                this.downloadLimit = number;
            }
        }

        private void DrawTransferOptions(Listing_Standard list) {
            transferLimitString = list.TextEntryLabeled("RealRuins.TransferLimit".Translate(), transferLimitString);
            if (int.TryParse(transferLimitString, out int number)) {
                this.transferLimit = number;
            }

            abandonedPercentage = (int)list.SliderLabeled("RealRuins.AbandonedPercentage".Translate(), (float)abandonedPercentage, 0, 100);

            list.CheckboxLabeled("RealRuins.BiomeFiltering".Translate(), ref biomeStrict, "RealRuins.BiomeFilteringTT".Translate());
            list.CheckboxLabeled("RealRuins.CostFiltering".Translate(), ref costStrict, "RealRuins.CostFilteringTT".Translate());
            list.CheckboxLabeled("RealRuins.AreaFiltering".Translate(), ref areaStrict, "RealRuins.AreaFilteringTT".Translate());
            list.CheckboxLabeled("RealRuins.DiscardAbandoned".Translate(), ref aggressiveDiscard, "RealRuins.DiscardAbandonedTT".Translate());
            list.CheckboxLabeled("RealRuins.DisableFriendlyLocations".Translate(), ref disableFriendlyLocations, "RealRuins.DisableFriendlyLocationsTT".Translate());
        }

        public override void DoWindowContents(Rect rect) {
            DoWindowContents(rect, true);
        }

        private void StartLoadingList() {
            pageState = RuinsPageState.LoadingHeader;
            Debug.Log("Loading list for seed: {0}", Find.World.info.seedString);

            var seed = Find.World.info.seedString;
            var coverage = (int)(Find.World.PlanetCoverage * 100);

            var size = 0;
            if (Find.GameInitData != null) {
                size = Find.GameInitData.mapSize;
            } else {
                size = Find.World.info.initialMapSize.x;
            }

            service.LoadAllMapsForSeed(seed, size, coverage, delegate (bool success, List<PlanetTileInfo> mapTiles) {
                if (success) {
                    if (mapTiles.Count == 0) {
                        this.Close();
                        SmallQuestionDialog q = new SmallQuestionDialog("RealRuins.NoBlueprintsWarning.Caption".Translate(),
                            "RealRuins.NoBlueprintsWarning.Text".Translate(),
                            new string[] { "RealRuins.Close".Translate() },
                            null);
                        Find.WindowStack.Add(q);
                    }

                    this.mapTiles = mapTiles;
                    blueprintIds = new List<string>();
                    foreach (PlanetTileInfo t in mapTiles) {
                        blueprintIds.Add(t.mapId);
                    }

                    blueprintsTotalCount = blueprintIds.Count;
                    pageState = RuinsPageState.LoadedHeader;
                    Debug.Log("Loaded list of snapshot names, {0} elements", blueprintsTotalCount);

                    // If we're in auto mode and have too many blueprints - ask the user what to do
                    if (blueprintsTotalCount > 500 && mode == RuinsPageMode.Default) {
                        // If no options shown -> display alert. Otherwise do nothing: user with options on screen knows what to do.
                        if (mode == RuinsPageMode.Default) {
                            var text = string.Format("RealRuins.TooManyBlueprints.Load.Text".Translate(), blueprintsTotalCount);
                            var dialog = new SmallQuestionDialog("RealRuins.TooManyBlueprints.Load.Title".Translate(),
                                text,
                                new string[] { "RealRuins.TooManyBlueprints.Get500".Translate(),
                                           "RealRuins.TooManyBlueprints.GetAll".Translate(),
                                           "RealRuins.TooManyBlueprints.ShowOptions".Translate() },
                                delegate (int selection) {
                                    switch (selection) {
                                        case 0:
                                            downloadLimit = 500;
                                            transferLimit = 500;
                                            mode = RuinsPageMode.FullAuto;
                                            LoadItems();
                                            break;
                                        case 1:
                                            downloadLimit = 0;
                                            transferLimit = 0;
                                            mode = RuinsPageMode.FullAuto;
                                            LoadItems();
                                            break;
                                        case 2:
                                            showLoaderOptions = true;
                                            showTranferrerOptions = true;
                                            mode = RuinsPageMode.Manual;
                                            break;
                                    }
                                });
                            Find.WindowStack.Add(dialog);
                        }
                    } else {
                        // other cases
                        // options shown: user knows what they are doing
                        // low blueprints count: can proceed
                        // initially asked for full auto: no need to interrupt
                        // etc
                        LoadItems();
                    }
                } else {
                    pageState = RuinsPageState.Idle;
                }
            });
        }

        private void LoadItems() {
            filteredList = null;
            
            // Get blueprints already available on disk for this game
            List<string> alreadyAvailable = SnapshotStoreManager.Instance.GetAvailableBlueprintsForGamePath(SnapshotStoreManager.CurrentGamePath());
            
            // Filter to get only NEW blueprints (not yet available)
            List<string> newBlueprints = new List<string>();
            foreach (string bp in blueprintIds) {
                if (!alreadyAvailable.Contains(bp)) {
                    newBlueprints.Add(bp);
                }
            }
            
            Debug.Log(Debug.POI, "Already available: {0}, new blueprints: {1}, total available: {2}", 
                alreadyAvailable.Count, newBlueprints.Count, blueprintIds.Count);
          
            // Apply download limit to NEW blueprints
            if (downloadLimit == 0 || newBlueprints.Count() < downloadLimit) {
                filteredList = newBlueprints.ListFullCopy();
            } else {
                filteredList = newBlueprints.ListFullCopy().InRandomOrder().ToList().GetRange(0, downloadLimit);
            }
            Debug.Log(Debug.POI, "limit: {0}, new blueprints: {1}, will download: {2}", downloadLimit, newBlueprints.Count, filteredList.Count);

            if (pageState == RuinsPageState.LoadedHeader) {
                pageState = RuinsPageState.LoadingBlueprints;

                SnapshotManager manager = new SnapshotManager();
                manager.Progress = delegate (int progress, int total) {
                    blueprintsLoadedCount = progress;
                    blueprintsToLoadCount = total;

                    if (forceStopLoading) {
                        manager.Stop();
                        LoadingCompleted();
                    }
                };

                manager.Completion = delegate (bool success) {
                    LoadingCompleted();
                };

                Debug.Log(Debug.POI, "Loading blueprints one by one...");
                
                manager.AggressiveLoadSnaphotsFromList(filteredList, gamePath: SnapshotStoreManager.CurrentGamePath(), loadIfExists: false);
            }
        }

        // Checks how many blueprints were loaded, check if any alert should be displayed, proceed with transferring if needed
        private void LoadingCompleted() {
            // Stopping the loader and the final responses of in-flight downloads can each report completion.
            if (pageState != RuinsPageState.LoadingBlueprints) {
                return;
            }
            pageState = RuinsPageState.LoadedBlueprints;
            if (mode == RuinsPageMode.FullAuto || mode == RuinsPageMode.Default) {
                CreateSites();
            } else {
                // do nothing for now, just wait for the user to adjust options.
            }
        }

        private void RemoveSites() {
            List<WorldObject> objectsToRemove = new List<WorldObject>();
            foreach (var obj in Find.WorldObjects.AllWorldObjects) {
                if (obj is RealRuinsPOIWorldObject) {
                    objectsToRemove.Add(obj);
                }
            }

            foreach (var obj in objectsToRemove) {
                Find.WorldObjects.Remove(obj);
            }

            blueprintsUsed = 0;
            pageState = RuinsPageState.Idle;
        }

        // Blueprints are loaded and analyzed on a background thread, world objects are created on the main thread
        // (CoroutineManager). Neither depends on this window: closing it does not stop the transfer.
        private class TransferJob {
            public readonly ConcurrentQueue<RealRuinsPOIFactory.PreparedPOI> prepared = new ConcurrentQueue<RealRuinsPOIFactory.PreparedPOI>();
            public volatile bool preparing = true;
            public volatile bool stop = false;
        }

        private void CreateSites() {
            if (pageState == RuinsPageState.ProcessingBlueprints || pageState == RuinsPageState.Completed) {
                return;
            }

            Debug.Log(Debug.POI, "Completed loading, creating sites.");
            pageState = RuinsPageState.ProcessingBlueprints;

            string gamePath = SnapshotStoreManager.CurrentGamePath();
            IntVec3 initialMapSize = Find.World.info.initialMapSize;

            // Get all blueprints available on disk for this game (previous sessions + current)
            HashSet<string> availableBlueprints = new HashSet<string>(SnapshotStoreManager.Instance.GetAvailableBlueprintsForGamePath(gamePath));

            blueprintsToProcessCount = mapTiles.Count;
            blueprintsProcessedCount = 0;
            List<PlanetTileInfo> candidates = new List<PlanetTileInfo>();
            foreach (PlanetTileInfo t in mapTiles) {
                if (!availableBlueprints.Contains(t.mapId)) {
                    Debug.Log(Debug.POI, "Skipped: Blueprint {0} is not available locally", t.mapId);
                    blueprintsProcessedCount++;
                    continue;
                }

                if (biomeStrict && (t.originX == 0 && t.originZ == 0 || t.biomeName == null)) {
                    Debug.Log(Debug.POI, "Skipped: Biome filtering is on, but blueprint does not contain biome or location information");
                    blueprintsProcessedCount++;
                    continue;
                }

                if (!RealRuinsPOIFactory.IsSuitableTile(t, biomeStrict)) {
                    blueprintsProcessedCount++;
                    continue;
                }

                candidates.Add(t);
            }

            TransferJob job = new TransferJob();
            new Thread(() => PrepareSites(job, candidates, gamePath, initialMapSize)) { IsBackground = true }.Start();
            CoroutineManager.Instance.RunCoroutine(CommitSites(job, gamePath));
        }

        // Background thread: no engine state access here.
        private void PrepareSites(TransferJob job, List<PlanetTileInfo> candidates, string gamePath, IntVec3 initialMapSize) {
            try {
                foreach (PlanetTileInfo t in candidates) {
                    if (job.stop || forceStopTransfer) {
                        break;
                    }

                    RealRuinsPOIFactory.PreparedPOI prepared = null;
                    try {
                        prepared = RealRuinsPOIFactory.PreparePOI(t, gamePath, initialMapSize, costStrict, aggressiveDiscard);
                    } catch (Exception e) {
                        Debug.Log(Debug.POI, $"PreparePOI failed with exception {e}");
                    }

                    if (prepared != null) {
                        job.prepared.Enqueue(prepared);
                    } else {
                        Interlocked.Increment(ref blueprintsProcessedCount);
                    }
                }
            } finally {
                job.preparing = false;
            }
        }

        // Main thread: creates world objects from prepared blueprints, a few milliseconds per frame.
        private IEnumerator CommitSites(TransferJob job, string gamePath) {
            const long frameBudgetMs = 5;
            World world = Find.World;
            System.Diagnostics.Stopwatch frameTimer = new System.Diagnostics.Stopwatch();

            while (true) {
                // Read before draining: if preparation was already over, the queue is final once drained.
                bool preparationFinished = !job.preparing;

                if (forceStopTransfer || Find.World != world) {
                    job.stop = true;
                    pageState = RuinsPageState.Idle;
                    yield break;
                }

                frameTimer.Restart();
                while (frameTimer.ElapsedMilliseconds < frameBudgetMs && job.prepared.TryDequeue(out RealRuinsPOIFactory.PreparedPOI prepared)) {
                    Interlocked.Increment(ref blueprintsProcessedCount);
                    try {
                        if (RealRuinsPOIFactory.CommitPOI(prepared, gamePath, biomeStrict, abandonedPercentage, disableFriendlyLocations)) {
                            blueprintsUsed++;
                            if (blueprintsUsed >= transferLimit && transferLimit != 0) {
                                Debug.Log(Debug.POI, "Reached limit of {0} blueprints", transferLimit);
                                job.stop = true;
                                pageState = RuinsPageState.Completed;
                                yield break;
                            }
                        } else {
                            Debug.Log(Debug.POI, "CommitPOI returned false.");
                        }
                    } catch (Exception e) {
                        Debug.Log(Debug.POI, $"CommitPOI failed with exception {e}");
                    }
                }

                if (preparationFinished && job.prepared.IsEmpty) {
                    break;
                }
                yield return null;
            }

            Debug.Log(Debug.POI, "Transfer finished, {0} sites created", blueprintsUsed);
            pageState = RuinsPageState.Completed;
        }

        public override void PreOpen() {
            base.PreOpen();
        }
    }
}
