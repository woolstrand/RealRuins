using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using RimWorld;
using Verse;

namespace RealRuins;

internal class SnapshotGenerator
{
	private Map map;

	private int maxHPItemsCount = 0;

	private int itemsCount = 0;

	private int terrainCount = 0;

	public SnapshotGenerator(Map map)
	{
		this.map = map;
	}

	public bool CanGenerate()
	{
		if (map.areaManager.Home.ActiveCells.Count() < 300)
		{
			return false;
		}
		return true;
	}

	private void EncodePawn(Pawn pawn, XmlWriter writer)
	{
		writer.WriteStartElement("item");
		writer.WriteAttributeString("def", "Pawn");
		writer.WriteElementString("kind", pawn.kindDef.defName);
		writer.WriteStartElement("name");
		if (pawn.Name is NameSingle)
		{
			writer.WriteAttributeString("first", ((NameSingle)pawn.Name).Name);
		}
		else if (pawn.Name is NameTriple)
		{
			writer.WriteAttributeString("first", ((NameTriple)pawn.Name).First);
			writer.WriteAttributeString("last", ((NameTriple)pawn.Name).Last);
			writer.WriteAttributeString("nick", ((NameTriple)pawn.Name).Nick);
		}
		else if (pawn.Name != null && pawn.Name.ToStringFull != null)
		{
			writer.WriteAttributeString("first", pawn.Name.ToStringFull);
		}
		else
		{
			writer.WriteAttributeString("first", "Unknown");
		}
		writer.WriteEndElement();
		writer.WriteElementString("gender", pawn.gender.ToString());
		writer.WriteElementString("biologicalAge", pawn.ageTracker.AgeBiologicalTicks.ToString());
		writer.WriteElementString("chronologicalAge", pawn.ageTracker.AgeBiologicalTicks.ToString());
		if (pawn.story != null)
		{
			string text = Scribe.saver.DebugOutputFor(pawn.story);
			writer.WriteRaw(text ?? "");
		}
		if (pawn.apparel != null && pawn.apparel.WornApparelCount > 0)
		{
			Debug.Log("BlueprintGen", "starting apparel");
			writer.WriteStartElement("apparel");
			foreach (Apparel item in pawn.apparel.WornApparel)
			{
				Debug.Log("BlueprintGen", "Trying {0}", item);
				string text2 = item.def?.defName;
				string text3 = item.Stuff?.defName;
				if (text2 != null)
				{
					writer.WriteStartElement("item");
					writer.WriteAttributeString("def", text2);
					if (text3 != null)
					{
						writer.WriteAttributeString("stuffDef", text3);
					}
					writer.WriteEndElement();
				}
			}
			writer.WriteEndElement();
		}
		if (pawn.health != null)
		{
			string text4 = Scribe.saver.DebugOutputFor(pawn.health);
			writer.WriteRaw(text4 ?? "");
		}
		if (pawn.skills != null)
		{
			string text5 = Scribe.saver.DebugOutputFor(pawn.skills);
			writer.WriteRaw(text5 ?? "");
		}
		writer.WriteEndElement();
		writer.Flush();
	}

	private void EncodeCorpse(Corpse corpse, XmlWriter writer)
	{
		writer.WriteStartElement("item");
		writer.WriteAttributeString("def", "corpse");
		writer.WriteAttributeString("timeOfDeath", corpse.timeOfDeath.ToString());
		EncodePawn(corpse.InnerPawn, writer);
		writer.WriteEndElement();
	}

	private void EncodeThing(Thing thing, XmlWriter writer)
	{
		if (thing is Corpse)
		{
			EncodeCorpse((Corpse)thing, writer);
		}
		else if (thing is Pawn)
		{
			EncodePawn((Pawn)thing, writer);
		}
		else
		{
			if ((thing.def.category != ThingCategory.Building && thing.def.category != ThingCategory.Item) || (thing.def.building != null && thing.def.building.isNaturalRock))
			{
				return;
			}
			Type type = Type.GetType("SaM.CompText, Signs_and_Memorials");
			if ((float)thing.HitPoints > (float)thing.MaxHitPoints * 0.9f)
			{
				maxHPItemsCount++;
			}
			itemsCount++;
			writer.WriteStartElement("item");
			writer.WriteAttributeString("def", thing.def.defName);
			if (thing.Stuff != null)
			{
				writer.WriteAttributeString("stuffDef", thing.Stuff.defName);
			}
			if (thing.stackCount > 1)
			{
				writer.WriteAttributeString("stackCount", thing.stackCount.ToString());
			}
			_ = thing.Rotation;
			if (true)
			{
				writer.WriteAttributeString("rot", thing.Rotation.AsByte.ToString());
			}
			CompArt compArt = thing.TryGetComp<CompArt>();
			if (compArt != null && compArt.Active)
			{
				writer.WriteAttributeString("artAuthor", compArt.AuthorName.RawText);
				writer.WriteAttributeString("artTitle", compArt.Title);
				writer.WriteAttributeString("artDescription", compArt.GenerateImageDescription().RawText);
			}
			if (thing is ThingWithComps thingWithComps && type != null)
			{
				object obj = null;
				for (int i = 0; i < thingWithComps.AllComps.Count; i++)
				{
					ThingComp thingComp = thingWithComps.AllComps[i];
					if (thingComp.GetType() == type)
					{
						obj = thingComp;
					}
				}
				if (obj != null)
				{
					string text = (string)obj?.GetType()?.GetField("text")?.GetValue(obj);
					if (text != null && text != null)
					{
						writer.WriteAttributeString("text", text);
					}
				}
			}
			if (thing.def.passability == Traversability.Impassable || (double)thing.def.fillPercent > 0.99)
			{
				writer.WriteAttributeString("actsAsWall", "1");
			}
			if (thing.def.IsDoor)
			{
				writer.WriteAttributeString("isDoor", "1");
			}
			if (thing is IThingHolder)
			{
				ThingOwner directlyHeldThings = ((IThingHolder)thing).GetDirectlyHeldThings();
				foreach (Thing item in (IEnumerable<Thing>)directlyHeldThings)
				{
					EncodeThing(item, writer);
				}
			}
			writer.WriteEndElement();
		}
	}

	public string Generate()
	{
		StringBuilder stringBuilder = new StringBuilder();
		XmlWriterSettings xmlWriterSettings = new XmlWriterSettings();
		xmlWriterSettings.OmitXmlDeclaration = true;
		xmlWriterSettings.ConformanceLevel = ConformanceLevel.Fragment;
		XmlWriter xmlWriter = XmlWriter.Create(stringBuilder, xmlWriterSettings);
		int num = 10000;
		int num2 = 0;
		int num3 = 10000;
		int num4 = 0;
		foreach (IntVec3 activeCell in map.areaManager.Home.ActiveCells)
		{
			if (activeCell.x < num)
			{
				num = activeCell.x;
			}
			if (activeCell.x > num2)
			{
				num2 = activeCell.x;
			}
			if (activeCell.z < num3)
			{
				num3 = activeCell.z;
			}
			if (activeCell.z > num4)
			{
				num4 = activeCell.z;
			}
		}
		Log.Message($"Home area bounds: ({num}, {num3}) - ({num2}, {num4})");
		int num5 = num;
		int num6 = num3;
		int width = num2 - num;
		int height = num4 - num3;
		Log.Message($"Origin: {num5}, {num6}");
		CellRect cellRect = new CellRect(num5, num6, width, height);
		Log.Message($"Start capturing in area of: ({cellRect.minX},{cellRect.minZ})-({cellRect.maxX},{cellRect.maxZ})");
		xmlWriter.WriteStartElement("snapshot");
		xmlWriter.WriteAttributeString("version", typeof(RealRuins).Assembly.GetName().Version.ToString());
		xmlWriter.WriteAttributeString("x", num5.ToString());
		xmlWriter.WriteAttributeString("z", num6.ToString());
		xmlWriter.WriteAttributeString("width", width.ToString());
		xmlWriter.WriteAttributeString("height", height.ToString());
		xmlWriter.WriteAttributeString("biomeDef", map.Biome.defName);
		xmlWriter.WriteAttributeString("mapSize", map.Size.x.ToString());
		xmlWriter.WriteAttributeString("inGameYear", (Find.TickManager.StartingYear + Find.TickManager.TicksGame / 3600000).ToString());
		xmlWriter.WriteStartElement("world");
		xmlWriter.WriteAttributeString("seed", Find.World.info.seedString);
		xmlWriter.WriteAttributeString("tile", map.Parent.Tile.ToString());
		xmlWriter.WriteAttributeString("gameId", Math.Abs(Find.World.info.persistentRandomValue).ToString());
		xmlWriter.WriteAttributeString("percentage", Find.World.info.planetCoverage.ToString());
		xmlWriter.WriteEndElement();
		for (int i = cellRect.minZ; i < cellRect.maxZ; i++)
		{
			for (int j = cellRect.minX; j < cellRect.maxX; j++)
			{
				IntVec3 intVec = new IntVec3(j, 0, i);
				List<Thing> list = map.thingGrid.ThingsListAt(intVec);
				TerrainDef terrainDef = map.terrainGrid.TerrainAt(intVec);
				RoofDef roofDef = map.roofGrid.RoofAt(intVec);
				if (roofDef == null && !terrainDef.BuildableByPlayer && list.Count == 0)
				{
					continue;
				}
				xmlWriter.WriteStartElement("cell");
				xmlWriter.WriteAttributeString("x", (j - num).ToString());
				xmlWriter.WriteAttributeString("z", (i - num3).ToString());
				if (terrainDef.BuildableByPlayer)
				{
					xmlWriter.WriteStartElement("terrain");
					xmlWriter.WriteAttributeString("def", terrainDef.defName);
					xmlWriter.WriteEndElement();
					terrainCount++;
				}
				if (roofDef != null)
				{
					xmlWriter.WriteElementString("roof", "");
				}
				if (list.Count > 0)
				{
					foreach (Thing item in list)
					{
						if (!(item is Pawn) && item.Position.Equals(intVec))
						{
							EncodeThing(item, xmlWriter);
						}
					}
				}
				xmlWriter.WriteEndElement();
			}
		}
		xmlWriter.WriteEndElement();
		float num7 = (float)(itemsCount + terrainCount) / (float)((num2 - num) * (num4 - num3));
		if ((double)num7 < 0.01)
		{
			Debug.Log("BlueprintGen", "Too low ruins density: {0}. Ignoring.", num7);
			return null;
		}
		if ((float)maxHPItemsCount < (float)itemsCount * 0.25f)
		{
			Debug.Log("BlueprintGen", "Too low ruins average HP. Ignoring.");
			return null;
		}
		string tempFileName = Path.GetTempFileName();
		File.WriteAllText(tempFileName, stringBuilder.ToString());
		Debug.Log("BlueprintGen", "Capture finished successfully and passed all checks. ({0})", tempFileName);
		return tempFileName;
	}
}
