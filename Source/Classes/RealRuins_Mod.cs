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
        }



        public void GetSettings() {
            GetSettings<RealRuins_ModSettings>();
        }

        public override void WriteSettings() {
            base.WriteSettings();
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
            listing_Standard.CheckboxLabeled(Text_Option_AllowDownloads, ref RealRuins_ModSettings.allowDownloads);
            listing_Standard.Gap(12f);
            listing_Standard.CheckboxLabeled(Text_Option_AllowUploads, ref RealRuins_ModSettings.allowUploads);
            listing_Standard.Gap(12f);
            listing_Standard.Label(Text_Option_CacheSize + "  " + ((int)(RealRuins_ModSettings.diskCacheLimit)).ToString() + " Mb", -1f, null);
            listing_Standard.End();
            listing_Standard2.Begin(rect3);
            listing_Standard2.Gap(84f);
            RealRuins_ModSettings.diskCacheLimit = listing_Standard2.Slider(RealRuins_ModSettings.diskCacheLimit, 20.0f, 2048.0f);
            listing_Standard2.End();
        }
    }
}
