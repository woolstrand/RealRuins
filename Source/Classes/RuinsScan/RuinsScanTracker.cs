using System;
using System.Linq;
using System.Threading;
using RimWorld;
using RimWorld.QuestGen;
using Verse;

namespace RealRuins {
    // A scanning pawn caught a signal: picks a blueprint on a background thread, then makes the scanned ruins quest.
    // The pending signal is saved, so a save made while the search runs restarts it on load.
    public class RuinsScanTracker : GameComponent {
        public static RuinsScanTracker Instance => Current.Game?.GetComponent<RuinsScanTracker>();

        // Dev action: the next scan roll succeeds
        public static bool debugForceDiscovery = false;

        private bool signalPending = false;
        private string scannerName;
        private string scanBuildingLabel;
        private int mapId = -1;
        private RuinsScanDef scanDef;

        private bool searchRunning = false; // not saved: a loaded game starts its own search

        public bool SignalPending => signalPending;

        public RuinsScanTracker(Game game) {
        }

        public void SignalFound(Pawn pawn, Thing building) {
            if (signalPending) {
                return;
            }
            signalPending = true;
            scannerName = pawn.LabelShortCap;
            scanBuildingLabel = building.def.label;
            mapId = building.Map.uniqueID;
            scanDef = building.TryGetComp<CompRuinsScanner>().ScanDef;

            // One discovery per scan order: the player switches scanning on again for the next one
            foreach (Building scanner in CompRuinsScanner.ScannersOn(building.Map)) {
                scanner.GetComp<CompRuinsScanner>().scanningEnabled = false;
            }

            Debug.Log(Debug.RuinsScan, "{0} found ruins at {1} ({2})", scannerName, building, scanDef);
            Messages.Message(scanDef.foundMessage.Formatted(pawn.Named("PAWN"), building.def.label.Named("BUILDING")), building, MessageTypeDefOf.PositiveEvent);
            StartSearch();
        }

        public override void LoadedGame() {
            base.LoadedGame();
            if (signalPending) {
                StartSearch();
            }
        }

        private void StartSearch() {
            if (searchRunning) {
                return;
            }
            searchRunning = true;
            if (scanDef == null) {
                scanDef = DefDatabase<RuinsScanDef>.AllDefs.First(); // def gone from a save made with another mod version
            }

            // Engine state is read here, on the main thread; the thread only touches blueprint files and defs
            RuinsScanDef settings = scanDef;
            int minArea = settings.minimumArea;
            int minWealth = (int)Math.Min(settings.minimumWealth, RuinsWealthLimits.For(RuinsKind.AbandonedBase));
            int attempts = settings.maxBlueprintAttempts;
            Game game = Current.Game;
            CoroutineManager mainThread = CoroutineManager.Instance;

            new Thread(() => {
                string filename = null;
                int cost = 0;
                try {
                    Blueprint bp = BlueprintFinder.FindRandomBlueprintWithParameters(out filename, minArea, 0.01f, minWealth, maxAttemptsCount: attempts);
                    if (bp != null) {
                        cost = (int)Math.Min(bp.totalCost, int.MaxValue - 1);
                    }
                } catch (Exception e) {
                    // an unhandled exception on a raw thread would take the whole game down
                    Debug.Error(Debug.RuinsScan, "Blueprint search failed: {0}", e);
                    filename = null;
                }
                mainThread.RunOnMainThread(() => {
                    if (Current.Game != game) {
                        return; // game was left or reloaded; a loaded game restarts the search itself
                    }
                    searchRunning = false;
                    CreateQuest(filename, cost);
                });
            }) { IsBackground = true, Name = "RealRuins ruins scan" }.Start();
        }

        private void CreateQuest(string blueprintName, int blueprintCost) {
            Map map = Find.Maps.FirstOrDefault(m => m.uniqueID == mapId) ?? Find.AnyPlayerHomeMap;
            string scanner = scannerName;
            string scanBuilding = scanBuildingLabel;
            QuestScriptDef script = scanDef.questScript;
            signalPending = false;
            scanDef = null;
            scannerName = null;
            scanBuildingLabel = null;
            mapId = -1;
            Debug.Log(Debug.RuinsScan, "Blueprint search finished: {0} (cost {1})", blueprintName ?? "none", blueprintCost);

            IIncidentTarget target = (IIncidentTarget)map ?? Find.World;
            Slate slate = new Slate();
            slate.Set("points", Math.Max(1f, StorytellerUtility.DefaultThreatPointsNow(target)));
            if (map != null) {
                slate.Set("map", map); // site tile is picked near the scanning colony
            }
            slate.Set("scannerName", scanner ?? "RealRuins_RuinsScan_UnknownScanner".Translate().ToString());
            slate.Set("scanBuilding", scanBuilding ?? "RealRuins_RuinsScan_UnknownBuilding".Translate().ToString());
            // No blueprint (all candidates rejected): the quest still runs and the map generator picks one, as with the random quest
            if (blueprintName != null) {
                slate.Set("blueprintName", blueprintName);
                slate.Set("blueprintCachedCost", blueprintCost);
            }

            if (!script.CanRun(slate, target)) {
                Debug.Warning(Debug.RuinsScan, "Scanned ruins quest can't run (no site tile?)");
                Messages.Message("RealRuins_RuinsScan_SignalLost".Translate(), MessageTypeDefOf.NeutralEvent);
                return;
            }
            Quest quest = QuestUtility.GenerateQuestAndMakeAvailable(script, slate);
            // Like vanilla's long range mineral scanner find: a "good news" letter with the quest's name and story,
            // not the standard "Quest active" one
            Find.LetterStack.ReceiveLetter(quest.name, quest.description, LetterDefOf.PositiveEvent, null, null, quest);
        }

        public override void ExposeData() {
            base.ExposeData();
            Scribe_Values.Look(ref signalPending, "signalPending", false);
            Scribe_Values.Look(ref scannerName, "scannerName");
            Scribe_Values.Look(ref scanBuildingLabel, "scanBuildingLabel");
            Scribe_Values.Look(ref mapId, "mapId", -1);
            Scribe_Defs.Look(ref scanDef, "scanDef");
        }
    }
}
