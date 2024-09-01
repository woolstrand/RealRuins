using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using RimWorld;
using RimWorld.BaseGen;
using Verse;

namespace RealRuins;

internal class BlueprintTransferUtility
{
	private const long ticksInYear = 3600000L;

	private Blueprint blueprint;

	private ResolveParams rp;

	private ScatterOptions options;

	private Map map;

	private int mapOriginX;

	private int mapOriginZ;

	private bool ClearCell(IntVec3 location, Map map, bool shouldForceClear = true)
	{
		try
		{
			List<Thing> list = map.thingGrid.ThingsListAt(location);
			foreach (Thing item in list)
			{
				if (item.def.thingClass.ToString().Contains("DubsBadHygiene"))
				{
					return false;
				}
				if (!item.def.destroyable)
				{
					return false;
				}
				if (item.def.mineable && !shouldForceClear)
				{
					return false;
				}
			}
			for (int num = list.Count - 1; num >= 0; num--)
			{
				list[num].DeSpawn();
			}
			return true;
		}
		catch
		{
			return false;
		}
	}

	private Pawn MakePawnWithRawXml(string xml)
	{
		try
		{
			XmlDocument xmlDocument = new XmlDocument();
			xmlDocument.LoadXml(xml);
			XmlNode firstChild = xmlDocument.FirstChild;
			string innerText = firstChild.SelectSingleNode("kind").InnerText;
			PawnKindDef pawnKindDef = PawnKindDef.Named(innerText);
			if (pawnKindDef == null)
			{
				pawnKindDef = PawnKindDefOf.AncientSoldier;
			}
			Pawn pawn = PawnGenerator.GeneratePawn(pawnKindDef, rp.faction);
			Name name = null;
			XmlNode xmlNode = firstChild.SelectSingleNode("name");
			XmlNode namedItem = xmlNode.Attributes.GetNamedItem("first");
			XmlNode namedItem2 = xmlNode.Attributes.GetNamedItem("last");
			XmlNode namedItem3 = xmlNode.Attributes.GetNamedItem("nick");
			name = ((namedItem == null || namedItem2 == null) ? ((Name)new NameSingle(namedItem?.Value ?? "Unknown")) : ((Name)new NameTriple(namedItem.Value, namedItem3?.Value ?? "", namedItem2.Value)));
			pawn.Name = name;
			string text = firstChild.SelectSingleNode("gender")?.InnerText;
			if (text == "Male")
			{
				pawn.gender = Gender.Male;
			}
			else if (text == "Female")
			{
				pawn.gender = Gender.Female;
			}
			string text2 = firstChild.SelectSingleNode("biologicalAge")?.InnerText;
			string text3 = firstChild.SelectSingleNode("chronologicalAge")?.InnerText;
			if (text2 != null && text3 != null)
			{
				long result = 0L;
				long.TryParse(text2, out result);
				pawn.ageTracker.AgeBiologicalTicks = result;
				long.TryParse(text3, out result);
				pawn.ageTracker.AgeChronologicalTicks = result + 3600000 * -blueprint.dateShift;
			}
			XmlNode xmlNode2 = firstChild.SelectSingleNode("saveable[@Class='Pawn_StoryTracker']");
			if (xmlNode2 != null)
			{
			}
			XmlNode xmlNode3 = firstChild.SelectSingleNode("saveable[@Class='Pawn_SkillTracker']");
			if (xmlNode3 != null)
			{
				XmlNodeList xmlNodeList = xmlNode2.SelectNodes("skills/li");
				foreach (XmlNode item in xmlNodeList)
				{
					string defName = item.SelectSingleNode("def")?.InnerText;
					int result2 = 0;
					int.TryParse(item.SelectSingleNode("level")?.InnerText, out result2);
					float result3 = 0f;
					float.TryParse(item.SelectSingleNode("xpSinceLastLevel")?.InnerText, out result3);
					SkillDef namedSilentFail = DefDatabase<SkillDef>.GetNamedSilentFail(defName);
					if (namedSilentFail == null)
					{
						continue;
					}
					SkillRecord skillRecord = pawn.skills.GetSkill(namedSilentFail);
					if (skillRecord == null)
					{
						skillRecord = new SkillRecord(pawn, namedSilentFail);
					}
					skillRecord.Level = result2;
					skillRecord.xpSinceLastLevel = result3;
					try
					{
						string text4 = item.SelectSingleNode("passion")?.InnerText;
						if (text4 != null)
						{
							skillRecord.passion = (Passion)Enum.Parse(typeof(Passion), text4);
						}
					}
					catch (Exception)
					{
					}
				}
			}
			XmlNode xmlNode5 = firstChild.SelectSingleNode("saveable[@Class='Pawn_HealthTracker']");
			if (xmlNode5 != null)
			{
				if (xmlNode5.SelectSingleNode("healthState")?.InnerText == "Dead")
				{
					pawn.health.SetDead();
				}
				XmlNodeList xmlNodeList2 = xmlNode5.SelectNodes("hediffSet/hediffs/li");
				if (xmlNodeList2 != null)
				{
					Scribe.mode = LoadSaveMode.LoadingVars;
					pawn.health?.hediffSet?.hediffs?.RemoveAll((Hediff _) => true);
					foreach (XmlNode item2 in xmlNodeList2)
					{
						XmlNode xmlNode7 = item2.SelectSingleNode("source");
						string text5 = xmlNode7?.InnerText;
						if (text5 != null)
						{
							ThingDef namedSilentFail2 = DefDatabase<ThingDef>.GetNamedSilentFail(text5);
							if (namedSilentFail2 == null)
							{
								item2.RemoveChild(xmlNode7);
							}
						}
						try
						{
							Hediff hediff = ScribeExtractor.SaveableFromNode<Hediff>(item2, null);
							if (hediff != null && hediff.sourceDef != null && hediff.Part != null)
							{
								pawn.health.AddHediff(hediff);
							}
						}
						catch (Exception)
						{
						}
					}
					Scribe.mode = LoadSaveMode.Inactive;
				}
			}
			XmlNode xmlNode8 = firstChild.SelectSingleNode("apparel");
			if (xmlNode8 != null)
			{
				XmlNodeList xmlNodeList3 = xmlNode8.SelectNodes("item");
				foreach (XmlNode item3 in xmlNodeList3)
				{
					string defName2 = item3.Attributes?.GetNamedItem("def")?.Value;
					string text6 = item3.Attributes?.GetNamedItem("stuffDef")?.Value;
					ThingDef stuff = null;
					ThingDef namedSilentFail3 = DefDatabase<ThingDef>.GetNamedSilentFail(defName2);
					if (text6 != null)
					{
						stuff = DefDatabase<ThingDef>.GetNamedSilentFail(text6);
					}
					if (namedSilentFail3 != null)
					{
						Apparel apparel = (Apparel)ThingMaker.MakeThing(namedSilentFail3, stuff);
						apparel.HitPoints = Rand.Range(1, (int)((double)apparel.MaxHitPoints * 0.6));
						if (apparel != null)
						{
							pawn.apparel.Wear(apparel, dropReplacedApparel: false);
						}
					}
				}
			}
			return pawn;
		}
		catch (Exception)
		{
			return PawnGenerator.GeneratePawn(PawnKindDefOf.AncientSoldier, rp.faction);
		}
	}

	private Thing MakeThingFromItemTile(ItemTile itemTile, bool enableLogging = false)
	{
		try
		{
			if (enableLogging)
			{
			}
			if (itemTile.defName.ToLower() == "pawn")
			{
				return null;
			}
			if (itemTile.defName.ToLower() == "corpse")
			{
				if (itemTile.innerItems != null)
				{
					Pawn pawn = (Pawn)MakeThingFromItemTile(itemTile.innerItems.First());
					Corpse corpse = null;
					if (pawn.Corpse != null)
					{
						corpse = pawn.Corpse;
					}
					else
					{
						corpse = (Corpse)ThingMaker.MakeThing(pawn.RaceProps.corpseDef);
						corpse.InnerPawn = pawn;
					}
					corpse.timeOfDeath = (int)(itemTile.corpseDeathTime + 3600000L * (long)blueprint.dateShift);
					CompRottable compRottable = corpse.TryGetComp<CompRottable>();
					if (compRottable != null)
					{
						compRottable.RotProgress = 3600000L * (long)(-blueprint.dateShift);
					}
					return corpse;
				}
				return null;
			}
			if (itemTile.defName.ToLower().Contains("corpse") || itemTile.defName.ToLower().Contains("minified"))
			{
				List<ItemTile> innerItems = itemTile.innerItems;
				if (innerItems == null || !innerItems.Any())
				{
					return null;
				}
			}
			if (itemTile.defName == "Hive")
			{
				return null;
			}
			ThingDef named = DefDatabase<ThingDef>.GetNamed(itemTile.defName, errorOnFail: false);
			if (named.category == ThingCategory.Ethereal)
			{
				return null;
			}
			ThingDef thingDef = null;
			if (itemTile.stuffDef != null && named.MadeFromStuff)
			{
				thingDef = DefDatabase<ThingDef>.GetNamed(itemTile.stuffDef, errorOnFail: false);
			}
			if (thingDef == null)
			{
				thingDef = ((!itemTile.isWall || !named.MadeFromStuff) ? GenStuff.DefaultStuffFor(named) : ThingDefOf.BlocksGranite);
			}
			Thing thing = ThingMaker.MakeThing(named, thingDef);
			if (thing != null)
			{
				if (itemTile.innerItems != null && thing is IThingHolder)
				{
					foreach (ItemTile innerItem in itemTile.innerItems)
					{
						Thing thing2 = MakeThingFromItemTile(innerItem, enableLogging: true);
						if (thing2 != null)
						{
							((IThingHolder)thing).GetDirectlyHeldThings().TryAdd(thing2);
						}
					}
					if (thing.GetInnerIfMinified() == null)
					{
						return null;
					}
				}
				if (named.CanHaveFaction)
				{
					thing.SetFaction(rp.faction);
				}
				CompQuality compQuality = thing.TryGetComp<CompQuality>();
				if (compQuality != null)
				{
					byte b = (byte)Math.Abs(Math.Round(Rand.Gaussian(0f, 2f)));
					if (itemTile.art != null)
					{
						if (b > 6)
						{
							b = 6;
						}
						compQuality.SetQuality((QualityCategory)b, ArtGenerationContext.Outsider);
						thing.TryGetComp<CompArt>()?.InitializeArt(itemTile.art.author, itemTile.art.title, itemTile.art.TextWithDatesShiftedBy(blueprint.dateShift));
					}
					else
					{
						if (b > 6)
						{
							b = 6;
						}
						compQuality.SetQuality((QualityCategory)b, ArtGenerationContext.Outsider);
					}
				}
				if (itemTile.stackCount > 1)
				{
					thing.stackCount = itemTile.stackCount;
					CompRottable compRottable2 = thing.TryGetComp<CompRottable>();
					if (compRottable2 != null)
					{
						if (options.canHaveFood)
						{
							compRottable2.RotProgress = (Rand.Value * 0.5f + options.deteriorationMultiplier) * (float)compRottable2.PropsRot.TicksToRotStart;
						}
						else
						{
							compRottable2.RotProgress = compRottable2.PropsRot.TicksToRotStart + 1;
						}
					}
				}
				if (itemTile.attachedText != null && thing is ThingWithComps)
				{
					ThingWithComps thingWithComps = thing as ThingWithComps;
					Type type = Type.GetType("SaM.CompText, Signs_and_Memorials");
					if (type != null)
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
							obj?.GetType()?.GetField("text").SetValue(obj, itemTile.attachedText);
						}
					}
				}
				if (thing is UnfinishedThing)
				{
					((UnfinishedThing)thing).workLeft = 10000f;
					((UnfinishedThing)thing).Creator = Find.WorldPawns.AllPawnsAliveOrDead.RandomElement();
				}
				int maxExclusive = 0;
				if (!options.forceFullHitPoints)
				{
					maxExclusive = Math.Min(thing.MaxHitPoints - 1, (int)Math.Abs(Rand.Gaussian(0f, 200f)));
				}
				thing.HitPoints = thing.MaxHitPoints - Rand.Range(0, maxExclusive);
				if (thing.def.EverHaulable)
				{
					thing.SetForbidden(value: true, warnOnFail: false);
				}
				if (thing is Building_Storage)
				{
					((Building_Storage)thing).settings.Priority = StoragePriority.Unstored;
				}
			}
			return thing;
		}
		catch (Exception ex)
		{
			Debug.Log("BlueprintTransfer", "Failed to spawn item {0} because of {1}", itemTile.defName, ex);
			return null;
		}
	}

	public BlueprintTransferUtility(Blueprint blueprint, Map map, ResolveParams rp, ScatterOptions options)
	{
		this.blueprint = blueprint;
		this.map = map;
		this.rp = rp;
		this.options = options;
		Debug.Log("Transferring blueprint of faction {0}", rp.faction?.Name ?? "none");
		if (blueprint == null)
		{
			Debug.Error("BlueprintTransfer", "Attempting to configure transfer utility with empty blueprint!");
			return;
		}
		if (map == null)
		{
			Debug.Error("BlueprintTransfer", "Attempting to configure transfer utility with empty map!");
			return;
		}
		if (options == null)
		{
			Debug.Error("BlueprintTransfer", "Attempting to configure transfer utility with empty options!");
			return;
		}
		mapOriginX = rp.rect.minX + rp.rect.Width / 2 - blueprint.width / 2;
		mapOriginZ = rp.rect.minZ + rp.rect.Height / 2 - blueprint.height / 2;
		if (mapOriginX < 0)
		{
			mapOriginX = 0;
		}
		if (mapOriginZ < 0)
		{
			mapOriginZ = 0;
		}
		if (mapOriginX + blueprint.width >= map.Size.x)
		{
			mapOriginX = map.Size.x - blueprint.width - 1;
		}
		if (mapOriginZ + blueprint.height >= map.Size.z)
		{
			mapOriginZ = map.Size.z - blueprint.height - 1;
		}
		if (options.overridePosition != new IntVec3(0, 0, 0))
		{
			if (!options.centerIfExceedsBounds || (options.overridePosition.x + blueprint.width < map.Size.x && options.overridePosition.z + blueprint.height < map.Size.z))
			{
				mapOriginX = options.overridePosition.x;
				mapOriginZ = options.overridePosition.z;
				return;
			}
			Debug.Warning("BlueprintTransfer", "Tried to override position, but map exceeded bounds and position was reverted due to corresponding options flag.");
			Debug.Warning("BlueprintTransfer", "New position: {0}, {1}", mapOriginX, mapOriginZ);
		}
	}

	public void RemoveIncompatibleItems()
	{
		if (blueprint.roofMap == null)
		{
			Debug.Log("BlueprintTransfer", "Trying to process blueprint with empty roof map");
		}
		if (map == null)
		{
			Debug.Log("BlueprintTransfer", "Trying to process blueprint but map is still null");
		}
		try
		{
			int num = 0;
			int num2 = 0;
			for (int i = 0; i < blueprint.width; i++)
			{
				for (int j = 0; j < blueprint.height; j++)
				{
					Debug.Extra("BlueprintTransfer", "Starting cell {0} {1}...", i, j);
					if (blueprint.itemsMap[i, j] == null)
					{
						blueprint.itemsMap[i, j] = new List<ItemTile>();
					}
					IntVec3 c = new IntVec3(i + mapOriginX, 0, j + mapOriginZ);
					if (!c.InBounds(map))
					{
						continue;
					}
					List<ItemTile> list = blueprint.itemsMap[i, j];
					TerrainTile terrainTile = blueprint.terrainMap[i, j];
					TerrainDef terrainDef = null;
					if (terrainTile != null)
					{
						terrainDef = DefDatabase<TerrainDef>.GetNamed(terrainTile.defName, errorOnFail: false);
						if (terrainDef == null)
						{
							blueprint.terrainMap[i, j] = null;
							terrainTile = null;
						}
					}
					TerrainDef terrainDef2 = map.terrainGrid?.TerrainAt(c);
					if (terrainDef2 != null && terrainDef != null && terrainDef2.affordances != null && terrainDef.terrainAffordanceNeeded != null && !terrainDef2.affordances.Contains(terrainDef.terrainAffordanceNeeded))
					{
						terrainDef = null;
						blueprint.terrainMap[i, j] = null;
						blueprint.roofMap[i, j] = false;
					}
					Debug.Extra("BlueprintTransfer", "Preprocessed cell {0} {1}, moving to items...", i, j);
					List<ItemTile> list2 = new List<ItemTile>();
					foreach (ItemTile item in list)
					{
						num++;
						ThingDef named = DefDatabase<ThingDef>.GetNamed(item.defName, errorOnFail: false);
						if (named == null)
						{
							list2.Add(item);
							continue;
						}
						Debug.Extra("BlueprintTransfer", "Making thorough check for thing {0}", item.defName);
						if (options.overwritesEverything || named.terrainAffordanceNeeded == null || (named.EverTransmitsPower && options.shouldKeepDefencesAndPower))
						{
							continue;
						}
						if (terrainDef != null && terrainDef.terrainAffordanceNeeded != null && terrainDef2.affordances.Contains(terrainDef.terrainAffordanceNeeded))
						{
							if (!terrainDef.affordances.Contains(named.terrainAffordanceNeeded))
							{
								list2.Add(item);
								blueprint.roofMap[i, j] = false;
							}
							continue;
						}
						List<TerrainAffordanceDef> affordances = terrainDef2.affordances;
						if (affordances != null && !affordances.Contains(named.terrainAffordanceNeeded))
						{
							list2.Add(item);
							blueprint.roofMap[i, j] = false;
						}
					}
					foreach (ItemTile item2 in list2)
					{
						if (item2.isWall || item2.isDoor)
						{
							blueprint.RemoveWall(item2.location.x, item2.location.z);
						}
						list.Remove(item2);
						num2++;
					}
				}
			}
			Debug.Extra("BlueprintTransfer", "Finished check, recalculating stats");
			blueprint.UpdateBlueprintStats(includeCost: true);
			Debug.Log("BlueprintTransfer", "Blueprint transfer utility did remove {0}/{1} incompatible items. New cost: {2}", num2, num, blueprint.totalCost);
		}
		catch (Exception ex)
		{
			Debug.Error("BlueprintTransfer", "Exception while trying to cleanup blueprint details. This should not normally happen, so please report this case: {0}", ex.ToString());
		}
	}

	public void Transfer(CoverageMap coverageMap)
	{
		float num = 0f;
		int num2 = 0;
		int num3 = 0;
		int num4 = 0;
		int num5 = 0;
		rp.rect = new CellRect(mapOriginX, mapOriginZ, blueprint.width, blueprint.height);
		Debug.Extra("BlueprintTransfer", "Clearing map...");
		for (int i = 0; i < blueprint.height; i++)
		{
			for (int j = 0; j < blueprint.width; j++)
			{
				try
				{
					IntVec3 intVec = new IntVec3(j + mapOriginX, 0, i + mapOriginZ);
					if (intVec.InBounds(map) && !intVec.InNoBuildEdgeArea(map) && (options.overwritesEverything || Rand.Chance(0.6f)) && (blueprint.terrainMap[j, i] != null || blueprint.itemsMap[j, i].Count > 0 || blueprint.wallMap[j, i] > 1))
					{
						ClearCell(intVec, map);
					}
				}
				catch (Exception ex)
				{
					Debug.Warning("BlueprintTransfer", "Failed to clean cell at {0}, {1} because of {2}", j, i, ex);
				}
			}
		}
		Debug.Extra("BlueprintTransfer", "Transferring map objects");
		for (int k = 0; k < blueprint.height; k++)
		{
			for (int l = 0; l < blueprint.width; l++)
			{
				IntVec3 intVec2 = new IntVec3(l + mapOriginX, 0, k + mapOriginZ);
				if (coverageMap != null)
				{
					if (coverageMap.isMarked(intVec2.x, intVec2.z))
					{
						continue;
					}
					if (blueprint.wallMap[l, k] > 1 || blueprint.wallMap[l, k] == -1)
					{
						coverageMap.Mark(intVec2.x, intVec2.z);
					}
				}
				if (!intVec2.InBounds(map) || intVec2.InNoBuildEdgeArea(map))
				{
					continue;
				}
				try
				{
					if (blueprint.terrainMap[l, k] != null)
					{
						num4++;
						TerrainDef newTerr = TerrainDef.Named(blueprint.terrainMap[l, k].defName);
						if (!map.terrainGrid.TerrainAt(intVec2).IsWater)
						{
							map.terrainGrid.SetTerrain(intVec2, newTerr);
							num += blueprint.terrainMap[l, k].cost;
							num2++;
						}
					}
					if (blueprint.roofMap[l, k] && options.overwritesEverything && blueprint.wallMap[l, k] != 1)
					{
						map.roofGrid.SetRoof(intVec2, RoofDefOf.RoofConstructed);
					}
					Debug.Extra("BlueprintTransfer", "Transferred terrain and roof at cell ({0}, {1})", l, k);
				}
				catch (Exception ex2)
				{
					Debug.Warning("BlueprintTransfer", "Failed to transfer terrain {0} at {1}, {2} because of {3}", blueprint.terrainMap[l, k].defName, l, k, ex2);
				}
				if (blueprint.itemsMap[l, k] == null || blueprint.itemsMap[l, k].Count <= 0)
				{
					continue;
				}
				num5 += blueprint.itemsMap[l, k].Count;
				foreach (ItemTile item in blueprint.itemsMap[l, k])
				{
					Debug.Extra("BlueprintTransfer", "Creating thing {2} at cell ({0}, {1})", l, k, item.defName);
					Thing thing = MakeThingFromItemTile(item);
					if (thing == null)
					{
						continue;
					}
					try
					{
						Rot4 rot = new Rot4(item.rot);
						foreach (IntVec3 item2 in GenAdj.CellsOccupiedBy(intVec2, rot, thing.def.Size))
						{
							foreach (Thing item3 in map.thingGrid.ThingsAt(item2).ToList())
							{
								if (GenSpawn.SpawningWipes(thing.def, item3.def))
								{
									if (thing.def.thingClass.ToString().Contains("DubsBadHygiene"))
									{
										throw new Exception("Can't spawn item because it will destroy Dubs Bad Hygiene Item and it will lead to app freeze.");
									}
									item3.Destroy();
								}
							}
						}
						GenSpawn.Spawn(thing, intVec2, map, rot);
						Debug.Extra("BlueprintTransfer", "Spawned");
						try
						{
							switch (thing.def.tickerType)
							{
							case TickerType.Never:
								break;
							case TickerType.Normal:
								thing.Tick();
								break;
							case TickerType.Long:
								thing.TickLong();
								break;
							case TickerType.Rare:
								thing.TickRare();
								break;
							}
						}
						catch (Exception ex3)
						{
							Debug.Log("BlueprintTransfer", "Exception while tried to perform tick for {0} of cost {1}, retrhrowing...", thing.def.defName, item.cost);
							thing.Destroy();
							throw ex3;
						}
						CompBreakdownable compBreakdownable = thing.TryGetComp<CompBreakdownable>();
						if (compBreakdownable != null && options.enableDeterioration && Rand.Chance(0.8f))
						{
							compBreakdownable.DoBreakdown();
						}
						if (thing.def.EverHaulable)
						{
							TerrainDef terrainDef = map.terrainGrid.TerrainAt(intVec2);
							if (terrainDef != null && terrainDef.IsWater)
							{
								thing.HitPoints = (thing.HitPoints - 10) / Rand.Range(5, 20) + Rand.Range(1, 10);
							}
						}
						Debug.Extra("BlueprintTransfer", "Item completed");
						num3++;
						num += item.cost;
					}
					catch (Exception ex4)
					{
						Debug.Warning("BlueprintTransfer", "Failed to spawn item {0} of cost {1} because of exception {2}", thing, item.cost, ex4.Message);
					}
				}
			}
		}
		Debug.Log("BlueprintTransfer", "Finished transferring");
		if (options.shouldKeepDefencesAndPower)
		{
			RestoreDefencesAndPower();
		}
		options.uncoveredCost = num;
		Debug.Log("BlueprintTransfer", "Transferred blueprint of size {0}x{1}, age {2}, total cost of approximately {3}. Items: {4}/{5}, terrains: {6}/{7}", blueprint.width, blueprint.height, -blueprint.dateShift, num, num3, num5, num2, num4);
	}

	public void AddFilthAndRubble()
	{
		ThingDef[] array = new ThingDef[3]
		{
			ThingDefOf.Filth_Dirt,
			ThingDefOf.Filth_Trash,
			ThingDefOf.Filth_Ash
		};
		float[,] array2 = new float[blueprint.width, blueprint.height];
		for (int i = 0; i < blueprint.height; i++)
		{
			for (int j = 0; j < blueprint.width; j++)
			{
				if (blueprint.itemsMap[j, i].Count() > 0 || blueprint.terrainMap[j, i] != null)
				{
					array2[j, i] = 1f;
				}
			}
		}
		array2.Blur(2);
		for (int k = 0; k < blueprint.height; k++)
		{
			for (int l = 0; l < blueprint.width; l++)
			{
				try
				{
					IntVec3 intVec = new IntVec3(l + mapOriginX, 0, k + mapOriginZ);
					if (!intVec.InBounds(map) || array2[l, k] <= 0f || Rand.Chance(0.2f))
					{
						continue;
					}
					FilthMaker.TryMakeFilth(intVec, map, array[0], Rand.Range(0, 3));
					while ((double)Rand.Value > 0.7)
					{
						FilthMaker.TryMakeFilth(intVec, map, array[Rand.Range(0, 2)], Rand.Range(1, 5));
					}
					if (options.shouldKeepDefencesAndPower && Rand.Chance(0.05f))
					{
						FilthMaker.TryMakeFilth(intVec, map, ThingDefOf.Filth_Blood, Rand.Range(1, 5));
					}
					if (!Rand.Chance(0.01f))
					{
						continue;
					}
					List<Thing> list = map.thingGrid.ThingsListAt(intVec);
					bool flag = true;
					foreach (Thing item in list)
					{
						if ((double)item.def.fillPercent > 0.5)
						{
							flag = false;
						}
					}
					if (flag)
					{
						Thing newThing = ThingMaker.MakeThing(ThingDefOf.ChunkSlagSteel);
						GenSpawn.Spawn(newThing, intVec, map, new Rot4(Rand.Range(0, 4)));
					}
				}
				catch (Exception)
				{
				}
			}
		}
	}

	private void RestoreDefencesAndPower()
	{
		foreach (Thing item in (IEnumerable<Thing>)map.spawnedThings)
		{
			if (item.TryGetComp<CompPowerPlant>() != null || item.TryGetComp<CompPowerBattery>() != null || (item.def.building != null && item.def.building.IsTurret))
			{
				item.TryGetComp<CompBreakdownable>()?.Notify_Repaired();
			}
		}
	}
}
