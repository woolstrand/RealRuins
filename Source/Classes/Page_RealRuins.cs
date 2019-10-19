using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

using Verse;
using RimWorld;
using System.Threading;

namespace RealRuins {
    enum RuinsPageState {
        Idle = 0,
        LoadingHeader,
        LoadedHeader,
        LoadingBlueprints,
        ProcessingBlueprints,
        Completed
    }

    class Page_RealRuins : Window {

        public override Vector2 InitialSize => new Vector2(650, 210 + 45 + 38);

        private RuinsPageState pageState = RuinsPageState.Idle;
        private int blueprintsTotalCount = 0;
        private int blueprintsToLoadCount = 0;
        private int blueprintsLoadedCount = 0;
        private int blueprintsProcessedCount = 0;
        private int blueprintsUsed = 0;
        private bool biomeStrict = true;
        private bool costStrict = false;
        private bool areaStrict = false;
        private List<string> blueprintIds = null;
        private List<PlanetTileInfo> mapTiles;

        private APIService service = new APIService();

        public Page_RealRuins() {
            doCloseX = true;
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

            Text.Font = GameFont.Small;
            string blueprintStats = "RealRuins.Loading".Translate();
            string skipped = "RealRuins.DroppedMaps".Translate() + " --- ";
            string used = "RealRuins.UsedMaps".Translate() + " --- ";

            switch (pageState) {
                case RuinsPageState.Idle:
                    blueprintStats = "RealRuins.Idle".Translate();
                    break;
                case RuinsPageState.LoadingHeader:
                    blueprintStats = "RealRuins.Searching".Translate();
                    break;
                case RuinsPageState.LoadedHeader:
                case RuinsPageState.LoadingBlueprints:
                    blueprintStats = "RealRuins.Loading".Translate() + blueprintsLoadedCount + " / " + blueprintsToLoadCount;
                    break;
                case RuinsPageState.ProcessingBlueprints:
                    blueprintStats = "RealRuins.Processing".Translate() + blueprintsProcessedCount + " / " + blueprintsTotalCount;
                    skipped = "RealRuins.DroppedMaps".Translate() + (blueprintsProcessedCount - blueprintsUsed);
                    used = "RealRuins.UsedMaps".Translate() + blueprintsUsed;
                    break;
                case RuinsPageState.Completed:
                    blueprintStats = "RealRuins.Completed".Translate();
                    skipped = "RealRuins.DroppedMaps".Translate() + (blueprintsProcessedCount - blueprintsUsed);
                    used = "RealRuins.UsedMaps".Translate() + blueprintsUsed;
                    break;
            }

            list.Label(blueprintStats);

            list.Label(skipped);
            list.Label(used);

            list.CheckboxLabeled("RealRuins.BiomeFiltering".Translate(), ref biomeStrict, "RealRuins.BiomeFilteringTT".Translate());
            list.CheckboxLabeled("RealRuins.CostFiltering".Translate(), ref costStrict, "RealRuins.CostFilteringTT".Translate());
            list.CheckboxLabeled("RealRuins.AreaFiltering".Translate(), ref areaStrict, "RealRuins.AreaFilteringTT".Translate());

            if (list.ButtonText("RealRuins.LoadBlueprints".Translate())) {
                StartLoadingList();
            }

            list.Gap();

            if (standalone) {
                if (list.ButtonText("RealRuins.Close".Translate())) {
                    Find.WindowStack.TryRemove(this);
                }
            }
            list.End();
        }

        public override void DoWindowContents(Rect rect) {
            DoWindowContents(rect, true);
        }

        private void StartLoadingList() {
            pageState = RuinsPageState.LoadingHeader;
            Debug.Log("Loading list for seed: {0}", Find.World.info.seedString);
            service.LoadAllMapsForSeed(Find.World.info.seedString, Find.World.info.initialMapSize.x, (int)(Find.World.PlanetCoverage * 100), delegate (bool success, List<PlanetTileInfo> mapTiles) {
                if (success) {
                    this.mapTiles = mapTiles;
                    blueprintIds = new List<string>();
                    foreach (PlanetTileInfo t in mapTiles) {
                        blueprintIds.Add(t.mapId);
                    }

                    blueprintsTotalCount = blueprintIds.Count;
                    pageState = RuinsPageState.LoadedHeader;
                    Debug.Log("Loaded list of snapshot names, {0} elements", blueprintsTotalCount);
                    LoadItems();
                } else {
                    pageState = RuinsPageState.Idle;
                }
            });
        }

        private void LoadItems() { 
            if (pageState == RuinsPageState.LoadedHeader) {
                pageState = RuinsPageState.LoadingBlueprints;

                SnapshotManager manager = new SnapshotManager();
                manager.Progress = delegate (int progress, int total) {
                    blueprintsLoadedCount = progress;
                    blueprintsToLoadCount = total;
                };

                manager.Completion = delegate (bool success) {
                    Debug.Log(Debug.POI, "Completed loading, creating sites.");
                    pageState = RuinsPageState.ProcessingBlueprints;
                    new Thread(() => {
                        CreateSites();
                        pageState = RuinsPageState.Completed;
                    }).Start();
                };

                Debug.Log(Debug.POI, "Loading blueprints one by one...");
                
                manager.AggressiveLoadSnaphotsFromList(blueprintIds, gamePath: SnapshotStoreManager.CurrentGamePath(), loadIfExists: false);
            }
        }

        private void CreateSites() {
            foreach (PlanetTileInfo t in mapTiles) {
                blueprintsProcessedCount++;
                if (biomeStrict) {
                    if (t.originX == 0 && t.originZ == 0 || t.biomeName == null) continue;
                }
                try {
                    if (RealRuinsPOIFactory.CreatePOI(t, SnapshotStoreManager.CurrentGamePath(), biomeStrict, costStrict, areaStrict)) {
                        blueprintsUsed++;
                    }
                } catch {
                    //just skip blueprint
                }
            }
        }

        public override void PreOpen() {
            base.PreOpen();
        }
    }
}
