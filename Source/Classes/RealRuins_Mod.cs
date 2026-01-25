using System;
using Verse;
using UnityEngine;
using RealRuins.Settings;

namespace RealRuins
{
    public class RealRuins_Mod : Mod
    {
        private RealRuinsSettingsWindow settingsWindow;

        public RealRuins_Mod(ModContentPack mcp)
            : base(mcp)
        {
            Debug.SysLog("Loaded RealRuins_Mod (no HugsLib)");
            LongEventHandler.ExecuteWhenFinished(GetSettings);
        }

        public void GetSettings()
        {
            GetSettings<RealRuins_ModSettings>();
            if (RealRuins_ModSettings.defaultScatterOptions == null)
            {
                Debug.Warning("Scatter settings is null! setting default");
                RealRuins_ModSettings.defaultScatterOptions = ScatterOptions.Default;
            }
        }

        public override void WriteSettings()
        {
            base.WriteSettings();
            SnapshotStoreManager.Instance.CheckCacheContents();
            SnapshotStoreManager.Instance.CheckCacheSizeLimits();
        }

        public override string SettingsCategory()
        {
            return "RealRuins_ModOptions_Category".Translate();
        }

        public override void DoSettingsWindowContents(Rect rect)
        {
            if (settingsWindow == null)
            {
                settingsWindow = new RealRuinsSettingsWindow();
            }
            
            settingsWindow.DoSettingsWindowContents(rect);
        }
    }
}
