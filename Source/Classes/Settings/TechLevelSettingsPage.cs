using System;
using System.Collections.Generic;
using Verse;
using RimWorld;
using UnityEngine;

namespace RealRuins.Settings
{
    public class TechLevelSettingsPage : SettingsPage
    {
        // TechLevel is a fixed game enum, mods can't add levels. Undefined and Animal are not eras, and Archotech as a cap limits nothing.
        private static readonly TechLevel[] CapLevels = {
            TechLevel.Neolithic,
            TechLevel.Medieval,
            TechLevel.Industrial,
            TechLevel.Spacer,
            TechLevel.Ultra
        };

        public override string TabLabel => "RealRuins.TechLimit.Caption".Translate();

        public override float ContentHeight => 400f;

        public override void Draw(Rect rect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(rect);

            GameFont font = Text.Font;
            Text.Font = GameFont.Medium;
            listing.Label("RealRuins.TechLimit.Caption".Translate());
            Text.Font = font;
            listing.GapLine();

            listing.CheckboxLabeled(
                "RealRuins.TechLimit.Enabled".Translate(),
                ref RealRuins_ModSettings.techLimitEnabled,
                "RealRuins.TechLimit.EnabledTT".Translate());

            listing.Gap(15f);

            bool enabled = RealRuins_ModSettings.techLimitEnabled;
            Color prevColor = GUI.color;
            if (!enabled) GUI.color = Color.gray;

            // Maximum tech level
            Rect levelRect = listing.GetRect(25f);
            Widgets.Label(levelRect.LeftHalf(), "RealRuins.TechLimit.MaxLevel".Translate());
            if (Widgets.ButtonText(levelRect.RightHalf().ContractedBy(0, 3), LevelLabel(RealRuins_ModSettings.techLimitMaxLevel), active: enabled) && enabled)
            {
                List<FloatMenuOption> options = new List<FloatMenuOption>();
                foreach (TechLevel level in CapLevels)
                {
                    TechLevel value = level;
                    options.Add(new FloatMenuOption(LevelLabel(level), () => RealRuins_ModSettings.techLimitMaxLevel = value));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }
            TooltipHandler.TipRegion(levelRect, "RealRuins.TechLimit.MaxLevelTT".Translate());

            listing.Gap(15f);

            // Capping strategy
            Rect strategyRect = listing.GetRect(25f);
            Widgets.Label(strategyRect.LeftHalf(), "RealRuins.TechLimit.Strategy".Translate());
            if (Widgets.ButtonText(strategyRect.RightHalf().ContractedBy(0, 3), StrategyLabel(RealRuins_ModSettings.techLimitStrict), active: enabled) && enabled)
            {
                Find.WindowStack.Add(new FloatMenu(new List<FloatMenuOption> {
                    new FloatMenuOption(StrategyLabel(true), () => RealRuins_ModSettings.techLimitStrict = true),
                    new FloatMenuOption(StrategyLabel(false), () => RealRuins_ModSettings.techLimitStrict = false),
                }));
            }
            TooltipHandler.TipRegion(strategyRect, "RealRuins.TechLimit.StrategyTT".Translate());

            listing.Gap(15f);

            // What to limit
            listing.Label("RealRuins.TechLimit.AppliesTo".Translate());
            ScopeCheckbox(listing, "RealRuins.TechLimit.Ruins", ref RealRuins_ModSettings.techLimitRuins, enabled);
            ScopeCheckbox(listing, "RealRuins.TechLimit.POIs", ref RealRuins_ModSettings.techLimitPOIs, enabled);
            ScopeCheckbox(listing, "RealRuins.TechLimit.Events", ref RealRuins_ModSettings.techLimitEvents, enabled);

            GUI.color = prevColor;
            listing.End();
        }

        private static void ScopeCheckbox(Listing_Standard listing, string key, ref bool value, bool enabled)
        {
            //Listing_Standard has no disabled checkbox: draw a copy and drop the click while the master toggle is off
            bool shown = value;
            listing.CheckboxLabeled(key.Translate(), ref shown, (key + "TT").Translate());
            if (enabled) value = shown;
        }

        private static string LevelLabel(TechLevel level) => level.ToStringHuman().CapitalizeFirst();

        private static string StrategyLabel(bool strict) => (strict ? "RealRuins.TechLimit.Strict" : "RealRuins.TechLimit.Limited").Translate();
    }
}
