using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Verse;

namespace RealRuins;

internal class SnapshotManager
{
	private static HashSet<Timer> timers = new HashSet<Timer>();

	private static SnapshotManager instance = null;

	private readonly SnapshotStoreManager storeManager = SnapshotStoreManager.Instance;

	private static Dictionary<string, DateTime> snapshotTimestamps = new Dictionary<string, DateTime>();

	private List<string> snapshotsToLoad = new List<string>();

	private int snapshotsToLoadCount = 0;

	private int loadedSnapshotsCount = 0;

	private int failedSnapshotsCount = 0;

	private bool loadIfExists = true;

	private Action<int, int> progress;

	private Action<bool> completion;

	private bool forceStop = false;

	public static SnapshotManager Instance
	{
		get
		{
			if (instance == null)
			{
				instance = new SnapshotManager();
			}
			return instance;
		}
	}

	public Action<int, int> Progress
	{
		set
		{
			progress = value;
		}
	}

	public Action<bool> Completion
	{
		set
		{
			completion = value;
		}
	}

	private static void ExecuteAfter(Action action, TimeSpan delay)
	{
		Timer timer = null;
		timer = new Timer(delegate
		{
			action();
			timer.Dispose();
			lock (timers)
			{
				timers.Remove(timer);
			}
		}, null, (long)delay.TotalMilliseconds, -1L);
		lock (timers)
		{
			timers.Add(timer);
		}
	}

	public void Reset()
	{
		snapshotsToLoadCount = 0;
		loadedSnapshotsCount = 0;
		failedSnapshotsCount = 0;
		snapshotsToLoad = new List<string>();
	}

	public void Stop()
	{
		forceStop = true;
	}

	public void AggressiveLoadSnapshots()
	{
		APIService aPIService = new APIService();
		Debug.Log("Store", "Snapshot pool is almost empty, doing some aggressive loading...", true);
		aPIService.LoadRandomMapsList(delegate(bool success, List<string> files)
		{
			if (!success)
			{
				Debug.Log("Store", "Failed loading list of random maps. Rescheduling after 10 seconds");
				ExecuteAfter(delegate
				{
					AggressiveLoadSnapshots();
				}, new TimeSpan(0, 0, 20));
			}
			else
			{
				Debug.Log("Store", "Loaded list of {0} elements...", files.Count);
				files = storeManager.FilterOutExistingItems(files);
				foreach (string file in files)
				{
					snapshotsToLoad.Add(file);
				}
				Debug.Log("Store", "Loading {0} files...", snapshotsToLoad.Count);
				AggressiveLoadSnaphotsFromList(snapshotsToLoad);
			}
		});
	}

	public void AggressiveLoadSnaphotsFromList(List<string> snapshotsToLoad, string gamePath = null, bool loadIfExists = true)
	{
		this.loadIfExists = loadIfExists;
		if (!loadIfExists)
		{
			this.snapshotsToLoad = storeManager.FilterOutExistingItems(snapshotsToLoad, gamePath);
			Debug.Log("Store", "Filtered snapshots {0} -> {1}", snapshotsToLoad.Count, this.snapshotsToLoad.Count);
		}
		else
		{
			this.snapshotsToLoad = snapshotsToLoad;
		}
		snapshotsToLoadCount = this.snapshotsToLoad.Count;
		int num = Math.Min(10, snapshotsToLoadCount);
		if (snapshotsToLoadCount > 0)
		{
			for (int i = 0; i < num; i++)
			{
				LoadNextSnapshot(gamePath);
			}
		}
		else
		{
			completion?.Invoke(obj: true);
		}
	}

	public void LoadSomeSnapshots(int concurrent = 1, int retries = 10)
	{
		if (snapshotsToLoad.Count > 0)
		{
			return;
		}
		loadIfExists = true;
		APIService aPIService = new APIService();
		Debug.Log("Store", "Loading some snapshots...", true);
		aPIService.LoadRandomMapsList(delegate(bool success, List<string> files)
		{
			if (!success)
			{
				Debug.Log("Store", "Failed loading list of random maps");
				ExecuteAfter(delegate
				{
					LoadSomeSnapshots(concurrent, retries - 1);
				}, new TimeSpan(0, 0, 20));
			}
			else
			{
				Debug.Log("Store", "Loaded list of {0} elements...", files.Count);
				files = storeManager.FilterOutExistingItems(files);
				foreach (string file in files)
				{
					snapshotsToLoad.Add(file);
				}
				Debug.Log("Store", "Loading {0} files...", snapshotsToLoad.Count);
				if (snapshotsToLoad.Count > 0)
				{
					for (int i = 0; i < concurrent; i++)
					{
						LoadNextSnapshot();
					}
				}
			}
		});
	}

	private void LoadNextSnapshot(string gamePath = null)
	{
		if (forceStop)
		{
			return;
		}
		string next = snapshotsToLoad.Pop();
		Debug.Log("Store", "Loading snapshot {0}", next);
		APIService aPIService = new APIService();
		aPIService.LoadMap(next, delegate(bool success, byte[] data)
		{
			if (success)
			{
				loadedSnapshotsCount++;
				storeManager.StoreBinaryData(data, next, gamePath);
			}
			else
			{
				failedSnapshotsCount++;
				Debug.Warning("Store", "Failed loading snapshot {0}", next);
			}
			progress?.Invoke(loadedSnapshotsCount, snapshotsToLoadCount);
			if (snapshotsToLoad.Count > 0)
			{
				LoadNextSnapshot(gamePath);
			}
			else if (loadedSnapshotsCount + failedSnapshotsCount == snapshotsToLoadCount)
			{
				completion?.Invoke(obj: true);
			}
		});
	}

	public void UploadCurrentMapSnapshot()
	{
		if (Find.CurrentMap == null)
		{
			return;
		}
		string text = Math.Abs(Find.World.info.persistentRandomValue).ToString();
		string text2 = Find.CurrentMap.uniqueID.ToString();
		string text3 = text + text2;
		if (snapshotTimestamps.ContainsKey(text3) && (DateTime.Now - snapshotTimestamps[text3]).TotalMinutes < 180.0)
		{
			return;
		}
		snapshotTimestamps[text3] = DateTime.Now;
		SnapshotGenerator snapshotGenerator = new SnapshotGenerator(Find.CurrentMap);
		if (!snapshotGenerator.CanGenerate())
		{
			return;
		}
		string tmpFilename = snapshotGenerator.Generate();
		if (tmpFilename == null)
		{
			return;
		}
		Compressor.ZipFile(tmpFilename);
		if (RealRuins.SingleFile)
		{
			SnapshotStoreManager.Instance.StoreBinaryData(File.ReadAllBytes(tmpFilename), "jeluder.bp");
			return;
		}
		if (RealRuins_ModSettings.offlineMode)
		{
			SnapshotStoreManager.Instance.StoreBinaryData(File.ReadAllBytes(tmpFilename), "local-" + text3 + ".bp");
			return;
		}
		Debug.Log("Store", "Uploading file {0}", tmpFilename);
		APIService aPIService = new APIService();
		aPIService.UploadMap(tmpFilename, delegate(bool success)
		{
			File.Delete(tmpFilename);
			completion?.Invoke(success);
		});
	}
}
