using System;
using Verse;
using RimWorld;
using UnityEngine;

namespace RealRuins.Settings
{
    public class DebugSettingsPage : SettingsPage
    {
        public override string TabLabel => "RealRuins.DebugSettings.Caption".Translate();

        public override void Draw(Rect rect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(rect);

            GameFont font = Text.Font;
            Text.Font = GameFont.Medium;
            listing.Label("RealRuins.DebugSettings.Caption".Translate());
            Text.Font = font;
            listing.GapLine();

            listing.CheckboxLabeled(
                "RealRuins.DebugSettings.KeepSnapshotsAfterUpload".Translate(),
                ref RealRuins_ModSettings.debugKeepSnapshotsAfterUpload,
                "RealRuins.DebugSettings.KeepSnapshotsAfterUploadTooltip".Translate());

            listing.Gap(15f);

            if (listing.ButtonText(
                "RealRuins.DebugSettings.ManualUpload".Translate(),
                "RealRuins.DebugSettings.ManualUploadTooltip".Translate()))
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

            listing.End();
        }
    }
}
