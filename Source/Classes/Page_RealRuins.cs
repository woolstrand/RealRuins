using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

using Verse;
using RimWorld;

namespace RealRuins {
    enum RuinsPageState {
        Idle = 0,
        LoadingHeader,
        LoadedHeader,
        LoadingBlueprints,
        Completed
    }

    class Page_RealRuins : Page {

        public override string PageTitle => "RealRuinsSetup".Translate();

        private RuinsPageState pageState = RuinsPageState.Idle;
        private int blueprintsTotalCount = 0;
        private int blueprintsLoadedCount = 0;
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
            if (pageState != RuinsPageState.Idle && pageState != RuinsPageState.LoadedHeader) {
                blueprintStats = " " + blueprintsLoadedCount + "/" + blueprintsTotalCount;
            } else if (pageState == RuinsPageState.LoadedHeader) {
                blueprintStats = " " + blueprintsTotalCount + "blueprints found";
            }

            Widgets.Label(new Rect(0f, num, 350f, 30f), "RealRuins.BlueprintsLoaderCount".Translate() + blueprintStats);

            Rect rect3 = new Rect(390f, num, 200f, 30f);
            if (Widgets.ButtonText(rect3, "RealRuins.LoadBlueprints".Translate())) {
                if (pageState == RuinsPageState.LoadedHeader) {
                    pageState = RuinsPageState.LoadingBlueprints;

                    SnapshotManager manager = new SnapshotManager();
                    manager.Progress = delegate (int progress, int total) {
                        blueprintsLoadedCount = progress;
                        blueprintsTotalCount = total;
                    };

                    manager.Completion = delegate (bool success) {
                        Debug.Message("Completed loadingm creating sites.");
                        pageState = RuinsPageState.Completed;
                        CreateSites();
                    };

                    Debug.Message("Loading blueprints one by one...");
                    manager.AggressiveLoadSnaphotsFromList(blueprintIds, "test");
                }
            }

            num += 45;

            Rect rect4 = new Rect(0, num, 200f, 30f);
            if (Widgets.ButtonText(rect4, "RealRuins.Close".Translate())) {
                Find.WindowStack.TryRemove(this);
            }

            Widgets.CheckboxLabeled(new Rect(20, 120, 300, 20), "Strict filtering", ref strict);


        }

        private void CreateSites() {
            foreach (PlanetTileInfo t in mapTiles) {
                if (strict) {
                    if (t.originX == 0 && t.originZ == 0) continue;
                }
                RealRuinsPOIFactory.CreatePOI(t, "test");
            }
        }

        public override void PreOpen() {
            base.PreOpen();
            pageState = RuinsPageState.LoadingHeader;
            Debug.Message("Loading list for seed: {0}", Find.World.info.seedString);
            service.LoadAllMapsForSeed(Find.World.info.seedString, (int)Find.World.info.initialMapSize.x, (int)(Find.World.PlanetCoverage * 100), delegate (bool success, List<PlanetTileInfo> mapTiles) {
                if (success) {
                    this.mapTiles = mapTiles;
                    blueprintIds = new List<string>();
                    foreach (PlanetTileInfo t in mapTiles) {
                        blueprintIds.Add(t.mapId);
                    }

                    blueprintsTotalCount = blueprintIds.Count;
                    pageState = RuinsPageState.LoadedHeader;
                    Debug.Message("Loaded list of snapshot names, {0} elements", blueprintsTotalCount);
                }
            });

        }

        public void SetupRealRuins() {
            Find.WindowStack.Add(this);
        }
    }
}
