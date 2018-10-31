using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Verse;
using RimWorld;
using UnityEngine;

namespace RealRuins {
    public class RealRuins_Mod : Mod {


        public static string Text_Category = "RealRuins_ModOptions_Category";
        public static string Text_Option_AllowDownloads = "RealRuins_ModOptions_AllowDownloads";
        public static string Text_Option_AllowUploads = "RealRuins_ModOptions_AllowUploads";
        public static string Text_Option_CacheSize = "RealRuins_ModOptions_CacheSize";
        public static string Text_Option_OfflineMode = "RealRuins_ModOptions_OfflineMode";

        public static string Text_Option_CurrentCacheSize = "RealRuins_ModOptions_CurrentCacheSize";
        public static string Text_Option_RemoveAll = "RealRuins_ModOptions_RemoveAll";

        public static string Text_Option_AllowDownloadsTooltip = "RealRuins_ModOptions_AllowDownloadsTooltip";
        public static string Text_Option_AllowUploadsTooltip = "RealRuins_ModOptions_AllowUploadsTooltip";
        public static string Text_Option_CacheSizeTooltip = "RealRuins_ModOptions_CacheSizeTooltip";
        public static string Text_Option_OfflineModeTooltip = "RealRuins_ModOptions_OfflineModeTooltip";


        public RealRuins_Mod(ModContentPack mcp)
        : base(mcp) {
            LongEventHandler.ExecuteWhenFinished(SetTexts);
            LongEventHandler.ExecuteWhenFinished(GetSettings);
        }

        public void SetTexts() {
            Text_Category = Text_Category.Translate();
            Text_Option_AllowDownloads = Text_Option_AllowDownloads.Translate();
            Text_Option_AllowUploads = Text_Option_AllowUploads.Translate();
            Text_Option_CacheSize = Text_Option_CacheSize.Translate();
            Text_Option_OfflineMode = Text_Option_OfflineMode.Translate();

            Text_Option_AllowDownloadsTooltip = Text_Option_AllowDownloadsTooltip.Translate();
            Text_Option_AllowUploadsTooltip = Text_Option_AllowUploadsTooltip.Translate();
            Text_Option_CacheSizeTooltip = Text_Option_CacheSizeTooltip.Translate();
            Text_Option_OfflineModeTooltip = Text_Option_OfflineModeTooltip.Translate();

            Text_Option_CurrentCacheSize = Text_Option_CurrentCacheSize.Translate();
            Text_Option_RemoveAll = Text_Option_RemoveAll.Translate();
        }



        public void GetSettings() {
            GetSettings<RealRuins_ModSettings>();
        }

        public override void WriteSettings() {
            base.WriteSettings();
            SnapshotStoreManager.Instance.CheckCacheContents();
            SnapshotStoreManager.Instance.CheckCacheSizeLimits();
        }

        public override string SettingsCategory() {
            return Text_Category;
        }

        public override void DoSettingsWindowContents(Rect rect) {
            Rect rect2 = rect.LeftHalf().Rounded();
            Rect rect3 = rect.RightHalf().Rounded();
            Listing_Standard listing_Standard = new Listing_Standard();
            Listing_Standard listing_Standard2 = new Listing_Standard();
            listing_Standard.Begin(rect2);
            listing_Standard.CheckboxLabeled(Text_Option_OfflineMode, ref RealRuins_ModSettings.offlineMode, Text_Option_OfflineModeTooltip);
            listing_Standard.Gap(12f);
            listing_Standard.CheckboxLabeled(Text_Option_AllowDownloads, ref RealRuins_ModSettings.allowDownloads, Text_Option_AllowDownloadsTooltip);
            listing_Standard.Gap(12f);
            listing_Standard.CheckboxLabeled(Text_Option_AllowUploads, ref RealRuins_ModSettings.allowUploads, Text_Option_AllowUploadsTooltip);
            listing_Standard.Gap(12f);
            listing_Standard.Label(Text_Option_CacheSize + "  " + ((int)(RealRuins_ModSettings.diskCacheLimit)).ToString() + " MB", -1f, Text_Option_CacheSizeTooltip);
            listing_Standard.Label(Text_Option_CurrentCacheSize + " " + SnapshotStoreManager.Instance.TotalSize() / (1024 * 1024) + " MB");
            listing_Standard.End();
            listing_Standard2.Begin(rect3);
            listing_Standard2.Gap(102f);
            RealRuins_ModSettings.diskCacheLimit = listing_Standard2.Slider(RealRuins_ModSettings.diskCacheLimit, 20.0f, 2048.0f);
            if (listing_Standard2.ButtonText(Text_Option_RemoveAll, null)) {
                SnapshotStoreManager.Instance.ClearCache();
            }
            listing_Standard2.End();
        }
    }
}
