using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace RealRuins;

public class RealRuins_Mod : Mod
{
	public static string Text_NetSettings_Category = "RealRuins_ModOptions_Category";

	public static string Text_MapSettings_Category = "RealRuins_MapOptions_Category";

	public static string Text_Option_AllowDownloads = "RealRuins_ModOptions_AllowDownloads";

	public static string Text_Option_AllowUploads = "RealRuins_ModOptions_AllowUploads";

	public static string Text_Option_CacheSize = "RealRuins_ModOptions_CacheSize";

	public static string Text_Option_OfflineMode = "RealRuins_ModOptions_OfflineMode";

	public static string Text_Option_CurrentCacheSize = "RealRuins_ModOptions_CurrentCacheSize";

	public static string Text_Option_RemoveAll = "RealRuins_ModOptions_RemoveAll";

	public static string Text_Option_ResetToDefaults = "RealRuins_ModOptions_Reset";

	public static string Text_Option_AllowDownloadsTooltip = "RealRuins_ModOptions_AllowDownloadsTooltip";

	public static string Text_Option_AllowUploadsTooltip = "RealRuins_ModOptions_AllowUploadsTooltip";

	public static string Text_Option_CacheSizeTooltip = "RealRuins_ModOptions_CacheSizeTooltip";

	public static string Text_Option_OfflineModeTooltip = "RealRuins_ModOptions_OfflineModeTooltip";

	public static string Text_Option_DownloadMore = "RealRuins_ModOptions_DownloadMore";

	public static string Text_Option_CurrentCacheCount = "RealRuins_ModOptions_CurrentCacheCount";

	public static string Text_Option_Density = "RealRuins_MapOptions_Density";

	public static string Text_Option_Size_Min = "RealRuins_MapOptions_Size";

	public static string Text_Option_Size_Max = "RealRuins_MapOptions_Size_Max";

	public static string Text_Option_Deterioration = "RealRuins_MapOptions_Deterioration";

	public static string Text_Option_Scavengers = "RealRuins_MapOptions_Scavengers";

	public static string Text_Option_CostLimit = "RealRuins_MapOptions_CostLimit";

	public static string Text_Option_DisableHaulables = "RealRuins_MapOptions_DisableHaulables";

	public static string Text_Option_WallsAndDoorsOnly = "RealRuins_MapOptions_WallsAndDoorsOnly";

	public static string Text_Option_DisableDecoration = "RealRuins_MapOptions_DisableDecoration";

	public static string Text_Option_DisableTraps = "RealRuins_MapOptions_DisableTraps";

	public static string Text_Option_DisableHostiles = "RealRuins_MapOptions_DisableHostiles";

	public static string Text_Option_Claimable = "RealRuins_MapOptions_Claimable";

	public static string Text_Option_Proximity = "RealRuins_MapOptions_EnableProximity";

	public static string Text_Option_DensityTT = "RealRuins_MapOptions_DensityTT";

	public static string Text_Option_SizeTT = "RealRuins_MapOptions_SizeTT";

	public static string Text_Option_DeteriorationTT = "RealRuins_MapOptions_DeteriorationTT";

	public static string Text_Option_ScavengersTT = "RealRuins_MapOptions_ScavengersTT";

	public static string Text_Option_CostLimitTT = "RealRuins_MapOptions_CostLimitTT";

	public static string Text_Option_DisableHaulablesTT = "RealRuins_MapOptions_DisableHaulablesTT";

	public static string Text_Option_WallsAndDoorsOnlyTT = "RealRuins_MapOptions_WallsAndDoorsOnlyTT";

	public static string Text_Option_DisableDecorationTT = "RealRuins_MapOptions_DisableDecorationTT";

	public static string Text_Option_DisableTrapsTT = "RealRuins_MapOptions_DisableTrapsTT";

	public static string Text_Option_DisableHostilesTT = "RealRuins_MapOptions_DisableHostilesTT";

	public static string Text_Option_ClaimableTT = "RealRuins_MapOptions_ClaimableTT";

	public static string Text_Option_ProximityTT = "RealRuins_MapOptions_EnableProximityTT";

	public static string Text_Option_CaravanReforming = "RealRuins_MapOptions_CaravanReforming";

	public static string Text_Option_CaravanReformingTT = "RealRuins_MapOptions_CaravanReformingTT";

	public static string Text_Option_StartWithourRuins = "RealRuins_MapOptions_StartWithoutRuins";

	public static string Text_Option_StartWithourRuinsTT = "RealRuins_MapOptions_StartWithoutRuinsTT";

	public static string[] CaravanReformOptions = new string[3] { "RealRuins.Reform.Automatic", "RealRuins.Reform.Instant", "RealRuins.Reform.Manual" };

	public static string[] LogLevelOptions = new string[3] { "RealRuins.LogLevel.All", "RealRuins.LogLevel.Warnings", "RealRuins.LogLevel.Errors" };

	private Vector2 scrollPosition = new Vector2(0f, 0f);

	private string buf1 = "";

	private string buf2 = "";

	public RealRuins_Mod(ModContentPack mcp)
		: base(mcp)
	{
		LongEventHandler.ExecuteWhenFinished(GetSettings);
		PostLoad();
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

	public void PostLoad()
	{
		StartApi.CreateSceneObject();
    }

	public override void WriteSettings()
	{
		base.WriteSettings();
		SnapshotStoreManager.Instance.CheckCacheContents();
		SnapshotStoreManager.Instance.CheckCacheSizeLimits();
	}

	public override string SettingsCategory()
	{
		return Text_NetSettings_Category.Translate();
	}

	private void ResetSettings()
	{
		RealRuins_ModSettings.Reset();
	}

	private void ReadableLabeledTextInput(Rect rect, string title, ref int value, ref string buffer)
	{
		Rect rect2 = rect.LeftHalf().Rounded();
		Rect rect3 = rect.RightPartPixels(100f);
		TextAnchor anchor = Text.Anchor;
		Text.Anchor = TextAnchor.MiddleLeft;
		Widgets.Label(rect2, title);
		Text.Anchor = anchor;
		Widgets.TextFieldNumeric(rect3, ref value, ref buffer);
	}

	public override void DoSettingsWindowContents(Rect rect)
	{
		Rect rect2 = new Rect(0f, 0f, rect.width - 20f, 1000f);
		Widgets.BeginScrollView(rect, ref scrollPosition, rect2);
		Rect rect3 = rect2.TopPartPixels(800f).LeftPart(0.45f).Rounded();
		Rect rect4 = rect2.TopPartPixels(800f).RightPart(0.45f).Rounded();
		Rect rect5 = rect2.BottomPartPixels(rect2.height - rect3.height);
		Listing_Standard listing_Standard = new Listing_Standard();
		Listing_Standard listing_Standard2 = new Listing_Standard();
		listing_Standard.Begin(rect3);
		listing_Standard.Label("RealRuins.CacheSettings.Caption".Translate());
		listing_Standard.GapLine();
		listing_Standard.CheckboxLabeled(Text_Option_OfflineMode.Translate(), ref RealRuins_ModSettings.offlineMode, Text_Option_OfflineModeTooltip.Translate());
		listing_Standard.Label(string.Concat(Text_Option_CurrentCacheSize.Translate() + " ", (SnapshotStoreManager.Instance.TotalSize() / 1048576).ToString(), " MB"));
		listing_Standard.Label(string.Concat(Text_Option_CurrentCacheCount.Translate() + " ", SnapshotStoreManager.Instance.StoredSnapshotsCount().ToString()));
		listing_Standard.Label(Text_Option_CacheSize.Translate() + "  " + ((int)RealRuins_ModSettings.diskCacheLimit).ToString() + " MB", -1f, Text_Option_CacheSizeTooltip.Translate());
		if (listing_Standard.ButtonText(Text_Option_DownloadMore.Translate() + " (50)"))
		{
			SnapshotManager.Instance.LoadSomeSnapshots(5);
		}
		if (listing_Standard.ButtonText(Text_Option_DownloadMore.Translate() + "(500)"))
		{
			for (int i = 0; i < 10; i++)
			{
				SnapshotManager.Instance.LoadSomeSnapshots();
			}
		}
		listing_Standard.Gap(25f);
		listing_Standard.Label("RealRuins.SpawnSettings.Caption".Translate());
		listing_Standard.GapLine();
		int minRadius = RealRuins_ModSettings.defaultScatterOptions.minRadius;
		int maxRadius = RealRuins_ModSettings.defaultScatterOptions.maxRadius;
		string text = "∞";
		if (RealRuins_ModSettings.defaultScatterOptions.itemCostLimit < 1000)
		{
			text = RealRuins_ModSettings.defaultScatterOptions.itemCostLimit.ToString();
		}
		string text2 = "∞";
		if ((double)RealRuins_ModSettings.ruinsCostCap < 999990000.0)
		{
			text2 = RealRuins_ModSettings.ruinsCostCap.ToString();
		}
		listing_Standard.Label(Text_Option_Density.Translate() + ": x" + RealRuins_ModSettings.defaultScatterOptions.densityMultiplier.ToString("F"), -1f, Text_Option_DensityTT.Translate());
		listing_Standard.Label(string.Concat(Text_Option_Size_Min.Translate() + ": ", RealRuins_ModSettings.defaultScatterOptions.minRadius.ToString()), -1f, Text_Option_SizeTT.Translate());
		listing_Standard.Label(string.Concat(Text_Option_Size_Max.Translate() + ": ", RealRuins_ModSettings.defaultScatterOptions.maxRadius.ToString()), -1f, Text_Option_SizeTT.Translate());
		listing_Standard.Gap(15f);
		listing_Standard.Label(Text_Option_Deterioration.Translate() + ": " + RealRuins_ModSettings.defaultScatterOptions.deteriorationMultiplier.ToString("F"), -1f, Text_Option_DeteriorationTT.Translate());
		listing_Standard.Label(Text_Option_Scavengers.Translate() + ": " + RealRuins_ModSettings.defaultScatterOptions.scavengingMultiplier.ToString("F"), -1f, Text_Option_ScavengersTT.Translate());
		listing_Standard.Label(Text_Option_CostLimit.Translate() + ": " + text, -1f, Text_Option_CostLimitTT.Translate());
		listing_Standard.Gap(15f);
		listing_Standard.Label(Text_Option_DisableDecoration.Translate(), -1f, Text_Option_DisableDecorationTT.Translate());
		listing_Standard.Label(Text_Option_DisableTraps.Translate(), -1f, Text_Option_DisableTrapsTT.Translate());
		listing_Standard.Label(Text_Option_DisableHostiles.Translate(), -1f, Text_Option_DisableHostilesTT.Translate());
		listing_Standard.Gap(15f);
		listing_Standard.Label("RealRuins.ForceMultiplier".Translate() + ": x" + RealRuins_ModSettings.forceMultiplier.ToString("F"), -1f, "RealRuins.ForceMultiplierTT".Translate());
		listing_Standard.Label("RealRuins.AbsoluteWealthCap".Translate() + ": " + text2, -1f, "RealRuins.AbsoluteWealthCapTT".Translate());
		listing_Standard.Gap(15f);
		listing_Standard.CheckboxLabeled(Text_Option_DisableHaulables.Translate(), ref RealRuins_ModSettings.defaultScatterOptions.disableSpawnItems, Text_Option_DisableHaulablesTT.Translate());
		listing_Standard.CheckboxLabeled(Text_Option_WallsAndDoorsOnly.Translate(), ref RealRuins_ModSettings.defaultScatterOptions.wallsDoorsOnly, Text_Option_WallsAndDoorsOnlyTT.Translate());
		listing_Standard.CheckboxLabeled(Text_Option_Proximity.Translate(), ref RealRuins_ModSettings.defaultScatterOptions.enableProximity, Text_Option_ProximityTT.Translate());
		listing_Standard.CheckboxLabeled(Text_Option_StartWithourRuins.Translate(), ref RealRuins_ModSettings.startWithoutRuins, Text_Option_StartWithourRuinsTT.Translate());
		listing_Standard.CheckboxLabeled("RealRuins.LeaveVanillaRuins".Translate(), ref RealRuins_ModSettings.preserveStandardRuins, "RealRuins.LeaveVanillaRuinsTT".Translate());
		Rect rect6 = listing_Standard.GetRect(30f);
		Widgets.Label(rect6.LeftHalf().ContractedBy(0f, 5f), "RealRuins.CaravanReformType".Translate());
		bool flag = Widgets.ButtonText(rect6.RightHalf(), CaravanReformOptions[Math.Min(2, RealRuins_ModSettings.caravanReformType)].Translate());
		listing_Standard.Gap(30f);
		if (flag)
		{
			List<FloatMenuOption> list = new List<FloatMenuOption>();
			for (int j = 0; j < 3; j++)
			{
				string label = CaravanReformOptions[j].Translate();
				int value2 = j;
				FloatMenuOption item = new FloatMenuOption(label, delegate
				{
					RealRuins_ModSettings.caravanReformType = value2;
				});
				list.Add(item);
			}
			Find.WindowStack.Add(new FloatMenu(list));
		}
		TooltipHandler.TipRegion(rect6, "RealRuins.CaravanReformTooltip".Translate());
		listing_Standard.Gap(4f);
		if (listing_Standard.ButtonText(Text_Option_ResetToDefaults.Translate()))
		{
			ResetSettings();
		}
		listing_Standard.End();
		listing_Standard2.Begin(rect4);
		listing_Standard2.Gap(32f);
		listing_Standard2.CheckboxLabeled(Text_Option_AllowDownloads.Translate(), ref RealRuins_ModSettings.allowDownloads, Text_Option_AllowDownloadsTooltip.Translate());
		listing_Standard2.CheckboxLabeled(Text_Option_AllowUploads.Translate(), ref RealRuins_ModSettings.allowUploads, Text_Option_AllowUploadsTooltip.Translate());
		listing_Standard2.Gap(25f);
		RealRuins_ModSettings.diskCacheLimit = listing_Standard2.Slider(RealRuins_ModSettings.diskCacheLimit, 20f, 2048f);
		if (listing_Standard2.ButtonText(Text_Option_RemoveAll.Translate()))
		{
			SnapshotStoreManager.Instance.ClearCache();
		}
		listing_Standard2.Gap(58f);
		if (RealRuins_ModSettings.defaultScatterOptions.minRadius > RealRuins_ModSettings.defaultScatterOptions.maxRadius)
		{
			RealRuins_ModSettings.defaultScatterOptions.minRadius = RealRuins_ModSettings.defaultScatterOptions.maxRadius;
		}
		listing_Standard2.Gap(32f);
		RealRuins_ModSettings.defaultScatterOptions.densityMultiplier = listing_Standard2.Slider(RealRuins_ModSettings.defaultScatterOptions.densityMultiplier, 0f, 20f);
		RealRuins_ModSettings.defaultScatterOptions.minRadius = (int)listing_Standard2.Slider(RealRuins_ModSettings.defaultScatterOptions.minRadius, 4f, 64f);
		RealRuins_ModSettings.defaultScatterOptions.maxRadius = (int)listing_Standard2.Slider(RealRuins_ModSettings.defaultScatterOptions.maxRadius, 4f, 64f);
		listing_Standard2.Gap();
		RealRuins_ModSettings.defaultScatterOptions.deteriorationMultiplier = listing_Standard2.Slider(RealRuins_ModSettings.defaultScatterOptions.deteriorationMultiplier, 0f, 1f);
		RealRuins_ModSettings.defaultScatterOptions.scavengingMultiplier = listing_Standard2.Slider(RealRuins_ModSettings.defaultScatterOptions.scavengingMultiplier, 0f, 5f);
		RealRuins_ModSettings.defaultScatterOptions.itemCostLimit = (int)listing_Standard2.Slider(RealRuins_ModSettings.defaultScatterOptions.itemCostLimit, 0f, 1000f);
		listing_Standard2.Gap();
		RealRuins_ModSettings.defaultScatterOptions.decorationChance = listing_Standard2.Slider(RealRuins_ModSettings.defaultScatterOptions.decorationChance, 0f, 0.01f);
		RealRuins_ModSettings.defaultScatterOptions.trapChance = listing_Standard2.Slider(RealRuins_ModSettings.defaultScatterOptions.trapChance, 0f, 0.01f);
		RealRuins_ModSettings.defaultScatterOptions.hostileChance = listing_Standard2.Slider(RealRuins_ModSettings.defaultScatterOptions.hostileChance, 0f, 1f);
		listing_Standard2.Gap();
		RealRuins_ModSettings.forceMultiplier = listing_Standard2.Slider(RealRuins_ModSettings.forceMultiplier, 0f, 2f);
		RealRuins_ModSettings.ruinsCostCap = (float)Math.Exp(listing_Standard2.Slider((float)Math.Log(RealRuins_ModSettings.ruinsCostCap), 6.908f, (float)Math.Log(1000000000.0)));
		Rect rect7 = listing_Standard2.GetRect(30f);
		Widgets.Label(rect7.LeftHalf().ContractedBy(0f, 5f), "RealRuins.LogLevel".Translate());
		bool flag2 = Widgets.ButtonText(rect7.RightHalf(), LogLevelOptions[Math.Min(2, RealRuins_ModSettings.logLevel)].Translate());
		listing_Standard2.Gap(30f);
		if (flag2)
		{
			List<FloatMenuOption> list2 = new List<FloatMenuOption>();
			for (int k = 0; k < 3; k++)
			{
				string label2 = LogLevelOptions[k].Translate();
				int value = k;
				FloatMenuOption item2 = new FloatMenuOption(label2, delegate
				{
					RealRuins_ModSettings.logLevel = value;
				});
				list2.Add(item2);
			}
			Find.WindowStack.Add(new FloatMenu(list2));
		}
		listing_Standard2.End();
		Listing_Standard listing_Standard3 = new Listing_Standard();
		listing_Standard3.Begin(rect5);
		listing_Standard3.Label("RealRuins.PlanetaryRuinsSettings.Caption".Translate());
		listing_Standard3.GapLine();
		listing_Standard3.CheckboxLabeled("RealRuins.PlanetarySettings.Enable".Translate(), ref RealRuins_ModSettings.planetaryRuinsOptions.allowOnStart);
		Rect rect8 = listing_Standard3.GetRect(20f);
		ReadableLabeledTextInput(rect8, "RealRuins.PlanetarySettings.DownloadLimit".Translate() + " ", ref RealRuins_ModSettings.planetaryRuinsOptions.downloadLimit, ref buf1);
		rect8 = listing_Standard3.GetRect(20f);
		ReadableLabeledTextInput(rect8, "RealRuins.PlanetarySettings.TransferLimit".Translate() + "  ", ref RealRuins_ModSettings.planetaryRuinsOptions.transferLimit, ref buf2);
		listing_Standard3.CheckboxLabeled("RealRuins.PlanetarySettings.ExcludePlain".Translate(), ref RealRuins_ModSettings.planetaryRuinsOptions.excludePlainRuins);
		string label3 = "RealRuins.PlanetarySettings.AbandonedPercentage".Translate() + ": " + ((int)RealRuins_ModSettings.planetaryRuinsOptions.abandonedLocations).ToString() + "%";
		RealRuins_ModSettings.planetaryRuinsOptions.abandonedLocations = listing_Standard3.SliderLabeled(label3, RealRuins_ModSettings.planetaryRuinsOptions.abandonedLocations, 0f, 100f);
		if (listing_Standard3.ButtonText("RealRuins.MapsModuleButton".Translate()))
		{
			Page_PlanetaryRuinsLoader window = new Page_PlanetaryRuinsLoader();
			//Find.WindowStack.TryRemove(typeof(Dialog_ModSettings));
			Find.WindowStack.Add(window);
		}
		listing_Standard3.End();
		Widgets.EndScrollView();
	}
}
