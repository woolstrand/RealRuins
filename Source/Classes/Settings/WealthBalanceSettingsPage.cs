using System;
using Verse;

namespace RealRuins.Settings
{
    public class WealthBalanceSettingsPage : SettingsPage
    {
        public override string TabLabel => "RealRuins.WealthSettings.TabCaption".Translate();

        protected override void DrawContents(Listing_Standard listing)
        {
            SettingsControls.PageHeader(listing, "RealRuins.WealthSettings.Caption", "RealRuins.WealthSettings.Description");

            SettingsControls.Section(listing, "RealRuins.WealthSettings.Limits");
            SettingsControls.RegularWealthLimit(listing);
            SettingsControls.CaravanWealthLimit(listing);
            SettingsControls.PristineWealthLimit(listing);
            SettingsControls.LocationsWealthLimit(listing);
            SettingsControls.ItemCostLimit(listing);
            SettingsControls.NoHaulables(listing);
            SettingsControls.WallsAndDoorsOnly(listing);

            SettingsControls.Section(listing, "RealRuins.WealthSettings.Scaling");
            SettingsControls.ScaleWithColonyWealth(listing);
            SettingsControls.RegularLimitCounting(listing);

            SettingsControls.Section(listing, "RealRuins.WealthSettings.Threats");
            SettingsControls.ThreatScale(listing);
            SettingsControls.InhabitantsChance(listing);
            SettingsControls.DisableFriendlyRaids(listing);
            SettingsControls.SmallRaidsKeepComing(listing);
            SettingsControls.NoRuinsOnStartingMap(listing);
        }
    }
}
