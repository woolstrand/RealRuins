using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using Verse;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using LudeonTK;

namespace RealRuins
{
    /// <summary>
    /// Debug actions menu for RealRuins mod.
    /// Provides debug utilities accessible from the world map debug menu.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class DebugActionsRealRuins
    {
        [DebugAction("RealRuins", "Focus on tile", allowedGameStates = AllowedGameStates.PlayingOnWorld)]
        public static void ShowFocusTileDialog()
        {
            if (Find.World == null)
            {
                Messages.Message("RealRuins: Not in world view", MessageTypeDefOf.RejectInput);
                return;
            }

            // Create a dialog that asks for tile ID input
            Find.WindowStack.Add(new Dialog_FocusTile());
        }

        // Same setting as the checkbox on the mod's debug settings page
        // AllowedGameStates flags must all match, so this is in-game only; at the main menu use the debug settings page
        [DebugAction("RealRuins", "Toggle ruins wealth log", allowedGameStates = AllowedGameStates.Playing)]
        public static void ToggleRuinsWealthLog()
        {
            RealRuins_ModSettings.debugLogRuinsWealth = !RealRuins_ModSettings.debugLogRuinsWealth;
            LoadedModManager.GetMod<RealRuins_Mod>().WriteSettings();
            Messages.Message("RealRuins: ruins wealth log " + (RealRuins_ModSettings.debugLogRuinsWealth ? "enabled" : "disabled"), MessageTypeDefOf.NeutralEvent, historical: false);
        }

        // Generates maps on random tiles, measures the scattered ruins (see RuinsWealthReport) and logs the distribution.
        [DebugAction("RealRuins", "Sample ruins wealth", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static List<DebugActionNode> SampleRuinsWealth()
        {
            var list = new List<DebugActionNode>();
            foreach (int count in new[] { 10, 25, 50 })
            {
                list.Add(new DebugActionNode(count + " maps", DebugActionType.Action, () => RuinsWealthSampler.Run(count)));
            }
            return list;
        }

        // Vanilla "Generate quests x#" -> "*Natural random" rolls the quest type once and generates it N times.
        // This rolls the storyteller's random quest slot N times instead and only counts the outcomes (nothing is spawned).
        [DebugAction("RealRuins", "Simulate random quest rolls", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static List<DebugActionNode> SimulateRandomQuestRolls()
        {
            var list = new List<DebugActionNode>();
            foreach (int count in new[] { 100, 1000, 5000 })
            {
                list.Add(new DebugActionNode(count.ToString(), DebugActionType.Action, () => QuestRollSimulator.Run(count)));
            }
            return list;
        }
    }

    /// <summary>
    /// Repeats what StorytellerComp_RandomQuest does when its slot fires: GenerateParms rolls a natural random quest
    /// (NaturalRandomQuestChooser), then one incident of the GiveQuest category is picked by weight. Vanilla only has
    /// GiveQuest_Random there, which gives the rolled quest; anything else in the category (e.g. Quest_PristineRuins) competes with it.
    /// History (refire days, recent-pick weighting) is read as of now and not advanced between rolls,
    /// so the result is the chance per quest slot at the current moment.
    /// </summary>
    public static class QuestRollSimulator
    {
        private static readonly MethodInfo UsableIncidentsMethod = AccessTools.Method(typeof(StorytellerComp), "UsableIncidentsInCategory", new[] { typeof(IncidentCategoryDef), typeof(IncidentParms) });
        private static readonly MethodInfo IncidentChanceFinalMethod = AccessTools.Method(typeof(StorytellerComp), "IncidentChanceFinal");

        public static void Run(int count)
        {
            IIncidentTarget target = Find.World;
            StorytellerComp comp = Find.Storyteller.storytellerComps.FirstOrDefault(c => c.GetType() == typeof(StorytellerComp_RandomQuest));
            if (comp == null)
            {
                Messages.Message("RealRuins: current storyteller has no random quest comp", MessageTypeDefOf.RejectInput, historical: false);
                return;
            }

            var tally = new Dictionary<string, int>();
            for (int i = 0; i < count; i++)
            {
                IncidentParms parms = comp.GenerateParms(IncidentCategoryDefOf.GiveQuest, target);
                var incidents = (IEnumerable<IncidentDef>)UsableIncidentsMethod.Invoke(comp, new object[] { IncidentCategoryDefOf.GiveQuest, parms });
                string outcome;
                if (!incidents.TryRandomElementByWeight(def => (float)IncidentChanceFinalMethod.Invoke(comp, new object[] { def, target }), out IncidentDef incident))
                {
                    outcome = "(nothing fires)";
                }
                else if (incident.Worker is IncidentWorker_GiveQuest && incident.questScriptDef == null)
                {
                    outcome = "quest: " + (parms.questScriptDef?.defName ?? "(none rolled)");
                }
                else
                {
                    outcome = "incident: " + incident.defName;
                }
                tally[outcome] = tally.TryGetValue(outcome, out int n) ? n + 1 : 1;
            }

            var sb = new StringBuilder();
            sb.AppendLine(string.Format("RealRuins: {0} random quest slot rolls ({1}, day {2}, {3:F0} points):", count, comp, GenDate.DaysPassedSinceSettle, StorytellerUtility.DefaultThreatPointsNow(target)));
            foreach (var pair in tally.OrderByDescending(p => p.Value))
            {
                sb.AppendLine(string.Format("{0,6:P1}  {1,5}  {2}", (float)pair.Value / count, pair.Value, pair.Key));
            }
            Log.Message(sb.ToString());

            int ruins = tally.Where(p => p.Key == "incident: Quest_PristineRuins" || p.Key == "quest: RealRuins_AbandonedBase").Sum(p => p.Value);
            Messages.Message(string.Format("RealRuins: pristine ruins in {0} of {1} quest rolls ({2:P1}). Full table in the log.", ruins, count, (float)ruins / count), MessageTypeDefOf.NeutralEvent, historical: false);
        }
    }

    /// <summary>
    /// Generates full maps for temporary player settlements on random settle-able tiles (like the vanilla "Run map generator"
    /// debug action), so regular ruins scatter runs as on a new colony, then removes each map and its settlement.
    /// The wealth log is on for the duration; every map logs its MAP line and the end logs the distribution over all maps.
    /// </summary>
    public static class RuinsWealthSampler
    {
        public static void Run(int count)
        {
            bool wasEnabled = RealRuins_ModSettings.debugLogRuinsWealth;
            int logLevel = RealRuins_ModSettings.logLevel;
            RealRuins_ModSettings.debugLogRuinsWealth = true;
            //every map logs hundreds of lines at log level "all", and the game stops logging after 10000 messages; the report itself ignores the level
            RealRuins_ModSettings.logLevel = 2;
            RuinsWealthReport.samples = new List<MapWealthSample>();
            Map home = Find.CurrentMap;
            int failed = 0;

            for (int i = 0; i < count; i++)
            {
                LongEventHandler.QueueLongEvent(() => { if (!GenerateAndRemoveMap()) failed++; }, string.Format("Sampling ruins wealth {0}/{1}", i + 1, count), false, null);
            }
            LongEventHandler.QueueLongEvent(() =>
            {
                LogDistribution(RuinsWealthReport.samples, count, failed);
                RuinsWealthReport.samples = null;
                RealRuins_ModSettings.debugLogRuinsWealth = wasEnabled;
                RealRuins_ModSettings.logLevel = logLevel;
                if (home != null && Find.Maps.Contains(home)) Current.Game.CurrentMap = home;
            }, "Sampling ruins wealth", false, null);
        }

        private static bool GenerateAndRemoveMap()
        {
            PlanetTile tile = TileFinder.RandomSettlementTileFor(Faction.OfPlayer, mustBeAutoChoosable: false);
            if (!tile.Valid) return false;

            MapParent parent = (MapParent)WorldObjectMaker.MakeWorldObject(WorldObjectDefOf.Settlement);
            parent.Tile = tile;
            parent.SetFaction(Faction.OfPlayer);
            Find.WorldObjects.Add(parent);
            Map map = null;
            Log.ResetMessageCount(); //vanilla warnings from map generation alone can reach the limit over a long run
            int samplesBefore = RuinsWealthReport.samples?.Count ?? 0;
            long started = CostTiming.Now;
            try
            {
                map = MapGenerator.GenerateMap(Find.World.info.initialMapSize, parent, parent.MapGeneratorDef);
                //map init leaves work for the end of the long event (map drawer, pawn spawn setup); removing the map before it runs
                //breaks MapDrawer.Dispose and leaves callbacks pointing at a removed map index
                LongEventHandler.ForceExecuteToExecuteWhenFinished();
                if (RuinsWealthReport.samples != null && RuinsWealthReport.samples.Count > samplesBefore)
                {
                    RuinsWealthReport.samples[RuinsWealthReport.samples.Count - 1].mapGenMs = CostTiming.Ms(CostTiming.Now - started);
                }
                return true;
            }
            catch (Exception e)
            {
                Log.Warning("RealRuins: wealth sample map generation failed: " + e);
                return false;
            }
            finally
            {
                try
                {
                    if (map != null && Find.Maps.Contains(map)) Current.Game.DeinitAndRemoveMap(map, false);
                    if (!parent.Destroyed) parent.Destroy();
                }
                catch (Exception e)
                {
                    Log.Warning("RealRuins: could not remove wealth sample map: " + e);
                }
            }
        }

        private static void LogDistribution(List<MapWealthSample> samples, int requested, int failed)
        {
            if (samples == null || samples.Count == 0)
            {
                Messages.Message("RealRuins: no maps sampled", MessageTypeDefOf.RejectInput, historical: false);
                return;
            }
            var withRuins = samples.Where(s => s.chunks > 0).ToList();
            float estimate = withRuins.Sum(s => s.estimate);
            float legacy = withRuins.Sum(s => s.legacyEstimate);
            var sb = new StringBuilder();
            sb.AppendFormat("SAMPLE maps={0}/{1} failed={2} withRuins={3} actual/estimate={4:F2} estimate/legacy={5:F2}",
                samples.Count, requested, failed, withRuins.Count,
                estimate > 0 ? withRuins.Sum(s => s.actual) / estimate : 0,
                legacy > 0 ? estimate / legacy : 0);
            sb.AppendLine();
            sb.Append("  maps with ruins, min / p25 / median / p75 / p90 / max / mean:");
            AppendPercentiles(sb, "actual", withRuins.Select(s => s.actual));
            AppendPercentiles(sb, "estimate", withRuins.Select(s => s.estimate));
            AppendPercentiles(sb, "legacy", withRuins.Select(s => s.legacyEstimate));
            AppendPercentiles(sb, "max chunk", withRuins.Select(s => s.maxChunk));
            AppendPercentiles(sb, "chunks", withRuins.Select(s => (float)s.chunks));
            sb.AppendLine();
            sb.Append("  generation time, ms, all maps:");
            AppendPercentiles(sb, "map", samples.Select(s => (float)s.mapGenMs));
            AppendPercentiles(sb, "ruins scatter", samples.Select(s => (float)s.scatterMs));
            AppendPercentiles(sb, "new cost", samples.Select(s => (float)s.costMs));
            AppendPercentiles(sb, "legacy cost", samples.Select(s => (float)s.legacyCostMs));
            Debug.Report(Debug.Wealth, "{0}", sb.ToString());
            Messages.Message(string.Format("RealRuins: sampled {0} maps ({1} with ruins), median ruins wealth {2:F0}. Details in the log.",
                samples.Count, withRuins.Count, Percentile(withRuins.Select(s => s.actual).OrderBy(v => v).ToList(), 0.5f)), MessageTypeDefOf.NeutralEvent, historical: false);
        }

        private static void AppendPercentiles(StringBuilder sb, string label, IEnumerable<float> values)
        {
            var sorted = values.OrderBy(v => v).ToList();
            sb.AppendLine();
            if (sorted.Count == 0)
            {
                sb.AppendFormat("  {0}: -", label);
                return;
            }
            sb.AppendFormat("  {0}: {1:F0} / {2:F0} / {3:F0} / {4:F0} / {5:F0} / {6:F0} / {7:F0}", label,
                sorted[0], Percentile(sorted, 0.25f), Percentile(sorted, 0.5f), Percentile(sorted, 0.75f), Percentile(sorted, 0.9f), sorted[sorted.Count - 1], sorted.Average());
        }

        // sorted must be in ascending order
        private static float Percentile(List<float> sorted, float p)
        {
            if (sorted.Count == 0) return 0;
            return sorted[(int)Math.Min(sorted.Count - 1, Math.Round(p * (sorted.Count - 1)))];
        }
    }

    /// <summary>
    /// Dialog for entering a tile ID to focus on.
    /// </summary>
    public class Dialog_FocusTile : Window
    {
        private string tileIdBuffer = "";
        private int tileId = -1;

        public override Vector2 InitialSize => new Vector2(300f, 250f);

        public Dialog_FocusTile()
        {
            this.doCloseButton = true;
            this.doCloseX = true;
            this.absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);

            listing.Label("Enter tile ID:");
            listing.TextFieldNumeric(ref tileId, ref tileIdBuffer, 0f);

            listing.Gap(10f);

            if (listing.ButtonText("Focus Tile"))
            {
                if (tileId >= 0 && tileId < Find.WorldGrid.TilesCount)
                {
                    RealRuinsDebugUtils.FocusTile(tileId);
                    this.Close();
                }
                else
                {
                    Messages.Message($"RealRuins: Invalid tile ID {tileId}. Valid range: 0-{Find.WorldGrid.TilesCount - 1}", MessageTypeDefOf.RejectInput);
                }
            }

            if (listing.ButtonText("Cancel"))
            {
                this.Close();
            }

            listing.End();
        }
    }
}
