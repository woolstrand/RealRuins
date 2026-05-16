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

        private string spawnBlacklistBuffer;
        private string materialBlacklistBuffer;
        private string fallbackMaterialBuffer;

        public AdvancedSettingsPage() {
            spawnBlacklistBuffer = RealRuins_ModSettings.spawnBlacklist ?? "";
            materialBlacklistBuffer = RealRuins_ModSettings.materialBlacklist ?? "";
            fallbackMaterialBuffer = RealRuins_ModSettings.fallbackMaterial ?? "";
        }

        public override string TabLabel => "RealRuins.AdvancedSettings".Translate();

        public override float ContentHeight => 615f;

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
            Rect forceMultiplierRect = listing.GetRect(25f);
            Widgets.Label(
                forceMultiplierRect.LeftHalf().ContractedBy(0, 0),
                "RealRuins.ForceMultiplier".Translate() + ": x" + RealRuins_ModSettings.forceMultiplier.ToString("F"));
            
            RealRuins_ModSettings.forceMultiplier =
                Widgets.HorizontalSlider(forceMultiplierRect.RightHalf().ContractedBy(0, 3), RealRuins_ModSettings.forceMultiplier, 0.0f, 2.0f);

            TooltipHandler.TipRegion(forceMultiplierRect, "RealRuins.ForceMultiplierTT".Translate());
            listing.Gap(15);

            // Wealth Cost Cap
            string wealthCapStr = RealRuins_ModSettings.ruinsCostCap < 9.9999e8 ? RealRuins_ModSettings.ruinsCostCap.ToString("#,0") : (string)"RealRuins.Unlimited".Translate();
            
            Rect wealthCapRect = listing.GetRect(25f);
            Widgets.Label(
                wealthCapRect.LeftHalf().ContractedBy(0, 0),
                "RealRuins.AbsoluteWealthCap".Translate() + ": " + wealthCapStr);

            RealRuins_ModSettings.ruinsCostCap =
                (float)System.Math.Exp(Widgets.HorizontalSlider(
                    wealthCapRect.RightHalf().ContractedBy(0, 3),
                    (float)System.Math.Log(RealRuins_ModSettings.ruinsCostCap),
                    6.908f,
                    (float)System.Math.Log(1.0e9)));

            TooltipHandler.TipRegion(wealthCapRect, "RealRuins.AbsoluteWealthCapTT".Translate());
            listing.Gap(15);

            // Caravan Reform Type
            Rect caravanRect = listing.GetRect(25f);
            Widgets.Label(
                caravanRect.LeftHalf().ContractedBy(0, 0),
                "RealRuins.CaravanReformType".Translate());

            bool caravanResult = Widgets.ButtonText(
                caravanRect.RightHalf().ContractedBy(0, 3),
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

            TooltipHandler.TipRegion(caravanRect, "RealRuins.CaravanReformTooltip".Translate());
            listing.Gap(15);

            listing.CheckboxLabeled(
                "RealRuins.UseForcesGenerationV2".Translate(),
                ref RealRuins_ModSettings.useRuinsForcesGenerationV2,
                "RealRuins.UseForcesGenerationV2TT".Translate());

            listing.Gap(15);

            listing.CheckboxLabeled(
                "RealRuins.DisableFriendlyRaids".Translate(),
                ref RealRuins_ModSettings.disableFriendlyRaids,
                "RealRuins.DisableFriendlyRaidsTT".Translate());

            listing.Gap(15);

            listing.CheckboxLabeled(
                "RealRuins.EnableAbandonedRuinsFoundEvent".Translate(),
                ref RealRuins_ModSettings.enableAbandonedRuinsFoundEvent,
                "RealRuins.EnableAbandonedRuinsFoundEventTT".Translate());

            listing.Gap(15);

            listing.CheckboxLabeled(
                "RealRuins.EnableCaravanFoundRuinsEvent".Translate(),
                ref RealRuins_ModSettings.enableCaravanFoundRuinsEvent,
                "RealRuins.EnableCaravanFoundRuinsEventTT".Translate());

            listing.Gap(15);

            Rect spawnBlacklistRect = listing.GetRect(25f);
            Widgets.Label(
                spawnBlacklistRect.LeftHalf().ContractedBy(0, 0),
                "RealRuins.SpawnBlacklist".Translate());
            
            spawnBlacklistBuffer = Widgets.TextField(spawnBlacklistRect.RightHalf().ContractedBy(0, 3), spawnBlacklistBuffer);
            if (spawnBlacklistBuffer != RealRuins_ModSettings.spawnBlacklist)
            {
                RealRuins_ModSettings.spawnBlacklist = spawnBlacklistBuffer;
            }
            
            TooltipHandler.TipRegion(spawnBlacklistRect, "RealRuins.SpawnBlacklistTT".Translate());

            listing.Gap(15);

            Rect materialBlacklistRect = listing.GetRect(25f);
            Widgets.Label(
                materialBlacklistRect.LeftHalf().ContractedBy(0, 0),
                "RealRuins.MaterialBlacklist".Translate());
            
            materialBlacklistBuffer = Widgets.TextField(materialBlacklistRect.RightHalf().ContractedBy(0, 3), materialBlacklistBuffer);
            if (materialBlacklistBuffer != RealRuins_ModSettings.materialBlacklist)
            {
                RealRuins_ModSettings.materialBlacklist = materialBlacklistBuffer;
            }
            
            TooltipHandler.TipRegion(materialBlacklistRect, "RealRuins.MaterialBlacklistTT".Translate());

            listing.Gap(15);

            Rect fallbackMaterialRect = listing.GetRect(25f);
            Widgets.Label(
                fallbackMaterialRect.LeftHalf().ContractedBy(0, 0),
                "RealRuins.FallbackMaterial".Translate());
            
            fallbackMaterialBuffer = Widgets.TextField(fallbackMaterialRect.RightHalf().ContractedBy(0, 3), fallbackMaterialBuffer);
            if (fallbackMaterialBuffer != RealRuins_ModSettings.fallbackMaterial)
            {
                RealRuins_ModSettings.fallbackMaterial = fallbackMaterialBuffer;
            }
            
            TooltipHandler.TipRegion(fallbackMaterialRect, "RealRuins.FallbackMaterialTT".Translate());

            listing.End();
        }
    }
}
