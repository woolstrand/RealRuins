using System;
using System.Collections.Generic;
using Verse;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;

namespace RealRuins.Settings
{
    /// <summary>
    /// One draw method per setting. A setting shown on two tabs calls the same method on both, so label, tooltip, range
    /// and greying can't drift apart.
    /// </summary>
    public static class SettingsControls
    {
        // One grid for every page: rows of the same height and gap, a control column of the same width at the right edge
        public const float RowHeight = 28f;
        public const float RowGap = 4f;
        public const float SectionGap = 18f;
        public const float ButtonHeight = 30f;
        public const float ButtonGap = 10f;
        private const float ControlFraction = 0.42f;
        private const float ControlGap = 12f;
        private const float CheckboxSize = 24f;

        public static readonly Color DescriptionColor = new Color(0.75f, 0.75f, 0.75f);
        public static readonly Color DangerColor = new Color(1f, 0.3f, 0.3f);
        public static readonly Color ActionColor = new Color(0.3f, 1f, 0.3f);

        private static readonly string[] CaravanReformOptions = {
            "RealRuins.Reform.Automatic",
            "RealRuins.Reform.Instant",
            "RealRuins.Reform.Manual"
        };

        // TechLevel is a fixed game enum, mods can't add levels. Undefined and Animal are not eras, and Archotech as a cap limits nothing.
        private static readonly TechLevel[] CapLevels = {
            TechLevel.Neolithic,
            TechLevel.Medieval,
            TechLevel.Industrial,
            TechLevel.Spacer,
            TechLevel.Ultra
        };

        // Text buffers outlive settings import and reset, so each remembers the setting value it was last synced with.
        // They are static: only one tab is drawn at a time, so a mirrored text field can share its buffer.
        private static string spawnBlacklistBuffer, spawnBlacklistSynced;
        private static string materialBlacklistBuffer, materialBlacklistSynced;
        private static string fallbackMaterialBuffer, fallbackMaterialSynced;
        private static string downloadLimitBuffer, transferLimitBuffer;
        private static int downloadLimitSynced, transferLimitSynced;

        #region Layout

        private static bool enabled = true;

        /// <summary>Greys out everything drawn inside and drops the changes. Nests: inner scopes can only disable more.</summary>
        public struct EnabledScope : IDisposable
        {
            private readonly bool prevEnabled;
            private readonly Color prevColor;

            public EnabledScope(bool condition)
            {
                prevEnabled = enabled;
                prevColor = GUI.color;
                enabled = enabled && condition;
                if (!enabled) GUI.color = Color.gray;
            }

            public void Dispose()
            {
                enabled = prevEnabled;
                GUI.color = prevColor;
            }
        }

        public static EnabledScope EnabledIf(bool condition) => new EnabledScope(condition);

        private const float IndentWidth = 24f;
        private static float indent = 0f;

        /// <summary>Shifts the rows drawn inside to the right, to show they depend on the row above.</summary>
        public struct IndentScope : IDisposable
        {
            public IndentScope(bool _) { indent += IndentWidth; }
            public void Dispose() { indent -= IndentWidth; }
        }

        public static IndentScope Indented() => new IndentScope(true);

        private static float ControlWidth(Listing_Standard listing) => Mathf.Round(listing.ColumnWidth * ControlFraction);

        // Listing_Standard.Indent changed between game versions, so rows are indented here.
        // Only the label is indented, the control column stays put: controls line up whatever the nesting.
        private static void SplitRow(Listing_Standard listing, string label, float controlWidth, out Rect labelRect, out Rect controlRect)
        {
            float labelWidth = listing.ColumnWidth - controlWidth - ControlGap - indent;
            float height = Mathf.Max(RowHeight, Text.CalcHeight(label, labelWidth));
            Rect row = listing.GetRect(height);
            controlRect = new Rect(row.xMax - controlWidth, row.y, controlWidth, height);
            labelRect = new Rect(row.x + indent, row.y, labelWidth, height);
            listing.Gap(RowGap);
        }

        private static void RowLabel(Rect rect, string label)
        {
            TextAnchor anchor = Text.Anchor;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(rect, label);
            Text.Anchor = anchor;
        }

        // Highlight and tooltip over the whole row, label and control
        private static void RowTooltip(Rect labelRect, Rect controlRect, string tooltip)
        {
            if (tooltip.NullOrEmpty()) return;
            Rect row = new Rect(labelRect.x, labelRect.y, controlRect.xMax - labelRect.x, labelRect.height);
            if (Mouse.IsOver(row)) Widgets.DrawHighlight(row);
            TooltipHandler.TipRegion(row, tooltip);
        }

        // A control of the given height, vertically centered in the control column
        private static Rect ControlBox(Rect controlRect, float height)
        {
            return new Rect(controlRect.x, controlRect.y + Mathf.Round((controlRect.height - height) / 2f), controlRect.width, height);
        }

        private static void Caption(Listing_Standard listing, string caption)
        {
            GameFont font = Text.Font;
            Text.Font = GameFont.Medium;
            Widgets.Label(listing.GetRect(Text.CalcHeight(caption, listing.ColumnWidth)), caption);
            Text.Font = font;
        }

        public static void PageHeader(Listing_Standard listing, string captionKey, string descriptionKey)
        {
            Caption(listing, captionKey.Translate());
            listing.Gap(2f);
            Description(listing, descriptionKey.Translate());
            listing.GapLine(16f);
        }

        public static void Section(Listing_Standard listing, string captionKey)
        {
            listing.Gap(SectionGap);
            Caption(listing, captionKey.Translate());
            listing.Gap(4f);
        }

        /// <summary>Grey wrapped text across the whole width.</summary>
        public static void Description(Listing_Standard listing, string text)
        {
            Rect rect = listing.GetRect(Text.CalcHeight(text, listing.ColumnWidth));
            Color prevColor = GUI.color;
            GUI.color = prevColor * DescriptionColor;
            Widgets.Label(rect, text);
            GUI.color = prevColor;
        }

        /// <summary>A label on the left and a read-only value in the control column.</summary>
        public static void ValueRow(Listing_Standard listing, string label, string value, string tooltip = null)
        {
            SplitRow(listing, label, ControlWidth(listing), out Rect labelRect, out Rect controlRect);
            RowTooltip(labelRect, controlRect, tooltip);
            RowLabel(labelRect, label);
            RowLabel(controlRect, value);
        }

        /// <summary>A label-only row, e.g. the caption of a group of indented rows.</summary>
        public static void LabelRow(Listing_Standard listing, string label)
        {
            float width = listing.ColumnWidth - indent;
            Rect row = listing.GetRect(Mathf.Max(RowHeight, Text.CalcHeight(label, width)));
            row.xMin += indent;
            RowLabel(row, label);
            listing.Gap(RowGap);
        }

        /// <summary>Equal buttons across the whole width. Returns the index of the clicked one, or -1.</summary>
        public static int ButtonRow(Listing_Standard listing, string[] labels, Color?[] colors = null, string[] tooltips = null)
        {
            Rect row = listing.GetRect(ButtonHeight);
            float width = (row.width - ButtonGap * (labels.Length - 1)) / labels.Length;
            int clicked = -1;
            for (int i = 0; i < labels.Length; i++)
            {
                Rect rect = new Rect(row.x + i * (width + ButtonGap), row.y, width, row.height).Rounded();
                Color prevColor = GUI.color;
                if (colors != null && colors[i].HasValue) GUI.color = colors[i].Value;
                if (Widgets.ButtonText(rect, labels[i], active: enabled) && enabled) clicked = i;
                GUI.color = prevColor;
                if (tooltips != null && !tooltips[i].NullOrEmpty()) TooltipHandler.TipRegion(rect, tooltips[i]);
            }
            listing.Gap(RowGap);
            return clicked;
        }

        // Drawn by hand instead of Listing_Standard.CheckboxLabeled, which can't be indented or disabled on every game version
        public static void Checkbox(Listing_Standard listing, string labelKey, ref bool value, string tooltip = null)
        {
            string label = labelKey.Translate();
            SplitRow(listing, label, CheckboxSize, out Rect labelRect, out Rect controlRect);
            RowTooltip(labelRect, controlRect, tooltip);
            RowLabel(labelRect, label);
            bool shown = value;
            Rect box = ControlBox(controlRect, CheckboxSize);
            Widgets.Checkbox(box.x, box.y, ref shown, CheckboxSize);
            if (enabled) value = shown;
        }

        public static void Checkbox(Listing_Standard listing, string labelKey, ref bool value, bool withTooltip)
        {
            Checkbox(listing, labelKey, ref value, withTooltip ? (string)(labelKey + "TT").Translate() : null);
        }

        // Label on the left, slider in the control column. Returns the new value, or the old one while disabled.
        public static float SliderRow(Listing_Standard listing, string label, float value, float min, float max, string tooltip)
        {
            SplitRow(listing, label, ControlWidth(listing), out Rect labelRect, out Rect controlRect);
            RowTooltip(labelRect, controlRect, tooltip);
            RowLabel(labelRect, label);
            float result = Widgets.HorizontalSlider(ControlBox(controlRect, 20f), value, min, max);
            return enabled ? result : value;
        }

        public static void DropdownRow(Listing_Standard listing, string label, string current, string tooltip, Func<List<FloatMenuOption>> options)
        {
            SplitRow(listing, label, ControlWidth(listing), out Rect labelRect, out Rect controlRect);
            RowTooltip(labelRect, controlRect, tooltip);
            RowLabel(labelRect, label);
            if (Widgets.ButtonText(ControlBox(controlRect, 24f), current, active: enabled) && enabled)
            {
                Find.WindowStack.Add(new FloatMenu(options()));
            }
        }

        private static string TextFieldRow(Listing_Standard listing, string labelKey, string value, ref string buffer, ref string synced)
        {
            string label = labelKey.Translate();
            SplitRow(listing, label, ControlWidth(listing), out Rect labelRect, out Rect controlRect);
            RowTooltip(labelRect, controlRect, (labelKey + "TT").Translate());
            RowLabel(labelRect, label);
            if (buffer == null || value != synced)
            {
                //first draw, or the setting was changed elsewhere (import, reset, preset): show it instead of writing the old text back
                buffer = value ?? "";
            }
            buffer = Widgets.TextField(ControlBox(controlRect, 24f), buffer);
            synced = buffer;
            return buffer;
        }

        // synced is the value the buffer was last filled from, see TextFieldRow
        private static void NumberFieldRow(Listing_Standard listing, string labelKey, ref int value, ref string buffer, ref int synced)
        {
            if (buffer == null || value != synced)
            {
                buffer = value.ToString();
            }
            string label = labelKey.Translate();
            SplitRow(listing, label, ControlWidth(listing), out Rect labelRect, out Rect controlRect);
            RowLabel(labelRect, label);
            Widgets.TextFieldNumeric(ControlBox(controlRect, 24f), ref value, ref buffer);
            synced = value;
        }

        private static string WealthText(float cap) => cap < RuinsWealthLimits.NoLimit ? cap.ToString("#,0") : (string)"RealRuins.Unlimited".Translate();

        #endregion

        #region Wealth limits

        public static void RegularWealthLimit(Listing_Standard listing)
        {
            RealRuins_ModSettings.ruinsWealthPerMap = Mathf.Round(SliderRow(listing,
                "RealRuins_MapOptions_WealthPerMap".Translate() + ": " + WealthText(RuinsWealthLimits.SettingCap(RuinsKind.Regular)),
                RealRuins_ModSettings.ruinsWealthPerMap, 1000.0f, RealRuins_ModSettings.ruinsWealthPerMapUnlimited,
                "RealRuins_MapOptions_WealthPerMapTT".Translate()) / 1000.0f) * 1000.0f;
        }

        public static void CaravanWealthLimit(Listing_Standard listing)
        {
            RealRuins_ModSettings.caravanRuinsCostCap = Mathf.Round(SliderRow(listing,
                "RealRuins.CaravanRuinsCostCap".Translate() + ": " + WealthText(RuinsWealthLimits.SettingCap(RuinsKind.Caravan)),
                Mathf.Max(RealRuins_ModSettings.caravanRuinsCostCapMin, RealRuins_ModSettings.caravanRuinsCostCap),
                RealRuins_ModSettings.caravanRuinsCostCapMin, RealRuins_ModSettings.caravanRuinsCostCapUnlimited,
                "RealRuins.CaravanRuinsCostCapTT".Translate()) / 1000.0f) * 1000.0f;
        }

        public static void PristineWealthLimit(Listing_Standard listing)
        {
            //log scale; the log/exp round trip isn't exact: write the value back only when the slider was moved
            float capLog = (float)Math.Log(RealRuins_ModSettings.ruinsCostCap);
            float newCapLog = SliderRow(listing,
                "RealRuins.AbsoluteWealthCap".Translate() + ": " + WealthText(RuinsWealthLimits.SettingCap(RuinsKind.AbandonedBase)),
                capLog, 6.908f, (float)Math.Log(1.0e9),
                "RealRuins.AbsoluteWealthCapTT".Translate());
            if (newCapLog != capLog)
            {
                RealRuins_ModSettings.ruinsCostCap = (float)Math.Exp(newCapLog);
            }
        }

        public static void LocationsWealthLimit(Listing_Standard listing)
        {
            RealRuins_ModSettings.poiRuinsCostCap = Mathf.Round(SliderRow(listing,
                "RealRuins.POIRuinsCostCap".Translate() + ": " + WealthText(RuinsWealthLimits.SettingCap(RuinsKind.AbandonedPOI)),
                RealRuins_ModSettings.poiRuinsCostCap, 0.0f, RealRuins_ModSettings.poiRuinsCostCapUnlimited,
                "RealRuins.POIRuinsCostCapTT".Translate()) / 1000.0f) * 1000.0f;
        }

        public static void ItemCostLimit(Listing_Standard listing)
        {
            ScatterOptions so = RealRuins_ModSettings.defaultScatterOptions;
            string costStr = so.itemCostLimit < 1000 ? so.itemCostLimit.ToString() : (string)"RealRuins.Unlimited".Translate();
            so.itemCostLimit = (int)SliderRow(listing,
                "RealRuins_MapOptions_CostLimit".Translate() + ": " + costStr,
                so.itemCostLimit, 0.0f, 1000.0f,
                "RealRuins_MapOptions_CostLimitTT".Translate());
        }

        public static void ScaleWithColonyWealth(Listing_Standard listing)
        {
            Checkbox(listing, "RealRuins.ScaleWithColonyWealth", ref RealRuins_ModSettings.ruinsScaleWithColonyWealth, true);
            using (EnabledIf(RealRuins_ModSettings.ruinsScaleWithColonyWealth))
            using (Indented())
            {
                RealRuins_ModSettings.ruinsReferenceColonyWealth = Mathf.Round(SliderRow(listing,
                    "RealRuins.ReferenceColonyWealth".Translate() + ": " + RealRuins_ModSettings.ruinsReferenceColonyWealth.ToString("#,0"),
                    RealRuins_ModSettings.ruinsReferenceColonyWealth, 25000.0f, 500000.0f,
                    "RealRuins.ReferenceColonyWealthTT".Translate()) / 5000.0f) * 5000.0f;
            }
        }

        // Only matters while the per-map limit is on
        public static void RegularLimitCounting(Listing_Standard listing)
        {
            LabelRow(listing, "RealRuins.Settings.RegularLimitCounting".Translate());
            using (EnabledIf(RuinsWealthLimits.SettingCap(RuinsKind.Regular) < RuinsWealthLimits.NoLimit))
            using (Indented())
            {
                Checkbox(listing, "RealRuins_MapOptions_WealthScaleWithProximity", ref RealRuins_ModSettings.ruinsWealthScaleWithProximity, true);
                Checkbox(listing, "RealRuins_MapOptions_WealthIgnoreStructure", ref RealRuins_ModSettings.ruinsWealthIgnoreStructure, true);
                Checkbox(listing, "RealRuins_MapOptions_WealthCountDamage", ref RealRuins_ModSettings.ruinsWealthCountDamage, true);
            }
        }

        #endregion

        #region Threats

        public static void ThreatScale(Listing_Standard listing)
        {
            RealRuins_ModSettings.forceMultiplier = SliderRow(listing,
                "RealRuins.ForceMultiplier".Translate() + ": x" + RealRuins_ModSettings.forceMultiplier.ToString("F"),
                RealRuins_ModSettings.forceMultiplier, 0.0f, 2.0f,
                "RealRuins.ForceMultiplierTT".Translate());
        }

        public static void InhabitantsChance(Listing_Standard listing)
        {
            ScatterOptions so = RealRuins_ModSettings.defaultScatterOptions;
            so.hostileChance = SliderRow(listing,
                "RealRuins_MapOptions_DisableHostiles".Translate() + ": " + so.hostileChance.ToStringPercent(),
                so.hostileChance, 0.0f, 1.0f,
                "RealRuins_MapOptions_DisableHostilesTT".Translate());
        }

        public static void DisableFriendlyRaids(Listing_Standard listing)
        {
            Checkbox(listing, "RealRuins.DisableFriendlyRaids", ref RealRuins_ModSettings.disableFriendlyRaids, true);
        }

        public static void SmallRaidsKeepComing(Listing_Standard listing)
        {
            Checkbox(listing, "RealRuins.UseForcesGenerationV2", ref RealRuins_ModSettings.useRuinsForcesGenerationV2, true);
        }

        public static void NoRuinsOnStartingMap(Listing_Standard listing)
        {
            Checkbox(listing, "RealRuins_MapOptions_StartWithoutRuins", ref RealRuins_ModSettings.startWithoutRuins, true);
        }

        #endregion

        #region Content

        public static void NoHaulables(Listing_Standard listing)
        {
            Checkbox(listing, "RealRuins_MapOptions_DisableHaulables", ref RealRuins_ModSettings.defaultScatterOptions.disableSpawnItems, true);
        }

        public static void WallsAndDoorsOnly(Listing_Standard listing)
        {
            Checkbox(listing, "RealRuins_MapOptions_WallsAndDoorsOnly", ref RealRuins_ModSettings.defaultScatterOptions.wallsDoorsOnly, true);
        }

        public static void SpawnRoofs(Listing_Standard listing)
        {
            Checkbox(listing, "RealRuins.SpawnRoofs", ref RealRuins_ModSettings.spawnRoofs, true);
        }

        public static void TechLevelCap(Listing_Standard listing)
        {
            Checkbox(listing, "RealRuins.TechLimit.Enabled", ref RealRuins_ModSettings.techLimitEnabled, true);
            using (EnabledIf(RealRuins_ModSettings.techLimitEnabled))
            using (Indented())
            {
                DropdownRow(listing, "RealRuins.TechLimit.MaxLevel".Translate(), TechLevelLabel(RealRuins_ModSettings.techLimitMaxLevel),
                    "RealRuins.TechLimit.MaxLevelTT".Translate(), () => {
                        List<FloatMenuOption> options = new List<FloatMenuOption>();
                        foreach (TechLevel level in CapLevels)
                        {
                            TechLevel value = level;
                            options.Add(new FloatMenuOption(TechLevelLabel(level), () => RealRuins_ModSettings.techLimitMaxLevel = value));
                        }
                        return options;
                    });
                DropdownRow(listing, "RealRuins.TechLimit.Strategy".Translate(), TechStrategyLabel(RealRuins_ModSettings.techLimitStrict),
                    "RealRuins.TechLimit.StrategyTT".Translate(), () => new List<FloatMenuOption> {
                        new FloatMenuOption(TechStrategyLabel(true), () => RealRuins_ModSettings.techLimitStrict = true),
                        new FloatMenuOption(TechStrategyLabel(false), () => RealRuins_ModSettings.techLimitStrict = false),
                    });
            }
        }

        // Belongs to the tech level cap, not to the section it sits in: greyed while the cap is off, and says so
        private static void TechCapAppliesHere(Listing_Standard listing, ref bool value, string scopeTooltipKey)
        {
            using (EnabledIf(RealRuins_ModSettings.techLimitEnabled))
            using (Indented())
            {
                Checkbox(listing, "RealRuins.TechLimit.AppliesHere", ref value,
                    "RealRuins.TechLimit.AppliesHereTT".Translate() + "\n\n" + scopeTooltipKey.Translate());
            }
        }

        public static void TechCapAppliesToRegular(Listing_Standard listing) => TechCapAppliesHere(listing, ref RealRuins_ModSettings.techLimitRuins, "RealRuins.TechLimit.RuinsTT");
        public static void TechCapAppliesToEvents(Listing_Standard listing) => TechCapAppliesHere(listing, ref RealRuins_ModSettings.techLimitEvents, "RealRuins.TechLimit.EventsTT");
        public static void TechCapAppliesToLocations(Listing_Standard listing) => TechCapAppliesHere(listing, ref RealRuins_ModSettings.techLimitPOIs, "RealRuins.TechLimit.POIsTT");

        public static string TechLevelLabel(TechLevel level) => level.ToStringHuman().CapitalizeFirst();

        private static string TechStrategyLabel(bool strict) => (strict ? "RealRuins.TechLimit.Strict" : "RealRuins.TechLimit.Limited").Translate();

        public static void Blacklists(Listing_Standard listing)
        {
            RealRuins_ModSettings.spawnBlacklist = TextFieldRow(listing, "RealRuins.SpawnBlacklist",
                RealRuins_ModSettings.spawnBlacklist, ref spawnBlacklistBuffer, ref spawnBlacklistSynced);
            RealRuins_ModSettings.materialBlacklist = TextFieldRow(listing, "RealRuins.MaterialBlacklist",
                RealRuins_ModSettings.materialBlacklist, ref materialBlacklistBuffer, ref materialBlacklistSynced);
            RealRuins_ModSettings.fallbackMaterial = TextFieldRow(listing, "RealRuins.FallbackMaterial",
                RealRuins_ModSettings.fallbackMaterial, ref fallbackMaterialBuffer, ref fallbackMaterialSynced);
        }

        #endregion

        #region Regular ruins

        public static void Density(Listing_Standard listing)
        {
            ScatterOptions so = RealRuins_ModSettings.defaultScatterOptions;
            so.densityMultiplier = SliderRow(listing,
                "RealRuins_MapOptions_Density".Translate() + ": x" + so.densityMultiplier.ToString("F"),
                so.densityMultiplier, 0.0f, 20.0f,
                "RealRuins_MapOptions_DensityTT".Translate());
        }

        public static void FragmentSize(Listing_Standard listing)
        {
            ScatterOptions so = RealRuins_ModSettings.defaultScatterOptions;
            so.minRadius = (int)SliderRow(listing,
                "RealRuins_MapOptions_Size".Translate() + ": " + so.minRadius,
                so.minRadius, 4.0f, 64.0f,
                "RealRuins_MapOptions_SizeTT".Translate());
            so.maxRadius = (int)SliderRow(listing,
                "RealRuins_MapOptions_Size_Max".Translate() + ": " + so.maxRadius,
                so.maxRadius, 4.0f, 64.0f,
                "RealRuins_MapOptions_SizeTT".Translate());
            if (so.minRadius > so.maxRadius)
            {
                so.minRadius = so.maxRadius;
            }
        }

        public static void SettlementProximity(Listing_Standard listing)
        {
            Checkbox(listing, "RealRuins_MapOptions_EnableProximity", ref RealRuins_ModSettings.defaultScatterOptions.enableProximity, true);
        }

        public static void Deterioration(Listing_Standard listing)
        {
            ScatterOptions so = RealRuins_ModSettings.defaultScatterOptions;
            so.deteriorationMultiplier = SliderRow(listing,
                "RealRuins_MapOptions_Deterioration".Translate() + ": " + so.deteriorationMultiplier.ToString("F"),
                so.deteriorationMultiplier, 0.0f, 1.0f,
                "RealRuins_MapOptions_DeteriorationTT".Translate());
        }

        public static void Scavengers(Listing_Standard listing)
        {
            ScatterOptions so = RealRuins_ModSettings.defaultScatterOptions;
            so.scavengingMultiplier = SliderRow(listing,
                "RealRuins_MapOptions_Scavengers".Translate() + ": " + so.scavengingMultiplier.ToString("F"),
                so.scavengingMultiplier, 0.0f, 5.0f,
                "RealRuins_MapOptions_ScavengersTT".Translate());
        }

        public static void KeepVanillaRuins(Listing_Standard listing)
        {
            Checkbox(listing, "RealRuins.KeepVanillaRuins", ref RealRuins_ModSettings.preserveStandardRuins, true);
        }

        #endregion

        #region Planetary locations

        public static void PlanetaryLimits(Listing_Standard listing)
        {
            NumberFieldRow(listing, "RealRuins.PlanetarySettings.DownloadLimit",
                ref RealRuins_ModSettings.planetaryRuinsOptions.downloadLimit, ref downloadLimitBuffer, ref downloadLimitSynced);
            NumberFieldRow(listing, "RealRuins.PlanetarySettings.TransferLimit",
                ref RealRuins_ModSettings.planetaryRuinsOptions.transferLimit, ref transferLimitBuffer, ref transferLimitSynced);
        }

        public static void DiscardPlainRuins(Listing_Standard listing)
        {
            Checkbox(listing, "RealRuins.PlanetarySettings.ExcludePlain", ref RealRuins_ModSettings.planetaryRuinsOptions.excludePlainRuins);
        }

        public static void NoFriendlyLocations(Listing_Standard listing)
        {
            Checkbox(listing, "RealRuins.PlanetarySettings.DisableFriendly", ref RealRuins_ModSettings.planetaryRuinsOptions.disableSpawnFriendlyLocations, true);
        }

        public static void AbandonedLocations(Listing_Standard listing)
        {
            PlanetaryRuinsOptions po = RealRuins_ModSettings.planetaryRuinsOptions;
            po.abandonedLocations = SliderRow(listing,
                "RealRuins.PlanetarySettings.AbandonedPercentage".Translate() + ": " + (int)po.abandonedLocations + "%",
                po.abandonedLocations, 0.0f, 100.0f, null);
        }

        public static void OfferPlanetaryAtStart(Listing_Standard listing)
        {
            Checkbox(listing, "RealRuins.PlanetarySettings.Enable", ref RealRuins_ModSettings.planetaryRuinsOptions.allowOnStart);
        }

        public static void PlanetaryButtons(Listing_Standard listing)
        {
            int clicked = ButtonRow(listing,
                new string[] { "RealRuins.PlanetarySettings.MapsModuleButton".Translate(), "RealRuins.PlanetarySettings.RemoveAll".Translate() },
                new Color?[] { ActionColor, DangerColor });
            if (clicked == 0)
            {
                Find.WindowStack.Add(new Page_PlanetaryRuinsLoader());
            }
            else if (clicked == 1)
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "RealRuins.PlanetarySettings.RemoveAllConfirm".Translate(),
                    RemoveAllLocations,
                    true,
                    null));
            }
        }

        private static void RemoveAllLocations()
        {
            List<WorldObject> objectsToRemove = new List<WorldObject>();
            foreach (var obj in Find.WorldObjects.AllWorldObjects)
            {
                if (obj is RealRuinsPOIWorldObject)
                {
                    objectsToRemove.Add(obj);
                }
            }

            foreach (var obj in objectsToRemove)
            {
                Find.WorldObjects.Remove(obj);
            }
        }

        #endregion

        #region Events

        public static void PristineRuinsQuest(Listing_Standard listing)
        {
            Checkbox(listing, "RealRuins.EnableAbandonedRuinsFoundEvent", ref RealRuins_ModSettings.enableAbandonedRuinsFoundEvent, true);
        }

        public static void CaravanRuinsEvent(Listing_Standard listing)
        {
            Checkbox(listing, "RealRuins.EnableCaravanFoundRuinsEvent", ref RealRuins_ModSettings.enableCaravanFoundRuinsEvent, true);
        }

        public static void CaravanReform(Listing_Standard listing)
        {
            DropdownRow(listing, "RealRuins.CaravanReformType".Translate(),
                CaravanReformOptions[(int)CaravanReformPolicy.Mode].Translate(),
                "RealRuins.CaravanReformTooltip".Translate(), () => {
                    List<FloatMenuOption> options = new List<FloatMenuOption>();
                    for (int i = 0; i < CaravanReformOptions.Length; i++)
                    {
                        int value = i;
                        options.Add(new FloatMenuOption(CaravanReformOptions[i].Translate(), () => RealRuins_ModSettings.caravanReformType = value));
                    }
                    return options;
                });
        }

        #endregion
    }
}
