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

        // The page outlives settings import and reset, so each buffer remembers the setting value it was last synced with.
        private string spawnBlacklistBuffer, spawnBlacklistSynced;
        private string materialBlacklistBuffer, materialBlacklistSynced;
        private string fallbackMaterialBuffer, fallbackMaterialSynced;

        private static string BufferedTextField(Rect rect, string value, ref string buffer, ref string synced) {
            if (buffer == null || value != synced) {
                //first draw, or the setting was changed elsewhere (import, reset): show it instead of writing the old text back
                buffer = value ?? "";
            }
            buffer = Widgets.TextField(rect, buffer);
            synced = buffer;
            return buffer;
        }

        public override string TabLabel => "RealRuins.AdvancedSettings".Translate();

        public override float ContentHeight => RealRuins_ModSettings.ruinsScaleWithColonyWealth ? 800f : 760f;

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
            string wealthCapStr = RuinsWealthLimits.SettingCap(RuinsKind.AbandonedBase) < RuinsWealthLimits.NoLimit ? RealRuins_ModSettings.ruinsCostCap.ToString("#,0") : (string)"RealRuins.Unlimited".Translate();
            
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

            // Scaling of all ruins wealth limits
            listing.CheckboxLabeled(
                "RealRuins.ScaleWithColonyWealth".Translate(),
                ref RealRuins_ModSettings.ruinsScaleWithColonyWealth,
                "RealRuins.ScaleWithColonyWealthTT".Translate());

            if (RealRuins_ModSettings.ruinsScaleWithColonyWealth) {
                Rect referenceRect = listing.GetRect(25f);
                Widgets.Label(
                    referenceRect.LeftHalf().ContractedBy(0, 0),
                    "RealRuins.ReferenceColonyWealth".Translate() + ": " + RealRuins_ModSettings.ruinsReferenceColonyWealth.ToString("#,0"));

                RealRuins_ModSettings.ruinsReferenceColonyWealth =
                    Mathf.Round(Widgets.HorizontalSlider(referenceRect.RightHalf().ContractedBy(0, 3), RealRuins_ModSettings.ruinsReferenceColonyWealth, 25000.0f, 500000.0f) / 5000.0f) * 5000.0f;

                TooltipHandler.TipRegion(referenceRect, "RealRuins.ReferenceColonyWealthTT".Translate());
                listing.Gap(15);
            }

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
                "RealRuins.SpawnRoofs".Translate(),
                ref RealRuins_ModSettings.spawnRoofs,
                "RealRuins.SpawnRoofsTT".Translate());

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

            string caravanCapStr = RuinsWealthLimits.SettingCap(RuinsKind.Caravan) < RuinsWealthLimits.NoLimit
                ? RealRuins_ModSettings.caravanRuinsCostCap.ToString("#,0")
                : (string)"RealRuins.Unlimited".Translate();
            Rect caravanCapRect = listing.GetRect(25f);
            Widgets.Label(
                caravanCapRect.LeftHalf().ContractedBy(0, 0),
                "RealRuins.CaravanRuinsCostCap".Translate() + ": " + caravanCapStr);

            RealRuins_ModSettings.caravanRuinsCostCap =
                Mathf.Round(Widgets.HorizontalSlider(
                    caravanCapRect.RightHalf().ContractedBy(0, 3),
                    RealRuins_ModSettings.caravanRuinsCostCap,
                    0.0f,
                    RealRuins_ModSettings.caravanRuinsCostCapUnlimited) / 1000.0f) * 1000.0f;

            TooltipHandler.TipRegion(caravanCapRect, "RealRuins.CaravanRuinsCostCapTT".Translate());
            listing.Gap(15);

            string poiCapStr = RuinsWealthLimits.SettingCap(RuinsKind.AbandonedPOI) < RuinsWealthLimits.NoLimit
                ? RealRuins_ModSettings.poiRuinsCostCap.ToString("#,0")
                : (string)"RealRuins.Unlimited".Translate();
            Rect poiCapRect = listing.GetRect(25f);
            Widgets.Label(
                poiCapRect.LeftHalf().ContractedBy(0, 0),
                "RealRuins.POIRuinsCostCap".Translate() + ": " + poiCapStr);

            RealRuins_ModSettings.poiRuinsCostCap =
                Mathf.Round(Widgets.HorizontalSlider(
                    poiCapRect.RightHalf().ContractedBy(0, 3),
                    RealRuins_ModSettings.poiRuinsCostCap,
                    0.0f,
                    RealRuins_ModSettings.poiRuinsCostCapUnlimited) / 1000.0f) * 1000.0f;

            TooltipHandler.TipRegion(poiCapRect, "RealRuins.POIRuinsCostCapTT".Translate());
            listing.Gap(15);

            Rect spawnBlacklistRect = listing.GetRect(25f);
            Widgets.Label(
                spawnBlacklistRect.LeftHalf().ContractedBy(0, 0),
                "RealRuins.SpawnBlacklist".Translate());
            
            RealRuins_ModSettings.spawnBlacklist = BufferedTextField(spawnBlacklistRect.RightHalf().ContractedBy(0, 3),
                RealRuins_ModSettings.spawnBlacklist, ref spawnBlacklistBuffer, ref spawnBlacklistSynced);
            
            TooltipHandler.TipRegion(spawnBlacklistRect, "RealRuins.SpawnBlacklistTT".Translate());

            listing.Gap(15);

            Rect materialBlacklistRect = listing.GetRect(25f);
            Widgets.Label(
                materialBlacklistRect.LeftHalf().ContractedBy(0, 0),
                "RealRuins.MaterialBlacklist".Translate());
            
            RealRuins_ModSettings.materialBlacklist = BufferedTextField(materialBlacklistRect.RightHalf().ContractedBy(0, 3),
                RealRuins_ModSettings.materialBlacklist, ref materialBlacklistBuffer, ref materialBlacklistSynced);
            
            TooltipHandler.TipRegion(materialBlacklistRect, "RealRuins.MaterialBlacklistTT".Translate());

            listing.Gap(15);

            Rect fallbackMaterialRect = listing.GetRect(25f);
            Widgets.Label(
                fallbackMaterialRect.LeftHalf().ContractedBy(0, 0),
                "RealRuins.FallbackMaterial".Translate());
            
            RealRuins_ModSettings.fallbackMaterial = BufferedTextField(fallbackMaterialRect.RightHalf().ContractedBy(0, 3),
                RealRuins_ModSettings.fallbackMaterial, ref fallbackMaterialBuffer, ref fallbackMaterialSynced);
            
            TooltipHandler.TipRegion(fallbackMaterialRect, "RealRuins.FallbackMaterialTT".Translate());

            listing.End();
        }
    }
}
