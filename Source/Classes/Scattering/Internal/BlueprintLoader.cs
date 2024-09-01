using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using RealRuins.Classes.Utility;
using RimWorld;
using Verse;

namespace RealRuins;

internal class BlueprintLoader
{
	private string snapshotName = null;

	private Blueprint blueprint = null;

	public static bool CanLoadBlueprintAtPath(string path)
	{
		if (File.Exists(path) || File.Exists(path + ".xml"))
		{
			return true;
		}
		return false;
	}

	public static Blueprint LoadWholeBlueprintAtPath(string path)
	{
		BlueprintLoader blueprintLoader = new BlueprintLoader(path);
		try
		{
			blueprintLoader.LoadBlueprint();
			return blueprintLoader.blueprint;
		}
		catch (Exception)
		{
			Debug.Log("Loader", "[2] Exception while loading or processing blueprint");
			return null;
		}
	}

	public static Blueprint LoadRandomBlueprintPartAtPath(string path, IntVec3 size)
	{
		BlueprintLoader blueprintLoader = new BlueprintLoader(path);
		try
		{
			blueprintLoader.LoadBlueprint();
			blueprintLoader.CutRandomRectOfSize(size);
			return blueprintLoader.blueprint;
		}
		catch (Exception)
		{
			return null;
		}
	}

	public BlueprintLoader(string path)
	{
		snapshotName = path;
	}

	private void LoadBlueprint()
	{
		Debug.Log("BlueprintTransfer", "[0] Loading blueprint at path {0}", snapshotName);
		string text = snapshotName;
		if (Path.GetExtension(snapshotName).Equals(".bp"))
		{
			text = snapshotName + ".xml";
			if (!File.Exists(text))
			{
				string contents = Compressor.UnzipFile(snapshotName);
				File.WriteAllText(text, contents);
			}
		}
		XmlDocument xmlDocument = new XmlDocument();
		try
		{
			xmlDocument.Load(text);
			Debug.Log("BlueprintTransfer", "[1] Loaded XML file, XML is valid, processing...");
		}
		catch
		{
			Debug.Log("BlueprintTransfer", "[2] Failed to load, recovering...");
			BlueprintRecoveryService blueprintRecoveryService = new BlueprintRecoveryService(text);
			if (!blueprintRecoveryService.TryRecoverInPlace())
			{
				Debug.Log("BlueprintTransfer", "[2.1] Failed to recover");
				throw;
			}
			xmlDocument.Load(text);
			Debug.Log("BlueprintTransfer", "[2.1] Recovered!");
		}
		XmlNodeList elementsByTagName = xmlDocument.GetElementsByTagName("cell");
		int width = int.Parse(xmlDocument.FirstChild.Attributes["width"].Value);
		int height = int.Parse(xmlDocument.FirstChild.Attributes["height"].Value);
		int originX = int.Parse(xmlDocument.FirstChild.Attributes["x"].Value);
		int originZ = int.Parse(xmlDocument.FirstChild.Attributes["z"].Value);
		Version version = new Version(xmlDocument.FirstChild?.Attributes["version"]?.Value ?? "0.0.0.0");
		blueprint = new Blueprint(originX, originZ, width, height, version);
		blueprint.snapshotYear = int.Parse(xmlDocument.FirstChild.Attributes["inGameYear"]?.Value ?? "5600");
		int num = 0;
		int num2 = 0;
		foreach (XmlNode item in elementsByTagName)
		{
			int num3 = int.Parse(item.Attributes["x"].Value);
			int num4 = int.Parse(item.Attributes["z"].Value);
			blueprint.itemsMap[num3, num4] = new List<ItemTile>();
			foreach (XmlNode childNode in item.ChildNodes)
			{
				try
				{
					if (childNode.Name.Equals("terrain"))
					{
						num2++;
						TerrainTile terrainTile = new TerrainTile(childNode);
						terrainTile.location = new IntVec3(num3, 0, num4);
						blueprint.terrainMap[num3, num4] = terrainTile;
					}
					else
					{
						if (childNode.Name.Equals("item"))
						{
							num++;
							ItemTile itemTile = new ItemTile(childNode);
							if (itemTile.defName == ThingDefOf.CollapsedRocks.defName)
							{
								itemTile = ItemTile.WallReplacementItemTile(itemTile.location);
							}
							if (itemTile.defName == ThingDefOf.MinifiedThing.defName && (itemTile.innerItems?.Count() ?? 0) == 0)
							{
								continue;
							}
							ThingDef named = DefDatabase<ThingDef>.GetNamed(itemTile.defName, errorOnFail: false);
							if (named != null)
							{
								if (named.fillPercent == 1f || itemTile.isWall || itemTile.isDoor)
								{
									blueprint.wallMap[num3, num4] = -1;
								}
								itemTile.stackCount = Math.Min(named.stackLimit, itemTile.stackCount);
								itemTile.location = new IntVec3(num3, 0, num4);
								blueprint.itemsMap[num3, num4].Add(itemTile);
							}
							else if (itemTile.isDoor)
							{
								itemTile.defName = ThingDefOf.Door.defName;
								itemTile.location = new IntVec3(num3, 0, num4);
								blueprint.itemsMap[num3, num4].Add(itemTile);
							}
							else if (itemTile.isWall || itemTile.defName.ToLower().Contains("wall"))
							{
								itemTile.defName = ThingDefOf.Wall.defName;
								itemTile.location = new IntVec3(num3, 0, num4);
								blueprint.itemsMap[num3, num4].Add(itemTile);
							}
							else if (itemTile.defName == "Corpse")
							{
								itemTile.location = new IntVec3(num3, 0, num4);
								blueprint.itemsMap[num3, num4].Add(itemTile);
							}
							continue;
						}
						if (childNode.Name.Equals("roof"))
						{
							blueprint.roofMap[num3, num4] = true;
						}
					}
				}
				catch (Exception)
				{
				}
			}
		}
	}

	private void CutRandomRectOfSize(IntVec3 size)
	{
		int newX = blueprint.width / 2;
		int newZ = blueprint.height / 2;
		if (blueprint.width > size.x)
		{
			newX = Rand.Range(size.x / 2, blueprint.width - size.x / 2);
		}
		if (blueprint.height > size.z)
		{
			newZ = Rand.Range(size.z / 2, blueprint.height - size.z / 2);
		}
		blueprint = blueprint.Part(new IntVec3(newX, 0, newZ), size);
	}
}
