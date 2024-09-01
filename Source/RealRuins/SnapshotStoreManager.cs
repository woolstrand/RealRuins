using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Verse;

namespace RealRuins;

internal class SnapshotStoreManager
{
	private static SnapshotStoreManager instance = null;

	private static string oldRootFolder = "../Snapshots";

	private long totalFilesSize = 0L;

	private int totalFileCount = 0;

	private static string snapshotsFolderPath = null;

	public static SnapshotStoreManager Instance
	{
		get
		{
			if (instance == null)
			{
				instance = new SnapshotStoreManager();
			}
			return instance;
		}
	}

	public static bool HasPlanetaryBlueprintForCurrentGame(string blueprintName)
	{
		string text = CurrentGamePath();
		bool flag = SnapshotExists(blueprintName, text);
		if (!flag)
		{
			Debug.Log("[PRELOAD CHECKER]", "{0} does not exist at {1}, scheduling load.", blueprintName, text);
		}
		return flag;
	}

	public static string GamePath(string seed, int mapSize, float coverage)
	{
		return $"{seed.SanitizeForFileSystem()}-{mapSize}-{(int)(coverage * 100f)}";
	}

	public static string CurrentGamePath()
	{
		return GamePath(Find.World.info.seedString, Find.World.info.initialMapSize.x, Find.World.PlanetCoverage);
	}

	public SnapshotStoreManager()
	{
		MoveFilesIfNeeded();
		GetSnapshotsFolderPath();
		RecalculateFilesSize();
	}

	private void MoveFilesIfNeeded()
	{
		if (!Directory.Exists(oldRootFolder))
		{
			return;
		}
		string text = GetSnapshotsFolderPath();
		if (text == oldRootFolder)
		{
			return;
		}
		string[] files = Directory.GetFiles(oldRootFolder);
		DateTime now = DateTime.Now;
		Debug.SysLog("Started moving {0} files at {1}", files.Length, now);
		string[] array = files;
		foreach (string text2 in array)
		{
			string fileName = Path.GetFileName(text2);
			string text3 = Path.Combine(text, fileName);
			try
			{
				if (!File.Exists(text3))
				{
					File.Move(text2, text3);
				}
				else
				{
					File.Delete(text2);
				}
			}
			catch
			{
			}
		}
		Debug.SysLog("finished at {0} ({1} msec)", DateTime.Now, (DateTime.Now - now).TotalMilliseconds);
		try
		{
			Directory.Delete(oldRootFolder);
		}
		catch
		{
		}
	}

	private static string GetSnapshotsFolderPath()
	{
		if (snapshotsFolderPath == null)
		{
			snapshotsFolderPath = Path.Combine(GenFilePaths.SaveDataFolderPath, "RealRuins");
			DirectoryInfo directoryInfo = new DirectoryInfo(snapshotsFolderPath);
			if (!directoryInfo.Exists)
			{
				try
				{
					directoryInfo.Create();
				}
				catch
				{
					snapshotsFolderPath = oldRootFolder;
				}
			}
		}
		return snapshotsFolderPath;
	}

	public void StoreData(string buffer, string blueprintName)
	{
		StoreBinaryData(Encoding.UTF8.GetBytes(buffer), blueprintName);
	}

	public void StoreBinaryData(byte[] buffer, string blueprintName, string gameName = null)
	{
		new Thread((ThreadStart)delegate
		{
			string text = blueprintName + ".bp";
			string text2 = GetSnapshotsFolderPath();
			if (gameName != null)
			{
				text2 = Path.Combine(text2, gameName);
			}
			if (RealRuins.SingleFile)
			{
				text = "jeluder.bp";
			}
			DirectoryInfo directoryInfo = new DirectoryInfo(text2);
			if (!directoryInfo.Exists)
			{
				try
				{
					directoryInfo.Create();
				}
				catch
				{
					Debug.Error("Store", "Can't access store path");
				}
			}
			string[] array = text.Split('=');
			if (array.Count() > 1)
			{
				int result = 0;
				if (int.TryParse(array[0], out result))
				{
					array[0] = "*";
					string searchPattern = string.Join("=", array);
					string[] files = Directory.GetFiles(text2, searchPattern);
					string[] array2 = files;
					foreach (string text3 in array2)
					{
						int result2 = 0;
						string[] array3 = text3.Split('-');
						if (int.TryParse(array3[0], out result2))
						{
							if (result2 > result)
							{
								return;
							}
							File.Delete(text3);
						}
					}
				}
			}
			File.WriteAllBytes(Path.Combine(text2, text), buffer);
			RecalculateFilesSize();
		}).Start();
	}

	private string DoGetRandomFilenameFromRootFolder()
	{
		if (RealRuins.SingleFile)
		{
			return RealRuins.SingleFileName;
		}
		string[] files = Directory.GetFiles(GetSnapshotsFolderPath());
		if (files.Length == 0)
		{
			return null;
		}
		int num = Rand.Range(0, files.Length);
		Debug.Log("Store", "files length: {0} count {1}, selected: {2}", files.Length, files.Count(), num);
		return files[num];
	}

	public string RandomSnapshotFilename()
	{
		string text = null;
		do
		{
			text = DoGetRandomFilenameFromRootFolder();
			if (text == null)
			{
				return null;
			}
		}
		while (text == null);
		return text;
	}

	public static string SnapshotNameFor(string snapshotId, string gameName)
	{
		if (gameName == null)
		{
			return GetSnapshotsFolderPath() + "/" + snapshotId + ".bp";
		}
		return GetSnapshotsFolderPath() + "/" + gameName + "/" + snapshotId + ".bp";
	}

	public static bool SnapshotExists(string snapshotId, string gameName)
	{
		return File.Exists(SnapshotNameFor(snapshotId, gameName));
	}

	public int StoredSnapshotsCount()
	{
		return totalFileCount;
	}

	public void RemoveBlueprintWithName(string filename)
	{
		try
		{
			File.Delete(filename);
		}
		catch (Exception)
		{
		}
	}

	public List<string> FilterOutExistingItems(List<string> source, string gamePath = null)
	{
		List<string> list = new List<string>();
		foreach (string item in source)
		{
			if (!File.Exists(SnapshotNameFor(item, gamePath)))
			{
				list.Add(item);
			}
		}
		return list;
	}

	public void CheckCacheContents()
	{
		if (RealRuins_ModSettings.offlineMode)
		{
			string[] files = Directory.GetFiles(GetSnapshotsFolderPath());
			string[] array = files;
			foreach (string text in array)
			{
				if (!text.Contains("local"))
				{
					File.Delete(text);
				}
			}
		}
		RecalculateFilesSize();
	}

	public void ClearCache()
	{
		Directory.Delete(GetSnapshotsFolderPath(), recursive: true);
		Directory.CreateDirectory(GetSnapshotsFolderPath());
		totalFilesSize = 0L;
	}

	public long TotalSize()
	{
		return totalFilesSize;
	}

	private void RecalculateFilesSize()
	{
		string[] files = Directory.GetFiles(GetSnapshotsFolderPath());
		long num = 0L;
		string[] array = files;
		foreach (string fileName in array)
		{
			num += new FileInfo(fileName).Length;
		}
		totalFilesSize = num;
		totalFileCount = files.Length;
	}

	public void CheckCacheSizeLimits()
	{
		string[] files = Directory.GetFiles(GetSnapshotsFolderPath());
		List<string> list = files.ToList();
		list.Sort();
		list.Reverse();
		long num = 0L;
		foreach (string item in list)
		{
			num += new FileInfo(item).Length;
			if ((float)num > RealRuins_ModSettings.diskCacheLimit * 1024f * 1024f)
			{
				File.Delete(item);
			}
		}
		RecalculateFilesSize();
	}

	public bool CanFireMediumEvent()
	{
		if (StoredSnapshotsCount() > 0)
		{
			if (RealRuins_ModSettings.offlineMode)
			{
				return true;
			}
			return StoredSnapshotsCount() > 30;
		}
		return false;
	}

	public bool CanFireLargeEvent()
	{
		if (StoredSnapshotsCount() > 0)
		{
			if (RealRuins_ModSettings.offlineMode)
			{
				return true;
			}
			return StoredSnapshotsCount() > 250;
		}
		return false;
	}
}
