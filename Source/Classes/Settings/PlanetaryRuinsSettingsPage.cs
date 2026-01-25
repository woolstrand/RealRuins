using System;
using Verse;
using UnityEngine;

namespace RealRuins.Settings
{
    public class PlanetaryRuinsSettingsPage : SettingsPage
    {
        private string buf1 = "";
        private string buf2 = "";

        public override string TabLabel => "RealRuins.PlanetaryRuinsSettings.Caption".Translate();

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

            listing.Gap(10f);

            Rect r = listing.GetRect(20);
            ReadableLabeledTextInput(
                r,
                "RealRuins.PlanetarySettings.DownloadLimit".Translate() + " ",
                ref RealRuins_ModSettings.planetaryRuinsOptions.downloadLimit,
                ref buf1);

            r = listing.GetRect(20);
            ReadableLabeledTextInput(
                r,
                "RealRuins.PlanetarySettings.TransferLimit".Translate() + "  ",
                ref RealRuins_ModSettings.planetaryRuinsOptions.transferLimit,
                ref buf2);

            listing.Gap(10f);

            listing.CheckboxLabeled(
                "RealRuins.PlanetarySettings.ExcludePlain".Translate(),
                ref RealRuins_ModSettings.planetaryRuinsOptions.excludePlainRuins);

            listing.Gap(10f);

            string sliderLabel = "RealRuins.PlanetarySettings.AbandonedPercentage".Translate() + ": " +
                                 ((int)RealRuins_ModSettings.planetaryRuinsOptions.abandonedLocations).ToString() + "%";
            RealRuins_ModSettings.planetaryRuinsOptions.abandonedLocations =
                listing.SliderLabeled(sliderLabel, RealRuins_ModSettings.planetaryRuinsOptions.abandonedLocations, 0.0f, 100.0f);

            listing.Gap(15f);

            if (listing.ButtonText("RealRuins.MapsModuleButton".Translate(), null))
            {
                Page_PlanetaryRuinsLoader page = new Page_PlanetaryRuinsLoader();
                Find.WindowStack.Add(page);
            }

            listing.End();
        }
    }
}
