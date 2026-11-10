using System;
using Verse;
using RimWorld;
using UnityEngine;
using RealRuins;

namespace RealRuins.Settings
{
    public class DebugSettingsPage : SettingsPage
    {
        public override string TabLabel => "RealRuins.DebugSettings.Caption".Translate();

        protected override void DrawContents(Listing_Standard listing)
        {
            SettingsControls.PageHeader(listing, "RealRuins.DebugSettings.Caption", "RealRuins.DebugSettings.Description");

            SettingsControls.DropdownRow(listing, "RealRuins.DebugSettings.LogLevel".Translate(), GetLogLevelLabel(), null, () =>
                new System.Collections.Generic.List<FloatMenuOption> {
                    new FloatMenuOption("RealRuins.DebugSettings.LogLevelAll".Translate(), () => RealRuins_ModSettings.logLevel = 0),
                    new FloatMenuOption("RealRuins.DebugSettings.LogLevelWarnings".Translate(), () => RealRuins_ModSettings.logLevel = 1),
                    new FloatMenuOption("RealRuins.DebugSettings.LogLevelErrors".Translate(), () => RealRuins_ModSettings.logLevel = 2),
                });

            SettingsControls.Checkbox(listing, "RealRuins.DebugSettings.KeepSnapshotsAfterUpload",
                ref RealRuins_ModSettings.debugKeepSnapshotsAfterUpload,
                "RealRuins.DebugSettings.KeepSnapshotsAfterUploadTooltip".Translate());
            SettingsControls.Checkbox(listing, "RealRuins.DebugSettings.LogRuinsWealth",
                ref RealRuins_ModSettings.debugLogRuinsWealth,
                "RealRuins.DebugSettings.LogRuinsWealthTooltip".Translate());
            SettingsControls.Checkbox(listing, "RealRuins.DebugSettings.UseStagingEnvironment",
                ref RealRuins_ModSettings.useStagingEnvironment,
                "RealRuins.DebugSettings.UseStagingEnvironmentTooltip".Translate());

            SettingsControls.Section(listing, "RealRuins.DebugSettings.DebugCategories");
            DrawDebugCategoryButtons(listing);

            listing.Gap(SettingsControls.SectionGap);

            // Manual blueprint capture
            int clicked = SettingsControls.ButtonRow(listing,
                new string[] { "RealRuins.DebugSettings.ManualUpload".Translate() },
                new Color?[] { SettingsControls.ActionColor },
                new string[] { "RealRuins.DebugSettings.ManualUploadTooltip".Translate() });
            if (clicked == 0)
            {
                if (Find.CurrentMap != null)
                {
                    SnapshotManager.Instance.UploadCurrentMapSnapshot();
                    Messages.Message(
                        "RealRuins.DebugSettings.UploadTriggered".Translate(),
                        MessageTypeDefOf.NeutralEvent);
                }
                else
                {
                    Messages.Message(
                        "RealRuins.DebugSettings.NoMap".Translate(),
                        MessageTypeDefOf.RejectInput);
                }
            }
        }

        private string GetLogLevelLabel()
        {
            switch (RealRuins_ModSettings.logLevel)
            {
                case 0:
                    return "RealRuins.DebugSettings.LogLevelAll".Translate();
                case 1:
                    return "RealRuins.DebugSettings.LogLevelWarnings".Translate();
                case 2:
                    return "RealRuins.DebugSettings.LogLevelErrors".Translate();
                default:
                    return "Unknown";
            }
        }

        // As many equal columns as the longest category name allows
        private void DrawDebugCategoryButtons(Listing_Standard listing)
        {
            const float rowHeight = 26f;
            const float spacing = 6f;
            string[] categories = Debug.AllCategories;

            float labelWidth = 0f;
            foreach (string category in categories)
            {
                labelWidth = Mathf.Max(labelWidth, Text.CalcSize(category).x);
            }
            float width = listing.ColumnWidth;
            int columns = Mathf.Clamp(Mathf.FloorToInt((width + spacing) / (labelWidth + 16f + spacing)), 1, categories.Length);
            int rows = (categories.Length + columns - 1) / columns;
            float buttonWidth = (width - spacing * (columns - 1)) / columns;

            Rect baseRect = listing.GetRect(rows * rowHeight + (rows - 1) * spacing);
            for (int i = 0; i < categories.Length; i++)
            {
                string category = categories[i];
                Rect buttonRect = new Rect(
                    baseRect.x + (i % columns) * (buttonWidth + spacing),
                    baseRect.y + (i / columns) * (rowHeight + spacing),
                    buttonWidth, rowHeight).Rounded();
                bool isEnabled = Debug.extras.Contains(category);

                Color prevColor = GUI.color;
                GUI.color = isEnabled ? new Color(0.3f, 0.8f, 0.3f) : new Color(0.5f, 0.5f, 0.5f);
                if (Widgets.ButtonText(buttonRect, category))
                {
                    if (isEnabled)
                    {
                        Debug.extras.Remove(category);
                    }
                    else
                    {
                        Debug.extras.Add(category);
                    }
                    LoadedModManager.GetMod<RealRuins_Mod>().WriteSettings();
                }
                GUI.color = prevColor;
            }
        }
    }
}
