using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using Verse;
using RimWorld;
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
