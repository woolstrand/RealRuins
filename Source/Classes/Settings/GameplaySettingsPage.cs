using System;
using Verse;

namespace RealRuins.Settings
{
    public class GameplaySettingsPage : SettingsPage
    {
        public override string TabLabel => "RealRuins.GameplaySettings.TabCaption".Translate();

        protected override void DrawContents(Listing_Standard listing)
        {
            SettingsControls.PageHeader(listing, "RealRuins.GameplaySettings.Caption", "RealRuins.GameplaySettings.Description");

            SettingsControls.Section(listing, "RealRuins.GameplaySettings.Events");
            SettingsControls.PristineRuinsQuest(listing);
            SettingsControls.CaravanRuinsEvent(listing);
            SettingsControls.CaravanReform(listing);
            SettingsControls.SmallRaidsKeepComing(listing);
            SettingsControls.DisableFriendlyRaids(listing);

            SettingsControls.Section(listing, "RealRuins.GameplaySettings.Planetary");
            SettingsControls.OfferPlanetaryAtStart(listing);
            SettingsControls.PlanetaryButtons(listing);
        }
    }
}
