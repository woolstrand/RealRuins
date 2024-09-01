using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace RealRuins;

internal class Page_PlanetaryRuinsLoader : Window
{
	private RuinsPageState pageState = RuinsPageState.Idle;

	private int blueprintsTotalCount = 0;

	private int blueprintsToLoadCount = 0;

	private int blueprintsLoadedCount = 0;

	private int blueprintsProcessedCount = 0;

	private int blueprintsUsed = 0;

	private int abandonedPercentage = (int)RealRuins_ModSettings.planetaryRuinsOptions.abandonedLocations;

	private bool biomeStrict = true;

	private bool costStrict = false;

	private bool areaStrict = false;

	private bool aggressiveDiscard = RealRuins_ModSettings.planetaryRuinsOptions.excludePlainRuins;

	private bool forceStopLoading = false;

	private bool forceStopTransfer = false;

	private bool showLoaderOptions = false;

	private bool showTranferrerOptions = false;

	private RuinsPageMode mode = RuinsPageMode.Default;

	private int downloadLimit = RealRuins_ModSettings.planetaryRuinsOptions.downloadLimit;

	private string downloadLimitString = RealRuins_ModSettings.planetaryRuinsOptions.downloadLimit.ToString();

	private int transferLimit = RealRuins_ModSettings.planetaryRuinsOptions.transferLimit;

	private string transferLimitString = RealRuins_ModSettings.planetaryRuinsOptions.transferLimit.ToString();

	private List<string> blueprintIds = null;

	private List<string> filteredList = null;

	private List<PlanetTileInfo> mapTiles;

	private APIService service = new APIService();

	public override Vector2 InitialSize => new Vector2(650f, 493f);

	public Page_PlanetaryRuinsLoader(bool forceCleanup = false)
	{
		doCloseX = true;
		if (forceCleanup)
		{
			PlanetaryRuinsInitData.shared.Cleanup();
			RemoveSites();
		}
	}

	public void DoWindowContents(Rect rect, bool standalone)
	{
		Listing_Standard listing_Standard = new Listing_Standard();
		listing_Standard.Begin(rect);
		if (standalone)
		{
			Text.Font = GameFont.Medium;
			listing_Standard.Label("RealRuins.LoadBlueprintsCaption".Translate());
			Text.Font = GameFont.Small;
		}
		World world = Find.World;
		if (world != null)
		{
			WorldInfo info = world.info;
			if (info != null)
			{
				_ = info.Seed;
				if (0 == 0)
				{
					if (showLoaderOptions && showTranferrerOptions)
					{
						listing_Standard.Label("RealRuins.OptionsDescription".Translate());
					}
					else
					{
						listing_Standard.Label("RealRuins.LoadBlueprintsDescription".Translate());
						listing_Standard.Label(new TaggedString("RealRuins.MapSizeChangeWarning".Translate().Colorize(Color.yellow)));
					}
					listing_Standard.Gap();
					DrawLoader(listing_Standard);
					DrawTransferrer(listing_Standard);
					bool flag = blueprintsUsed > 0 && pageState == RuinsPageState.Completed;
					Rect rect2 = rect.BottomPartPixels(flag ? 65 : 30);
					Listing_Standard listing_Standard2 = new Listing_Standard();
					listing_Standard2.Begin(rect2);
					if (flag && listing_Standard2.ButtonText("RealRuins.RemoveSites".Translate()))
					{
						RemoveSites();
					}
					if (standalone)
					{
						GUI.color = new Color(1f, 0.3f, 0.35f);
						if (listing_Standard2.ButtonText("RealRuins.Close".Translate()))
						{
							Find.WindowStack.TryRemove(this);
						}
						GUI.color = Color.white;
					}
					listing_Standard2.End();
					listing_Standard.End();
					return;
				}
			}
		}
		Text.Font = GameFont.Medium;
		listing_Standard.Label("RealRuins.NeedsGameInProgress".Translate());
		listing_Standard.End();
	}

	public void DrawLoader(Listing_Standard list)
	{
		Text.Font = GameFont.Small;
		string text = "RealRuins.Loading".Translate();
		string text2 = "RealRuins.DroppedMaps".Translate() + " --- ";
		string text3 = "RealRuins.UsedMaps".Translate() + " --- ";
		switch (pageState)
		{
		case RuinsPageState.Idle:
		{
			if (showLoaderOptions)
			{
				DrawLoadingOptions(list);
				if (list.ButtonText("RealRuins.GetCount".Translate()))
				{
					StartLoadingList();
				}
				break;
			}
			Listing_Standard listing_Standard = list.BeginSection(40f);
			listing_Standard.Gap(4f);
			listing_Standard.maxOneColumn = false;
			listing_Standard.ColumnWidth = (list.ColumnWidth - 20f) / 2f;
			if (listing_Standard.ButtonText("RealRuins.StartAuto".Translate()))
			{
				mode = RuinsPageMode.Default;
				StartLoadingList();
			}
			listing_Standard.NewColumn();
			listing_Standard.Gap(4f);
			if (listing_Standard.ButtonText("RealRuins.MoreOptions".Translate()))
			{
				showLoaderOptions = true;
				showTranferrerOptions = true;
				mode = RuinsPageMode.Manual;
			}
			list.EndSection(listing_Standard);
			break;
		}
		case RuinsPageState.LoadingHeader:
			text = "RealRuins.Searching".Translate();
			list.Label(text);
			break;
		case RuinsPageState.LoadedHeader:
			if (showLoaderOptions)
			{
				string label2 = string.Format("RealRuins.LoadedAmount".Translate(), blueprintsTotalCount);
				list.Label(label2);
				DrawLoadingOptions(list);
				if (blueprintsTotalCount < 30)
				{
					list.Label(new TaggedString("RealRuins.LowCountWarning".Translate().Colorize(Color.yellow)));
				}
				if (list.ButtonText("RealRuins.LoadBlueprints".Translate()))
				{
					LoadItems();
				}
			}
			break;
		case RuinsPageState.LoadingBlueprints:
		{
			text = string.Concat("RealRuins.Loading".Translate(), blueprintsLoadedCount.ToString(), " / ", blueprintsToLoadCount.ToString());
			list.Label(text);
			string label = "RealRuins.StopButtonTitle1".Translate();
			if (blueprintsLoadedCount > 500)
			{
				label = "RealRuins.StopButtonTitle2".Translate();
			}
			if (blueprintsLoadedCount > 1300)
			{
				label = "RealRuins.StopButtonTitle3".Translate();
			}
			if (list.ButtonText(label))
			{
				forceStopLoading = true;
			}
			break;
		}
		case RuinsPageState.LoadedBlueprints:
		case RuinsPageState.ProcessingBlueprints:
		case RuinsPageState.Completed:
			text = "RealRuins.Loading".Translate() + "RealRuins.Completed".Translate();
			list.Label(text);
			break;
		}
		list.GapLine();
	}

	public void DrawTransferrer(Listing_Standard list)
	{
		if (showLoaderOptions || mode == RuinsPageMode.Manual || pageState > RuinsPageState.LoadingBlueprints)
		{
			Text.Font = GameFont.Medium;
			list.Label("RealRuins.TransferringOptions".Translate());
			Text.Font = GameFont.Small;
		}
		string text = "RealRuins.Loading".Translate();
		string text2 = "RealRuins.DroppedMaps".Translate() + " --- ";
		string text3 = "RealRuins.UsedMaps".Translate() + " --- ";
		switch (pageState)
		{
		case RuinsPageState.Idle:
		case RuinsPageState.LoadedHeader:
			if (!showTranferrerOptions)
			{
				break;
			}
			DrawTransferOptions(list);
			if (list.ButtonText("RealRuins.LoadAndSpawnBlueprints".Translate()))
			{
				mode = RuinsPageMode.FullAuto;
				if (pageState == RuinsPageState.Idle)
				{
					StartLoadingList();
				}
				else
				{
					LoadItems();
				}
			}
			break;
		case RuinsPageState.LoadingHeader:
		case RuinsPageState.LoadingBlueprints:
			if (showTranferrerOptions)
			{
				DrawTransferOptions(list);
			}
			break;
		case RuinsPageState.LoadedBlueprints:
			if (showTranferrerOptions)
			{
				DrawTransferOptions(list);
				if (list.ButtonText("RealRuins.TransferBlueprints".Translate()))
				{
					CreateSites();
				}
			}
			break;
		case RuinsPageState.ProcessingBlueprints:
			text = string.Concat("RealRuins.Processing".Translate(), blueprintsProcessedCount.ToString(), " / ", filteredList.Count.ToString());
			text2 = string.Concat("RealRuins.DroppedMaps".Translate(), (blueprintsProcessedCount - blueprintsUsed).ToString());
			text3 = string.Concat("RealRuins.UsedMaps".Translate(), blueprintsUsed.ToString());
			list.Label(text);
			list.Label(text2);
			list.Label(text3);
			break;
		case RuinsPageState.Completed:
			text = "RealRuins.Transferring".Translate() + "RealRuins.Completed".Translate();
			list.Label(text);
			break;
		}
	}

	private void DrawLoadingOptions(Listing_Standard list)
	{
		downloadLimitString = list.TextEntryLabeled("RealRuins.DownloadLimit".Translate(), downloadLimitString);
		if (int.TryParse(downloadLimitString, out var result))
		{
			downloadLimit = result;
		}
	}

	private void DrawTransferOptions(Listing_Standard list)
	{
		transferLimitString = list.TextEntryLabeled("RealRuins.TransferLimit".Translate(), transferLimitString);
		if (int.TryParse(transferLimitString, out var result))
		{
			transferLimit = result;
		}
		abandonedPercentage = (int)list.SliderLabeled("RealRuins.AbandonedPercentage".Translate(), abandonedPercentage, 0f, 100f);
		list.CheckboxLabeled("RealRuins.BiomeFiltering".Translate(), ref biomeStrict, "RealRuins.BiomeFilteringTT".Translate());
		list.CheckboxLabeled("RealRuins.CostFiltering".Translate(), ref costStrict, "RealRuins.CostFilteringTT".Translate());
		list.CheckboxLabeled("RealRuins.AreaFiltering".Translate(), ref areaStrict, "RealRuins.AreaFilteringTT".Translate());
		list.CheckboxLabeled("RealRuins.DiscardAbandoned".Translate(), ref aggressiveDiscard, "RealRuins.DiscardAbandonedTT".Translate());
	}

	public override void DoWindowContents(Rect rect)
	{
		DoWindowContents(rect, standalone: true);
	}

	private void StartLoadingList()
	{
		pageState = RuinsPageState.LoadingHeader;
		Debug.Log("Loading list for seed: {0}", Find.World.info.seedString);
		string seedString = Find.World.info.seedString;
		int num = (int)(Find.World.PlanetCoverage * 100f);
		int num2 = 0;
		num2 = ((Find.GameInitData == null) ? Find.World.info.initialMapSize.x : Find.GameInitData.mapSize);
		service.LoadAllMapsForSeed(seedString, num2, num, delegate(bool success, List<PlanetTileInfo> mapTiles)
		{
			if (success)
			{
				if (mapTiles.Count == 0)
				{
					Close();
					SmallQuestionDialog window = new SmallQuestionDialog("RealRuins.NoBlueprintsWarning.Caption".Translate(), "RealRuins.NoBlueprintsWarning.Text".Translate(), new string[1] { "RealRuins.Close".Translate() }, null);
					Find.WindowStack.Add(window);
				}
				this.mapTiles = mapTiles;
				blueprintIds = new List<string>();
				foreach (PlanetTileInfo mapTile in mapTiles)
				{
					blueprintIds.Add(mapTile.mapId);
				}
				blueprintsTotalCount = blueprintIds.Count;
				pageState = RuinsPageState.LoadedHeader;
				Debug.Log("Loaded list of snapshot names, {0} elements", blueprintsTotalCount);
				if (blueprintsTotalCount > 500 && mode == RuinsPageMode.Default)
				{
					if (mode == RuinsPageMode.Default)
					{
						string text = string.Format("RealRuins.TooManyBlueprints.Load.Text".Translate(), blueprintsTotalCount);
						SmallQuestionDialog window2 = new SmallQuestionDialog("RealRuins.TooManyBlueprints.Load.Title".Translate(), text, new string[3]
						{
							"RealRuins.TooManyBlueprints.Get500".Translate(),
							"RealRuins.TooManyBlueprints.GetAll".Translate(),
							"RealRuins.TooManyBlueprints.ShowOptions".Translate()
						}, delegate(int selection)
						{
							switch (selection)
							{
							case 0:
								downloadLimit = 500;
								transferLimit = 500;
								mode = RuinsPageMode.FullAuto;
								LoadItems();
								break;
							case 1:
								downloadLimit = 0;
								transferLimit = 0;
								mode = RuinsPageMode.FullAuto;
								LoadItems();
								break;
							case 2:
								showLoaderOptions = true;
								showTranferrerOptions = true;
								mode = RuinsPageMode.Manual;
								break;
							}
						});
						Find.WindowStack.Add(window2);
					}
				}
				else
				{
					LoadItems();
				}
			}
			else
			{
				pageState = RuinsPageState.Idle;
			}
		});
	}

	private void LoadItems()
	{
		filteredList = null;
		if (downloadLimit == 0 || blueprintIds.Count() < downloadLimit)
		{
			filteredList = blueprintIds.ListFullCopy();
		}
		else
		{
			filteredList = blueprintIds.ListFullCopy().InRandomOrder().ToList()
				.GetRange(0, downloadLimit);
		}
		Debug.Log("POI", "limit: {0}, in list: {1}, loading: {2}", downloadLimit, blueprintIds.Count, filteredList.Count);
		if (pageState != RuinsPageState.LoadedHeader)
		{
			return;
		}
		pageState = RuinsPageState.LoadingBlueprints;
		SnapshotManager manager = new SnapshotManager();
		manager.Progress = delegate(int progress, int total)
		{
			blueprintsLoadedCount = progress;
			blueprintsToLoadCount = total;
			if (forceStopLoading)
			{
				manager.Stop();
				LoadingCompleted();
			}
		};
		manager.Completion = delegate
		{
			LoadingCompleted();
		};
		Debug.Log("POI", "Loading blueprints one by one...");
		manager.AggressiveLoadSnaphotsFromList(filteredList, SnapshotStoreManager.CurrentGamePath(), loadIfExists: false);
	}

	private void LoadingCompleted()
	{
		pageState = RuinsPageState.LoadedBlueprints;
		if (mode == RuinsPageMode.FullAuto || mode == RuinsPageMode.Default)
		{
			CreateSites();
		}
	}

	private void RemoveSites()
	{
		List<WorldObject> list = new List<WorldObject>();
		foreach (WorldObject allWorldObject in Find.WorldObjects.AllWorldObjects)
		{
			if (allWorldObject is RealRuinsPOIWorldObject)
			{
				list.Add(allWorldObject);
			}
		}
		foreach (WorldObject item in list)
		{
			Find.WorldObjects.Remove(item);
		}
		blueprintsUsed = 0;
		pageState = RuinsPageState.Idle;
	}

	private void CreateSites()
	{
		Debug.Log("POI", "Completed loading, creating sites.");
		new Thread((ThreadStart)delegate
		{
			pageState = RuinsPageState.ProcessingBlueprints;
			if (CreateSitesInt())
			{
				pageState = RuinsPageState.Completed;
			}
			else
			{
				pageState = RuinsPageState.Idle;
			}
		}).Start();
	}

	private bool CreateSitesInt()
	{
		foreach (PlanetTileInfo mapTile in mapTiles)
		{
			if (!filteredList.Contains(mapTile.mapId))
			{
				continue;
			}
			if (forceStopTransfer)
			{
				return false;
			}
			blueprintsProcessedCount++;
			if (biomeStrict && ((mapTile.originX == 0 && mapTile.originZ == 0) || mapTile.biomeName == null))
			{
				Debug.Log("POI", "Skipped: Biome filtering is on, but blueprint does not contain biome or location information");
				continue;
			}
			try
			{
				if (RealRuinsPOIFactory.CreatePOI(mapTile, SnapshotStoreManager.CurrentGamePath(), biomeStrict, costStrict, areaStrict, abandonedPercentage, aggressiveDiscard))
				{
					blueprintsUsed++;
					if (blueprintsUsed >= transferLimit && transferLimit != 0)
					{
						Debug.Log("POI", "Reached limit of {0} blueprints", transferLimit);
						return true;
					}
				}
				else
				{
					Debug.Log("POI", "CreatePOI returned false.");
				}
			}
			catch (Exception arg)
			{
				Debug.Log("POI", $"CreatePOI failed with exception {arg}");
			}
		}
		return true;
	}

	public override void PreOpen()
	{
		base.PreOpen();
	}
}
