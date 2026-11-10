using System;
using System.Collections.Generic;
using Verse;
using UnityEngine;

namespace RealRuins.Settings
{
    public class PresetsSettingsPage : SettingsPage
    {
        private const float PresetButtonHeight = 32f;
        private const float ButtonColumnFraction = 0.3f;
        private const float ColumnGap = 16f;
        private const float PresetGap = 8f;

        private static readonly Color SelectedColor = new Color(0.55f, 0.85f, 0.55f);

        private static readonly HashSet<string> PlayStyleKeys = SettingsPresets.KeysOf(SettingsPresets.PlayStyles);
        private static readonly HashSet<string> TechLevelKeys = SettingsPresets.KeysOf(SettingsPresets.TechLevels);

        // The Custom preset of a group, rebuilt only when its stored values change
        private class CustomSlot
        {
            public string packed;
            public SettingsPreset preset;
        }
        private static readonly CustomSlot PlayStyleSlot = new CustomSlot();
        private static readonly CustomSlot TechLevelSlot = new CustomSlot();

        public override string TabLabel => "RealRuins.Presets.TabCaption".Translate();

        public PresetsSettingsPage()
        {
            if (Prefs.DevMode) SettingsPresets.Validate();
        }

        protected override void DrawContents(Listing_Standard listing)
        {
            SettingsControls.PageHeader(listing, "RealRuins.Presets.Caption", "RealRuins.Presets.Description");

            Dictionary<string, string> current = SettingsPresets.CurrentValues();

            SettingsPreset playStyle = PresetSection(listing, "RealRuins.Presets.PlayStyle", SettingsPresets.PlayStyles, current);
            SettingsPreset customPlayStyle = Custom(ref RealRuins_ModSettings.customPlayStyle, PlayStyleKeys, current, playStyle == null, PlayStyleSlot);
            foreach (SettingsPreset preset in SettingsPresets.PlayStyles)
            {
                DrawPresetRow(listing, preset, preset == playStyle);
            }
            DrawPresetRow(listing, customPlayStyle, playStyle == null);

            SettingsPreset techLevel = PresetSection(listing, "RealRuins.Presets.TechLevel", SettingsPresets.TechLevels, current);
            SettingsPreset customTechLevel = Custom(ref RealRuins_ModSettings.customTechLevel, TechLevelKeys, current, techLevel == null, TechLevelSlot);
            List<SettingsPreset> techColumns = new List<SettingsPreset>(SettingsPresets.TechLevels) { customTechLevel };
            DrawPresetColumns(listing, techColumns, techLevel ?? customTechLevel);

            SettingsControls.Section(listing, "RealRuins.Presets.SettingsSection");
            DrawSettingsButtons(listing);
        }

        // Section caption and the name of the preset the current settings match. Returns that preset, or null.
        private static SettingsPreset PresetSection(Listing_Standard listing, string captionKey, List<SettingsPreset> presets, Dictionary<string, string> current)
        {
            SettingsPreset matching = SettingsPresets.Matching(presets, current);
            SettingsControls.Section(listing, captionKey);
            SettingsControls.Description(listing, "RealRuins.Presets.Current".Translate() + ": " +
                (matching != null ? matching.Label : (string)"RealRuins.Presets.Custom".Translate()));
            listing.Gap(PresetGap);
            return matching;
        }

        // The Custom preset of a group. No change handlers are needed: the page compares the settings with the presets on every
        // draw, and while they match none, they are remembered (and saved with the mod settings) as the group's custom values.
        // Picking a preset then leaves them alone, so the Custom button brings them back.
        private static SettingsPreset Custom(ref string stored, HashSet<string> keys, Dictionary<string, string> current, bool isCustom, CustomSlot slot)
        {
            if (isCustom)
            {
                string packed = SettingsPresets.Pack(current, keys);
                if (packed != stored) stored = packed;
            }
            if (slot.preset == null || stored != slot.packed)
            {
                slot.packed = stored;
                slot.preset = new SettingsPreset("RealRuins.Presets.Custom", SettingsPresets.Unpack(stored));
            }
            return slot.preset;
        }

        private static bool PresetButton(Rect rect, SettingsPreset preset, bool selected)
        {
            // Custom has nothing to restore until the settings have matched no preset at least once
            bool active = preset.values.Count > 0;
            Color prevColor = GUI.color;
            if (selected) GUI.color = SelectedColor;
            bool clicked = Widgets.ButtonText(rect, preset.Label, active: active) && active;
            GUI.color = prevColor;
            if (clicked) preset.Apply();
            return clicked;
        }

        private static void DescriptionLabel(Rect rect, string text, TextAnchor anchor)
        {
            Color prevColor = GUI.color;
            TextAnchor prevAnchor = Text.Anchor;
            GUI.color = SettingsControls.DescriptionColor;
            Text.Anchor = anchor;
            Widgets.Label(rect, text);
            Text.Anchor = prevAnchor;
            GUI.color = prevColor;
        }

        // Button in the left 30%, description in the right 70%
        private static void DrawPresetRow(Listing_Standard listing, SettingsPreset preset, bool selected)
        {
            float buttonWidth = Mathf.Round(listing.ColumnWidth * ButtonColumnFraction);
            float textWidth = listing.ColumnWidth - buttonWidth - ColumnGap;
            string description = preset.Tooltip;
            float height = Mathf.Max(PresetButtonHeight, Text.CalcHeight(description, textWidth));
            Rect row = listing.GetRect(height);

            PresetButton(new Rect(row.x, row.y, buttonWidth, PresetButtonHeight), preset, selected);
            DescriptionLabel(new Rect(row.x + buttonWidth + ColumnGap, row.y, textWidth, height), description,
                height > PresetButtonHeight ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft);
            listing.Gap(PresetGap);
        }

        // One column per preset: buttons on the top row, descriptions below them
        private static void DrawPresetColumns(Listing_Standard listing, List<SettingsPreset> presets, SettingsPreset selected)
        {
            float width = (listing.ColumnWidth - SettingsControls.ButtonGap * (presets.Count - 1)) / presets.Count;
            float textHeight = 0f;
            foreach (SettingsPreset preset in presets)
            {
                textHeight = Mathf.Max(textHeight, Text.CalcHeight(preset.Tooltip, width));
            }

            Rect row = listing.GetRect(PresetButtonHeight + 6f + textHeight);
            for (int i = 0; i < presets.Count; i++)
            {
                float x = Mathf.Round(row.x + i * (width + SettingsControls.ButtonGap));
                PresetButton(new Rect(x, row.y, Mathf.Round(width), PresetButtonHeight), presets[i], presets[i] == selected);
                DescriptionLabel(new Rect(x, row.y + PresetButtonHeight + 6f, Mathf.Round(width), textHeight), presets[i].Tooltip, TextAnchor.UpperLeft);
            }
            listing.Gap(PresetGap);
        }

        private static void DrawSettingsButtons(Listing_Standard listing)
        {
            int clicked = SettingsControls.ButtonRow(listing,
                new string[] {
                    "RealRuins.Settings.ExportSettings".Translate(),
                    "RealRuins.Settings.ImportSettings".Translate(),
                    "RealRuins.DebugSettings.ResetSettings".Translate()
                },
                new Color?[] { null, null, SettingsControls.DangerColor },
                new string[] { null, null, "RealRuins.Presets.ResetAllTT".Translate() });

            switch (clicked)
            {
                case 0:
                    Find.WindowStack.Add(new Dialog_ImportExportSettings(true));
                    break;
                case 1:
                    Find.WindowStack.Add(new Dialog_ImportExportSettings(false));
                    break;
                case 2:
                    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                        "RealRuins.DebugSettings.ResetSettingsConfirm".Translate(),
                        () => RealRuins_ModSettings.Reset(),
                        true,
                        null));
                    break;
            }
        }
    }
}
