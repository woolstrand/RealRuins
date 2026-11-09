using System;
using System.Collections.Generic;
using Verse;
using UnityEngine;

namespace RealRuins.Settings
{
    public class PresetsSettingsPage : SettingsPage
    {
        private const float PresetButtonHeight = 32f;
        private const float PresetButtonGap = 6f;

        private static readonly Color SelectedColor = new Color(0.55f, 0.85f, 0.55f);

        public override string TabLabel => "RealRuins.Presets.TabCaption".Translate();

        public PresetsSettingsPage()
        {
            if (Prefs.DevMode) SettingsPresets.Validate();
        }

        protected override void DrawContents(Listing_Standard listing)
        {
            SettingsControls.PageHeader(listing, "RealRuins.Presets.Caption", "RealRuins.Presets.Description");

            Dictionary<string, string> current = SettingsPresets.CurrentValues();

            DrawPresetRow(listing, "RealRuins.Presets.PlayStyle", SettingsPresets.PlayStyles, current);
            DrawPresetRow(listing, "RealRuins.Presets.TechLevel", SettingsPresets.TechLevels, current);

            SettingsControls.Section(listing, "RealRuins.Presets.SettingsSection");
            DrawSettingsButtons(listing);
        }

        private static void DrawPresetRow(Listing_Standard listing, string captionKey, List<SettingsPreset> presets, Dictionary<string, string> current)
        {
            SettingsPreset matching = SettingsPresets.Matching(presets, current);

            SettingsControls.Section(listing, captionKey);
            listing.Label("RealRuins.Presets.Current".Translate() + ": " +
                (matching != null ? matching.Label : (string)"RealRuins.Presets.Custom".Translate()));

            Rect row = listing.GetRect(PresetButtonHeight);
            // at least four slots, so a row with one preset doesn't get a button across the whole page
            int slots = Math.Max(4, presets.Count);
            float width = (row.width - PresetButtonGap * (slots - 1)) / slots;
            for (int i = 0; i < presets.Count; i++)
            {
                SettingsPreset preset = presets[i];
                Rect buttonRect = new Rect(row.x + i * (width + PresetButtonGap), row.y, width, row.height);
                Color prevColor = GUI.color;
                if (preset == matching) GUI.color = SelectedColor;
                if (Widgets.ButtonText(buttonRect, preset.Label))
                {
                    preset.Apply();
                }
                GUI.color = prevColor;
                TooltipHandler.TipRegion(buttonRect, preset.Tooltip);
            }
        }

        private static void DrawSettingsButtons(Listing_Standard listing)
        {
            Rect row = listing.GetRect(30f);
            float width = (row.width - 20f) / 3f;
            Rect exportRect = new Rect(row.x, row.y, width, row.height);
            Rect importRect = new Rect(row.x + width + 10f, row.y, width, row.height);
            Rect resetRect = new Rect(row.x + (width + 10f) * 2f, row.y, width, row.height);

            if (Widgets.ButtonText(exportRect, "RealRuins.Settings.ExportSettings".Translate()))
            {
                Find.WindowStack.Add(new Dialog_ImportExportSettings(true));
            }

            if (Widgets.ButtonText(importRect, "RealRuins.Settings.ImportSettings".Translate()))
            {
                Find.WindowStack.Add(new Dialog_ImportExportSettings(false));
            }

            Color prevColor = GUI.color;
            GUI.color = new Color(1f, 0.3f, 0.3f);
            if (Widgets.ButtonText(resetRect, "RealRuins.DebugSettings.ResetSettings".Translate()))
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "RealRuins.DebugSettings.ResetSettingsConfirm".Translate(),
                    () => RealRuins_ModSettings.Reset(),
                    true,
                    null));
            }
            GUI.color = prevColor;
            TooltipHandler.TipRegion(resetRect, "RealRuins.Presets.ResetAllTT".Translate());
        }
    }
}
