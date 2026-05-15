using System;
using System.Collections.Generic;
using Verse;
using RimWorld;
using UnityEngine;

namespace RealRuins.Settings
{
    public class MapGenerationSettingsPage : SettingsPage
    {
        public override string TabLabel => "RealRuins.SpawnSettings.TabCaption".Translate();

        public override void Draw(Rect rect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(rect);

            GameFont font = Text.Font;
            Text.Font = GameFont.Medium;
            listing.Label("RealRuins.SpawnSettings.Caption".Translate());
            Text.Font = font;
            
            float descHeight = Text.CalcHeight("RealRuins.SpawnSettings.Description".Translate(), rect.width);
            Rect descRect = listing.GetRect(descHeight);
            string description = "RealRuins.SpawnSettings.Description".Translate();
            Widgets.Label(descRect, description);
            listing.Gap(5f);
            
            listing.GapLine();

            // Size range info
            int sizeMin = RealRuins_ModSettings.defaultScatterOptions.minRadius;
            int sizeMax = RealRuins_ModSettings.defaultScatterOptions.maxRadius;

            // Cost limit display
            string costStr = "∞";
            if (RealRuins_ModSettings.defaultScatterOptions.itemCostLimit < 1000)
            {
                costStr = RealRuins_ModSettings.defaultScatterOptions.itemCostLimit.ToString();
            }

            // Density
            listing.Label(
                "RealRuins_MapOptions_Density".Translate() + ": x" +
                RealRuins_ModSettings.defaultScatterOptions.densityMultiplier.ToString("F"),
                -1,
                "RealRuins_MapOptions_DensityTT".Translate());

            RealRuins_ModSettings.defaultScatterOptions.densityMultiplier =
                listing.Slider(RealRuins_ModSettings.defaultScatterOptions.densityMultiplier, 0.0f, 20.0f);

            listing.Gap(12);

            // Size min
            listing.Label(
                label: "RealRuins_MapOptions_Size".Translate() + ": " +
                RealRuins_ModSettings.defaultScatterOptions.minRadius,
                maxHeight: -1,
                tooltip: "RealRuins_MapOptions_SizeTT".Translate());

            RealRuins_ModSettings.defaultScatterOptions.minRadius =
                (int)listing.Slider(RealRuins_ModSettings.defaultScatterOptions.minRadius, 4.0f, 64.0f);

            listing.Gap(12);

            // Size max
            listing.Label(
                label: "RealRuins_MapOptions_Size_Max".Translate() + ": " +
                RealRuins_ModSettings.defaultScatterOptions.maxRadius,
                maxHeight: -1,
                tooltip: "RealRuins_MapOptions_SizeTT".Translate());

            RealRuins_ModSettings.defaultScatterOptions.maxRadius =
                (int)listing.Slider(RealRuins_ModSettings.defaultScatterOptions.maxRadius, 4.0f, 64.0f);

            if (RealRuins_ModSettings.defaultScatterOptions.minRadius > RealRuins_ModSettings.defaultScatterOptions.maxRadius)
            {
                RealRuins_ModSettings.defaultScatterOptions.minRadius = RealRuins_ModSettings.defaultScatterOptions.maxRadius;
            }

            listing.Gap(12);

            // Deterioration
            listing.Label(
                "RealRuins_MapOptions_Deterioration".Translate() + ": " +
                RealRuins_ModSettings.defaultScatterOptions.deteriorationMultiplier.ToString("F"),
                -1,
                "RealRuins_MapOptions_DeteriorationTT".Translate());

            RealRuins_ModSettings.defaultScatterOptions.deteriorationMultiplier =
                listing.Slider(RealRuins_ModSettings.defaultScatterOptions.deteriorationMultiplier, 0.0f, 1.0f);

            listing.Gap(12);

            // Scavengers
            listing.Label(
                "RealRuins_MapOptions_Scavengers".Translate() + ": " +
                RealRuins_ModSettings.defaultScatterOptions.scavengingMultiplier.ToString("F"),
                -1,
                "RealRuins_MapOptions_ScavengersTT".Translate());

            RealRuins_ModSettings.defaultScatterOptions.scavengingMultiplier =
                listing.Slider(RealRuins_ModSettings.defaultScatterOptions.scavengingMultiplier, 0.0f, 5.0f);

            listing.Gap(12);

            // Cost limit
            listing.Label(
                "RealRuins_MapOptions_CostLimit".Translate() + ": " + costStr,
                -1,
                "RealRuins_MapOptions_CostLimitTT".Translate());

            RealRuins_ModSettings.defaultScatterOptions.itemCostLimit =
                (int)listing.Slider(RealRuins_ModSettings.defaultScatterOptions.itemCostLimit, 0.0f, 1000.0f);

            listing.Gap(12);

            // Hostiles
            listing.Label(
                "RealRuins_MapOptions_DisableHostiles".Translate(),
                -1,
                "RealRuins_MapOptions_DisableHostilesTT".Translate());

            RealRuins_ModSettings.defaultScatterOptions.hostileChance =
                listing.Slider(RealRuins_ModSettings.defaultScatterOptions.hostileChance, 0.0f, 1.0f);

            listing.Gap(12);

            listing.CheckboxLabeled(
                "RealRuins_MapOptions_DisableHaulables".Translate(),
                ref RealRuins_ModSettings.defaultScatterOptions.disableSpawnItems,
                "RealRuins_MapOptions_DisableHaulablesTT".Translate());

            listing.Gap(12);

            listing.CheckboxLabeled(
                "RealRuins_MapOptions_WallsAndDoorsOnly".Translate(),
                ref RealRuins_ModSettings.defaultScatterOptions.wallsDoorsOnly,
                "RealRuins_MapOptions_WallsAndDoorsOnlyTT".Translate());

            listing.Gap(12);

            listing.CheckboxLabeled(
                "RealRuins_MapOptions_EnableProximity".Translate(),
                ref RealRuins_ModSettings.defaultScatterOptions.enableProximity,
                "RealRuins_MapOptions_EnableProximityTT".Translate());

            listing.Gap(12);

            listing.CheckboxLabeled(
                "RealRuins_MapOptions_StartWithoutRuins".Translate(),
                ref RealRuins_ModSettings.startWithoutRuins,
                "RealRuins_MapOptions_StartWithoutRuinsTT".Translate());

            listing.Gap(12);

            listing.CheckboxLabeled(
                "RealRuins.KeepVanillaRuins".Translate(),
                ref RealRuins_ModSettings.preserveStandardRuins,
                "RealRuins.KeepVanillaRuinsTT".Translate());

             listing.End();
        }
    }
}
