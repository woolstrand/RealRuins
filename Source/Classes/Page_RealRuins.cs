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

    class Page_RealRuins : Page {

        public override string PageTitle => "RealRuinsSetup".Translate();

        private RuinsPageState pageState = RuinsPageState.Idle;
        private int blueprintsTotalCount = 0;
        private int blueprintsLoadedCount = 0;
        private int blueprintsProcessedCount = 0;
        private bool strict = true;
        private List<string> blueprintIds = null;
        private List<PlanetTileInfo> mapTiles;

        private APIService service = new APIService();


        public override void DoWindowContents(Rect rect) {
            DrawPageTitle(rect);
            GUI.BeginGroup(GetMainRect(rect));
            Text.Font = GameFont.Small;
            float num = 0f;
            string blueprintStats = "RealRuins.Loading".Translate();
            switch (pageState) {
                case RuinsPageState.Idle:
                    blueprintStats = "Idle".Translate();
                    break;
                case RuinsPageState.LoadingHeader:
                    blueprintStats = "Searching for blueprints...";
                    break;
                case RuinsPageState.LoadedHeader:
                case RuinsPageState.LoadingBlueprints:
                    blueprintStats = "Loading blueprints: " + blueprintsLoadedCount + "/" + blueprintsTotalCount;
                    break;
                case RuinsPageState.ProcessingBlueprints:
                    blueprintStats = "Processing blueprints: " + blueprintsProcessedCount + "/" + blueprintsTotalCount;
                    break;
                case RuinsPageState.Completed:
                    blueprintStats = "Completed";
                    break;
            }

            Widgets.Label(new Rect(0f, num, 350f, 30f), blueprintStats);

            Rect rect3 = new Rect(390f, num, 200f, 30f);
            if (Widgets.ButtonText(rect3, "RealRuins.LoadBlueprints".Translate())) {
                StartLoadingList();
            }

            num += 45;

            Rect rect4 = new Rect(0, num, 200f, 30f);
            if (Widgets.ButtonText(rect4, "RealRuins.Close".Translate())) {
                Find.WindowStack.TryRemove(this);
            }

            Widgets.CheckboxLabeled(new Rect(20, 120, 300, 20), "Strict filtering", ref strict);


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
                }
            });
        }

        private void LoadItems() { 
            if (pageState == RuinsPageState.LoadedHeader) {
                pageState = RuinsPageState.LoadingBlueprints;

                SnapshotManager manager = new SnapshotManager();
                manager.Progress = delegate (int progress, int total) {
                    blueprintsLoadedCount = progress;
                    blueprintsTotalCount = total;
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
                string gamePath = string.Format("{0}-{1}-{2}", Find.World.info.seedString.SanitizeForFileSystem(), Find.World.info.initialMapSize.x, (int)(Find.World.PlanetCoverage * 100));
                manager.AggressiveLoadSnaphotsFromList(blueprintIds, gamePath: gamePath, loadIfExists: false);
            }
        }

        private void CreateSites() {
            foreach (PlanetTileInfo t in mapTiles) {
                if (strict) {
                    if (t.originX == 0 && t.originZ == 0) continue;
                }
                string gamePath = string.Format("{0}-{1}-{2}", Find.World.info.seedString.SanitizeForFileSystem(), Find.World.info.initialMapSize.x, (int)(Find.World.PlanetCoverage * 100));
                RealRuinsPOIFactory.CreatePOI(t, gamePath);
                blueprintsProcessedCount ++;
            }
        }

        public override void PreOpen() {
            base.PreOpen();
            

        }

        public void SetupRealRuins() {
            Find.WindowStack.Add(this);
        }
    }
}
