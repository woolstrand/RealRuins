using System;
using System.Collections.Generic;
using Verse;
using RimWorld;
using UnityEngine;

namespace RealRuins.Settings
{
    public class AdvancedSettingsPage : SettingsPage
    {
        private static readonly string[] CaravanReformOptions = {
            "RealRuins.Reform.Automatic",
            "RealRuins.Reform.Instant",
            "RealRuins.Reform.Manual"
        };

        private static readonly string[] LogLevelOptions = {
            "RealRuins.LogLevel.All",
            "RealRuins.LogLevel.Warnings",
            "RealRuins.LogLevel.Errors"
        };

        public override string TabLabel => "RealRuins.AdvancedSettings".Translate();

        public override void Draw(Rect rect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(rect);

            GameFont font = Text.Font;
            Text.Font = GameFont.Medium;
            listing.Label("RealRuins.AdvancedSettings".Translate());
            Text.Font = font;
            listing.GapLine();

            // Force Multiplier
            listing.Label(
                "RealRuins.ForceMultiplier".Translate() + ": x" +
                RealRuins_ModSettings.forceMultiplier.ToString("F"),
                -1,
                "RealRuins.ForceMultiplierTT".Translate());

            RealRuins_ModSettings.forceMultiplier =
                listing.Slider(RealRuins_ModSettings.forceMultiplier, 0.0f, 2.0f);

            listing.Gap(15);

            // Wealth Cost Cap
            string wealthCapStr = "∞";
            if (RealRuins_ModSettings.ruinsCostCap < 9.9999e8)
            {
                wealthCapStr = RealRuins_ModSettings.ruinsCostCap.ToString();
            }

            listing.Label(
                "RealRuins.AbsoluteWealthCap".Translate() + ": " + wealthCapStr,
                -1,
                "RealRuins.AbsoluteWealthCapTT".Translate());

            RealRuins_ModSettings.ruinsCostCap =
                (float)System.Math.Exp(listing.Slider(
                    (float)System.Math.Log(RealRuins_ModSettings.ruinsCostCap),
                    6.908f,
                    (float)System.Math.Log(1.0e9)));

            listing.Gap(15);

            // Caravan Reform Type
            Rect ttrect = listing.GetRect(30f);
            Widgets.Label(
                ttrect.LeftHalf().ContractedBy(0, 5),
                "RealRuins.CaravanReformType".Translate());

            bool caravanResult = Widgets.ButtonText(
                ttrect.RightHalf(),
                CaravanReformOptions[System.Math.Min(2, RealRuins_ModSettings.caravanReformType)].Translate());

            if (caravanResult)
            {
                List<FloatMenuOption> options = new List<FloatMenuOption>();
                for (int i = 0; i < 3; i++)
                {
                    string text = CaravanReformOptions[i].Translate();
                    int value = i;
                    FloatMenuOption item = new FloatMenuOption(text, delegate
                    {
                        RealRuins_ModSettings.caravanReformType = value;
                    });
                    options.Add(item);
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }

            TooltipHandler.TipRegion(ttrect, "RealRuins.CaravanReformTooltip".Translate());

            listing.Gap(25);

            // Log Level
            Rect loglevelRect = listing.GetRect(30f);
            Widgets.Label(
                loglevelRect.LeftHalf().ContractedBy(0, 5),
                "RealRuins.LogLevel".Translate());

            bool logLevelResult = Widgets.ButtonText(
                loglevelRect.RightHalf(),
                LogLevelOptions[System.Math.Min(2, RealRuins_ModSettings.logLevel)].Translate());

            if (logLevelResult)
            {
                List<FloatMenuOption> options = new List<FloatMenuOption>();
                for (int i = 0; i < 3; i++)
                {
                    string text = LogLevelOptions[i].Translate();
                    int value = i;
                    FloatMenuOption item = new FloatMenuOption(text, delegate
                    {
                        RealRuins_ModSettings.logLevel = value;
                    });
                    options.Add(item);
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }

            listing.Gap(30);

            // Reset Button
            if (listing.ButtonText("RealRuins_ModOptions_Reset".Translate(), null))
            {
                RealRuins_ModSettings.Reset();
            }

            listing.End();
        }
    }
}
