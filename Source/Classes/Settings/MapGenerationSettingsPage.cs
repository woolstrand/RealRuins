using System;
using System.Collections.Generic;
using Verse;
using RimWorld;
using UnityEngine;

namespace RealRuins.Settings
{
    public class MapGenerationSettingsPage : SettingsPage
    {
        public override string TabLabel => "RealRuins.SpawnSettings.Caption".Translate();

        public override void Draw(Rect rect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(rect);

            GameFont font = Text.Font;
            Text.Font = GameFont.Medium;
            listing.Label("RealRuins.SpawnSettings.Caption".Translate());
            Text.Font = font;
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

            // Display current values as info labels
            listing.Label(
                "RealRuins_MapOptions_Density".Translate() + ": x" +
                RealRuins_ModSettings.defaultScatterOptions.densityMultiplier.ToString("F"),
                -1,
                "RealRuins_MapOptions_DensityTT".Translate());

            listing.Label(
                label: "RealRuins_MapOptions_Size".Translate() + ": " +
                RealRuins_ModSettings.defaultScatterOptions.minRadius,
                maxHeight: -1,
                tooltip: "RealRuins_MapOptions_SizeTT".Translate());

            listing.Label(
                label: "RealRuins_MapOptions_Size_Max".Translate() + ": " +
                RealRuins_ModSettings.defaultScatterOptions.maxRadius,
                maxHeight: -1,
                tooltip: "RealRuins_MapOptions_SizeTT".Translate());

            listing.Gap(15);

            listing.Label(
                "RealRuins_MapOptions_Deterioration".Translate() + ": " +
                RealRuins_ModSettings.defaultScatterOptions.deteriorationMultiplier.ToString("F"),
                -1,
                "RealRuins_MapOptions_DeteriorationTT".Translate());

            listing.Label(
                "RealRuins_MapOptions_Scavengers".Translate() + ": " +
                RealRuins_ModSettings.defaultScatterOptions.scavengingMultiplier.ToString("F"),
                -1,
                "RealRuins_MapOptions_ScavengersTT".Translate());

            listing.Label(
                "RealRuins_MapOptions_CostLimit".Translate() + ": " + costStr,
                -1,
                "RealRuins_MapOptions_CostLimitTT".Translate());

            listing.Gap(15);

            listing.Label(
                "RealRuins_MapOptions_DisableDecoration".Translate(),
                -1,
                "RealRuins_MapOptions_DisableDecorationTT".Translate());

            listing.Label(
                "RealRuins_MapOptions_DisableTraps".Translate(),
                -1,
                "RealRuins_MapOptions_DisableTrapsTT".Translate());

            listing.Label(
                "RealRuins_MapOptions_DisableHostiles".Translate(),
                -1,
                "RealRuins_MapOptions_DisableHostilesTT".Translate());

            listing.Gap(15);

            listing.CheckboxLabeled(
                "RealRuins_MapOptions_DisableHaulables".Translate(),
                ref RealRuins_ModSettings.defaultScatterOptions.disableSpawnItems,
                "RealRuins_MapOptions_DisableHaulablesTT".Translate());

            listing.CheckboxLabeled(
                "RealRuins_MapOptions_WallsAndDoorsOnly".Translate(),
                ref RealRuins_ModSettings.defaultScatterOptions.wallsDoorsOnly,
                "RealRuins_MapOptions_WallsAndDoorsOnlyTT".Translate());

            listing.CheckboxLabeled(
                "RealRuins_MapOptions_EnableProximity".Translate(),
                ref RealRuins_ModSettings.defaultScatterOptions.enableProximity,
                "RealRuins_MapOptions_EnableProximityTT".Translate());

            listing.CheckboxLabeled(
                "RealRuins_MapOptions_StartWithoutRuins".Translate(),
                ref RealRuins_ModSettings.startWithoutRuins,
                "RealRuins_MapOptions_StartWithoutRuinsTT".Translate());

            listing.CheckboxLabeled(
                "RealRuins.LeaveVanillaRuins".Translate(),
                ref RealRuins_ModSettings.preserveStandardRuins,
                "RealRuins.LeaveVanillaRuinsTT".Translate());

            // Sliders
            listing.Gap(15);

            RealRuins_ModSettings.defaultScatterOptions.densityMultiplier =
                listing.Slider(RealRuins_ModSettings.defaultScatterOptions.densityMultiplier, 0.0f, 20.0f);

            RealRuins_ModSettings.defaultScatterOptions.minRadius =
                (int)listing.Slider(RealRuins_ModSettings.defaultScatterOptions.minRadius, 4.0f, 64.0f);

            RealRuins_ModSettings.defaultScatterOptions.maxRadius =
                (int)listing.Slider(RealRuins_ModSettings.defaultScatterOptions.maxRadius, 4.0f, 64.0f);

            if (RealRuins_ModSettings.defaultScatterOptions.minRadius > RealRuins_ModSettings.defaultScatterOptions.maxRadius)
            {
                RealRuins_ModSettings.defaultScatterOptions.minRadius = RealRuins_ModSettings.defaultScatterOptions.maxRadius;
            }

            listing.Gap(12);

            RealRuins_ModSettings.defaultScatterOptions.deteriorationMultiplier =
                listing.Slider(RealRuins_ModSettings.defaultScatterOptions.deteriorationMultiplier, 0.0f, 1.0f);

            RealRuins_ModSettings.defaultScatterOptions.scavengingMultiplier =
                listing.Slider(RealRuins_ModSettings.defaultScatterOptions.scavengingMultiplier, 0.0f, 5.0f);

            RealRuins_ModSettings.defaultScatterOptions.itemCostLimit =
                (int)listing.Slider(RealRuins_ModSettings.defaultScatterOptions.itemCostLimit, 0.0f, 1000.0f);

            listing.Gap(12);

            RealRuins_ModSettings.defaultScatterOptions.decorationChance =
                listing.Slider(RealRuins_ModSettings.defaultScatterOptions.decorationChance, 0.0f, 0.01f);

            RealRuins_ModSettings.defaultScatterOptions.trapChance =
                listing.Slider(RealRuins_ModSettings.defaultScatterOptions.trapChance, 0.0f, 0.01f);

            RealRuins_ModSettings.defaultScatterOptions.hostileChance =
                listing.Slider(RealRuins_ModSettings.defaultScatterOptions.hostileChance, 0.0f, 1.0f);

            listing.Gap(15);

            listing.CheckboxLabeled(
                "RealRuins.UseForcesGenerationV2".Translate(),
                ref RealRuins_ModSettings.useRuinsForcesGenerationV2,
                "RealRuins.UseForcesGenerationV2TT".Translate());

            listing.End();
        }
    }
}
