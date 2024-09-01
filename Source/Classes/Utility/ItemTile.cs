using System;
using System.Collections.Generic;
using System.Xml;
using RimWorld;
using Verse;

namespace RealRuins;

internal class ItemTile : Tile
{
	public string stuffDef;

	public int stackCount;

	public long corpseDeathTime;

	public int rot;

	public bool isWall = false;

	public bool isDoor = false;

	public ItemArt art = null;

	public string attachedText = null;

	public List<ItemTile> innerItems;

	public string itemXml;

	public static ItemTile WallReplacementItemTile()
	{
		return WallReplacementItemTile(new IntVec3(0, 0, 0));
	}

	public static ItemTile WallReplacementItemTile(IntVec3 location)
	{
		return new ItemTile
		{
			defName = ThingDefOf.Wall.defName,
			stuffDef = ThingDefOf.BlocksGranite.defName,
			isDoor = false,
			isWall = true,
			stackCount = 1,
			rot = 0,
			cost = 0f,
			weight = 1f,
			location = location
		};
	}

	public static ItemTile DefaultDoorItemTile()
	{
		return DefaultDoorItemTile(new IntVec3(0, 0, 0));
	}

	public static ItemTile DefaultDoorItemTile(IntVec3 location)
	{
		ItemTile itemTile = new ItemTile();
		itemTile.defName = ThingDefOf.Door.defName;
		itemTile.stuffDef = ThingDefOf.WoodLog.defName;
		itemTile.isDoor = true;
		itemTile.isWall = false;
		itemTile.location = location;
		itemTile.stackCount = 1;
		itemTile.rot = 0;
		itemTile.cost = 0f;
		itemTile.weight = 1f;
		return itemTile;
	}

	public override string ToString()
	{
		string text = "";
		string text2 = "";
		if (stuffDef != null)
		{
			text = " of " + stuffDef;
		}
		if (stackCount > 1)
		{
			text2 = " (" + stackCount + " pcs)";
		}
		string text3 = "Tile \"" + defName + "\"" + text + text2 + ", $" + cost + ", " + weight + "kg";
		if (isWall)
		{
			text3 += ", wall";
		}
		if (isDoor)
		{
			text3 += ", door";
		}
		return text3;
	}

	public ItemTile()
	{
	}

	public ItemTile(XmlNode node)
	{
		defName = node.Attributes["def"].Value;
		itemXml = node.OuterXml;
		XmlAttribute xmlAttribute = node.Attributes["stuffDef"];
		if (xmlAttribute != null)
		{
			stuffDef = xmlAttribute.Value;
		}
		corpseDeathTime = long.Parse(node.Attributes["timeOfDeath"]?.Value ?? "0");
		XmlAttribute xmlAttribute2 = node.Attributes["stackCount"];
		if (xmlAttribute2 != null)
		{
			stackCount = int.Parse(xmlAttribute2.Value);
		}
		else
		{
			stackCount = 1;
		}
		XmlAttribute xmlAttribute3 = node.Attributes["isDoor"];
		if (xmlAttribute3 != null || defName.ToLower().Contains("door"))
		{
			isDoor = true;
		}
		XmlAttribute xmlAttribute4 = node.Attributes["text"];
		if (xmlAttribute4 != null)
		{
			attachedText = xmlAttribute4.Value;
		}
		XmlAttribute xmlAttribute5 = node.Attributes["actsAsWall"];
		if (xmlAttribute5 != null || defName.ToLower().Contains("wall") || defName.Equals("Cooler") || defName.Equals("Vent"))
		{
			isWall = true;
		}
		XmlAttribute xmlAttribute6 = node.Attributes["rot"];
		if (xmlAttribute6 != null)
		{
			rot = int.Parse(xmlAttribute6.Value);
		}
		else
		{
			rot = 0;
		}
		XmlAttribute xmlAttribute7 = node.Attributes["artTitle"];
		if (xmlAttribute7 != null)
		{
			if (art == null)
			{
				art = new ItemArt();
			}
			art.title = Uri.UnescapeDataString(xmlAttribute7.Value);
		}
		XmlAttribute xmlAttribute8 = node.Attributes["artAuthor"];
		if (xmlAttribute8 != null)
		{
			if (art == null)
			{
				art = new ItemArt();
			}
			art.author = Uri.UnescapeDataString(xmlAttribute8.Value);
		}
		XmlAttribute xmlAttribute9 = node.Attributes["artDescription"];
		if (xmlAttribute9 != null)
		{
			if (art == null)
			{
				art = new ItemArt();
			}
			art.text = Uri.UnescapeDataString(xmlAttribute9.Value);
		}
		if (!node.HasChildNodes)
		{
			return;
		}
		List<ItemTile> list = new List<ItemTile>();
		foreach (XmlNode childNode in node.ChildNodes)
		{
			if (childNode.Name.Equals("item"))
			{
				ItemTile item = new ItemTile(childNode);
				list.Add(item);
			}
		}
		if (list.Count > 0)
		{
			innerItems = list;
		}
	}
}
