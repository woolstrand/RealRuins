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

            // Log Level Selector
            Rect logLevelRect = listing.GetRect(25f);
            Widgets.Label(
                logLevelRect.LeftHalf().ContractedBy(0, 0),
                "RealRuins.DebugSettings.LogLevel".Translate());
            
            if (Widgets.ButtonText(logLevelRect.RightHalf().ContractedBy(0, 3), GetLogLevelLabel()))
            {
                var options = new System.Collections.Generic.List<FloatMenuOption>();
                options.Add(new FloatMenuOption("RealRuins.DebugSettings.LogLevelAll".Translate(), () => RealRuins_ModSettings.logLevel = 0));
                options.Add(new FloatMenuOption("RealRuins.DebugSettings.LogLevelWarnings".Translate(), () => RealRuins_ModSettings.logLevel = 1));
                options.Add(new FloatMenuOption("RealRuins.DebugSettings.LogLevelErrors".Translate(), () => RealRuins_ModSettings.logLevel = 2));
                Find.WindowStack.Add(new FloatMenu(options));
            }

            listing.Gap(15f);

            listing.CheckboxLabeled(
                "RealRuins.DebugSettings.KeepSnapshotsAfterUpload".Translate(),
                ref RealRuins_ModSettings.debugKeepSnapshotsAfterUpload,
                "RealRuins.DebugSettings.KeepSnapshotsAfterUploadTooltip".Translate());

            listing.Gap(15f);

            listing.CheckboxLabeled(
                "RealRuins.DebugSettings.LogRuinsWealth".Translate(),
                ref RealRuins_ModSettings.debugLogRuinsWealth,
                "RealRuins.DebugSettings.LogRuinsWealthTooltip".Translate());

            listing.Gap(15f);

            listing.CheckboxLabeled(
                "RealRuins.DebugSettings.UseStagingEnvironment".Translate(),
                ref RealRuins_ModSettings.useStagingEnvironment,
                "RealRuins.DebugSettings.UseStagingEnvironmentTooltip".Translate());

            listing.Gap(15f);

            // Debug Categories
            listing.Label("RealRuins.DebugSettings.DebugCategories".Translate());
            DrawDebugCategoryButtons(listing);

            listing.Gap(15f);

            // Manual blueprint capture
            Rect uploadRect = listing.GetRect(35f);
            Color prevColor = GUI.color;
            GUI.color = new Color(0.3f, 0.8f, 0.3f);
            if (Widgets.ButtonText(uploadRect, "RealRuins.DebugSettings.ManualUpload".Translate()))
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
            GUI.color = prevColor;
            TooltipHandler.TipRegion(uploadRect, "RealRuins.DebugSettings.ManualUploadTooltip".Translate());
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

        private void DrawDebugCategoryButtons(Listing_Standard listing)
        {
            float buttonWidth = 160f;
            float spacing = 5f;
            float rowHeight = 25f;
            
            // Calculate required height based on number of categories
            int categoriesCount = Debug.AllCategories.Length;
            float requiredHeight = ((categoriesCount + 2) / 3) * (rowHeight + spacing) + spacing;
            
            Rect baseRect = listing.GetRect(requiredHeight);
            float x = baseRect.x;
            float y = baseRect.y;
            float maxWidth = baseRect.width;
            float currentX = x;
            float currentY = y;

            foreach (string category in Debug.AllCategories)
            {
                float requiredWidth = buttonWidth + spacing;
                if (currentX + requiredWidth > x + maxWidth)
                {
                    currentX = x;
                    currentY += rowHeight + spacing;
                }

                Rect buttonRect = new Rect(currentX, currentY, buttonWidth, rowHeight);
                bool isEnabled = Debug.extras.Contains(category);
                
                Color bgColor = isEnabled ? new Color(0.3f, 0.8f, 0.3f) : new Color(0.5f, 0.5f, 0.5f);
                Color prevColor = GUI.color;
                GUI.color = bgColor;
                
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
                currentX += buttonWidth + spacing;
            }
        }
    }
}
