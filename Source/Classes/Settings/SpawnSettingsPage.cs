using System;
using Verse;

namespace RealRuins.Settings
{
    public class SpawnSettingsPage : SettingsPage
    {
        public override string TabLabel => "RealRuins.SpawnSettings.TabCaption".Translate();

        protected override void DrawContents(Listing_Standard listing)
        {
            SettingsControls.PageHeader(listing, "RealRuins.SpawnSettings.Caption", "RealRuins.SpawnSettings.Description");

            SettingsControls.Section(listing, "RealRuins.SpawnSettings.Common");
            SettingsControls.TechLevelCap(listing);
            SettingsControls.SpawnRoofs(listing);
            SettingsControls.NoHaulables(listing);
            SettingsControls.WallsAndDoorsOnly(listing);
            SettingsControls.Blacklists(listing);

            SettingsControls.Section(listing, "RealRuins.SpawnSettings.Regular");
            SettingsControls.TechCapAppliesToRegular(listing);
            SettingsControls.Density(listing);
            SettingsControls.FragmentSize(listing);
            SettingsControls.SettlementProximity(listing);
            SettingsControls.Deterioration(listing);
            SettingsControls.Scavengers(listing);
            SettingsControls.KeepVanillaRuins(listing);
            SettingsControls.NoRuinsOnStartingMap(listing);

            SettingsControls.Section(listing, "RealRuins.SpawnSettings.Events");
            SettingsControls.TechCapAppliesToEvents(listing);

            SettingsControls.Section(listing, "RealRuins.SpawnSettings.Planetary");
            SettingsControls.TechCapAppliesToLocations(listing);
            SettingsControls.PlanetaryLimits(listing);
            SettingsControls.DiscardPlainRuins(listing);
            SettingsControls.NoFriendlyLocations(listing);
            SettingsControls.AbandonedLocations(listing);
        }
    }
}
