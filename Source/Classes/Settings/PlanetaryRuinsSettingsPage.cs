using System;
using System.Collections.Generic;
using Verse;
using RimWorld.Planet;
using UnityEngine;

namespace RealRuins.Settings
{
    public class PlanetaryRuinsSettingsPage : SettingsPage
    {
        private string buf1 = "";
        private string buf2 = "";

        public override string TabLabel => "RealRuins.PlanetaryRuinsSettings.Caption".Translate();

        public override float ContentHeight => 390f;

        public override void Draw(Rect rect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(rect);

            GameFont font = Text.Font;
            Text.Font = GameFont.Medium;
            listing.Label("RealRuins.PlanetaryRuinsSettings.Caption".Translate());
            Text.Font = font;
            listing.GapLine();

            listing.CheckboxLabeled(
                "RealRuins.PlanetarySettings.Enable".Translate(),
                ref RealRuins_ModSettings.planetaryRuinsOptions.allowOnStart);

            listing.Gap(12f);

            Rect r = listing.GetRect(20);
            ReadableLabeledTextInput(
                r,
                "RealRuins.PlanetarySettings.DownloadLimit".Translate() + " ",
                ref RealRuins_ModSettings.planetaryRuinsOptions.downloadLimit,
                ref buf1);

            listing.Gap(12f);

            r = listing.GetRect(20);
            ReadableLabeledTextInput(
                r,
                "RealRuins.PlanetarySettings.TransferLimit".Translate() + "  ",
                ref RealRuins_ModSettings.planetaryRuinsOptions.transferLimit,
                ref buf2);

            listing.Gap(12f);

            listing.CheckboxLabeled(
                "RealRuins.PlanetarySettings.ExcludePlain".Translate(),
                ref RealRuins_ModSettings.planetaryRuinsOptions.excludePlainRuins);

            listing.Gap(12f);

            listing.CheckboxLabeled(
                "RealRuins.PlanetarySettings.DisableFriendly".Translate(),
                ref RealRuins_ModSettings.planetaryRuinsOptions.disableSpawnFriendlyLocations,
                "RealRuins.PlanetarySettings.DisableFriendlyTT".Translate());

            listing.Gap(12f);

            string sliderLabel = "RealRuins.PlanetarySettings.AbandonedPercentage".Translate() + ": " +
                                 ((int)RealRuins_ModSettings.planetaryRuinsOptions.abandonedLocations).ToString() + "%";
            RealRuins_ModSettings.planetaryRuinsOptions.abandonedLocations =
                listing.SliderLabeled(sliderLabel, RealRuins_ModSettings.planetaryRuinsOptions.abandonedLocations, 0.0f, 100.0f);

            listing.Gap(12f);

            Rect buttonRect = listing.GetRect(25f);
            Rect spawnButtonRect = buttonRect.LeftHalf().Rounded();
            Rect removeButtonRect = buttonRect.RightHalf().Rounded();
            
            Color prevColor = GUI.color;
            
            // Spawn button - Green
            GUI.color = new Color(0.3f, 1f, 0.3f);
            if (Widgets.ButtonText(spawnButtonRect, "RealRuins.MapsModuleButton".Translate()))
            {
                Page_PlanetaryRuinsLoader page = new Page_PlanetaryRuinsLoader();
                Find.WindowStack.Add(page);
            }
            
            // Remove button - Red
            GUI.color = new Color(1f, 0.3f, 0.3f);
            if (Widgets.ButtonText(removeButtonRect, "RealRuins.PlanetarySettings.RemoveAll".Translate()))
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "RealRuins.PlanetarySettings.RemoveAllConfirm".Translate(),
                    () => RemoveAllLocations(),
                    true,
                    null));
            }
            
            GUI.color = prevColor;

            listing.End();
        }

        private void RemoveAllLocations()
        {
            List<WorldObject> objectsToRemove = new List<WorldObject>();
            foreach (var obj in Find.WorldObjects.AllWorldObjects) {
                if (obj is RealRuinsPOIWorldObject) {
                    objectsToRemove.Add(obj);
                }
            }

            foreach (var obj in objectsToRemove) {
                Find.WorldObjects.Remove(obj);
            }
        }
    }
}
