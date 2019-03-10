using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;

using RimWorld;
using Verse;
using RimWorld.BaseGen;
using UnityEngine;
using Verse.AI;
using Verse.AI.Group;

/**
 * This class does two things: removes things which can't be placed and actually transfers a blueprint to a real map according to options provided
 * Those two things are separated because they happen at different steps with some steps in between.
 * Those two things are combined here, because both depends on a real location map and work with it.
 * Blueprint is transferred as is, so you should do all preprocessing beforehand
 * */

namespace RealRuins {
    class BlueprintTransferUtility {

        private const long ticksInYear = 3600000;


        Blueprint blueprint;
        ResolveParams rp;
        ScatterOptions options;
        Map map;

        int mapOriginX;
        int mapOriginZ;


        // Clear the cell from other destroyable objects
        private bool ClearCell(IntVec3 location, Map map, bool shouldForceClear = true) {
            List<Thing> items = map.thingGrid.ThingsListAt(location);
            foreach (Thing item in items) {
                if (!item.def.destroyable) {
                    return false;
                }
                if (item.def.mineable && !shouldForceClear) {//mountain is destroyable only when forcing
                    return false;
                }
            }

            for (int index = items.Count - 1; index >= 0; index--) {
                items[index].Destroy(DestroyMode.Vanish);
            }
            return true;
        }




        private Pawn MakePawnWithRawXml(string xml) {
            try {

                XmlDocument document = new XmlDocument();
                document.LoadXml(xml);
                //Debug.Message("Pawn xml: {0}", xml);
                XmlNode root = document.FirstChild;

                string pawnKind = root.SelectSingleNode("kind").InnerText;
                PawnKindDef kindDef = PawnKindDef.Named(pawnKind);
                if (kindDef == null) {
                    kindDef = PawnKindDefOf.AncientSoldier;
                }

                Pawn p = PawnGenerator.GeneratePawn(kindDef, rp.faction);

                // ==== NAME AND AGE ====
                Name name = null;
                var nameNode = root.SelectSingleNode("name");
                var attrFirst = nameNode.Attributes.GetNamedItem("first");
                var attrLast = nameNode.Attributes.GetNamedItem("last");
                var attrNick = nameNode.Attributes.GetNamedItem("nick");
                if (attrFirst != null && attrLast != null) {
                    name = new NameTriple(attrFirst.Value, attrNick?.Value ?? "", attrLast.Value);
                } else {
                    name = new NameSingle(attrFirst?.Value ?? "Unknown");
                }
                p.Name = name;
                //Debug.Message("got name");

                string gender = root.SelectSingleNode("gender")?.InnerText;
                if (gender == "Male") {
                    p.gender = Gender.Male;
                } else if (gender == "Female") {
                    p.gender = Gender.Female;
                }

                string bioAgeString = root.SelectSingleNode("biologicalAge")?.InnerText;
                string chronoAgeString = root.SelectSingleNode("chronologicalAge")?.InnerText;
                if (bioAgeString != null && chronoAgeString != null) {
                    long result = 0;
                    Int64.TryParse(bioAgeString, out result);
                    p.ageTracker.AgeBiologicalTicks = result;
                    Int64.TryParse(chronoAgeString, out result);
                    p.ageTracker.AgeChronologicalTicks = result + 3600000 * (-blueprint.dateShift); //+dateShift for dates, -dateShift for ages
                }
                //Debug.Message("got age");


                // ==== STORY AND APPEARANCE ====
                var storyNode = root.SelectSingleNode("saveable[@Class='Pawn_StoryTracker']");
                if (storyNode != null) {
                    Backstory bs = null;
                    string childhoodDef = storyNode.SelectSingleNode("childhood")?.InnerText;
                    if (BackstoryDatabase.TryGetWithIdentifier(childhoodDef, out bs)) {
                        p.story.childhood = bs;
                    }
                    string adulthoodDef = storyNode.SelectSingleNode("adulthood")?.InnerText;
                    if (BackstoryDatabase.TryGetWithIdentifier(adulthoodDef, out bs)) {
                        p.story.adulthood = bs;
                    }

                    string bodyTypeDefName = storyNode.SelectSingleNode("bodyType")?.InnerText;
                    if (bodyTypeDefName != null) {
                        BodyTypeDef def = DefDatabase<BodyTypeDef>.GetNamedSilentFail(bodyTypeDefName);
                        if (def != null) { p.story.bodyType = def; }

                        try {
                            string crownTypeName = storyNode.SelectSingleNode("crownType")?.InnerText;
                            p.story.crownType = (CrownType)Enum.Parse(typeof(CrownType), crownTypeName);
                        } catch (Exception) { }

                        string hairDefName = storyNode.SelectSingleNode("hairDef")?.InnerText;
                        HairDef hairDef = DefDatabase<HairDef>.GetNamedSilentFail(hairDefName);
                        if (hairDef != null) { p.story.hairDef = hairDef; }

                        float melanin = 0;
                        if (float.TryParse(storyNode.SelectSingleNode("melanin")?.InnerText, out melanin)) {
                            p.story.melanin = melanin;
                        }

                        string hairColorString = storyNode.SelectSingleNode("hairColor")?.InnerText;
                        Color hairColor = (Color)ParseHelper.FromString(hairColorString, typeof(Color));
                        if (hairColor != null) {
                            p.story.hairColor = hairColor;
                        }
                    }
                    XmlNodeList traitsList = storyNode.SelectNodes("traits/allTraits/li");
                    if (traitsList != null) {
                        p.story.traits.allTraits.RemoveAll(_ => true);
                        foreach (XmlNode traitNode in traitsList) {
                            string traitDefName = traitNode.SelectSingleNode("def")?.InnerText;
                            int traitDegree = 0;
                            int.TryParse(traitNode.SelectSingleNode("degree")?.InnerText, out traitDegree);

                            TraitDef traitDef = DefDatabase<TraitDef>.GetNamedSilentFail(traitDefName);
                            if (traitDef == null) continue;

                            Trait t = new Trait(traitDef, traitDegree);
                            if (t == null) continue;

                            p.story.traits.allTraits.Add(t);
                        }
                    }
                }

                // ==== SKILLS ====
                var skills = root.SelectSingleNode("saveable[@Class='Pawn_SkillTracker']");
                if (skills != null) {
                    XmlNodeList skillsList = storyNode.SelectNodes("skills/li");

                    foreach (XmlNode skillNode in skillsList) {
                        string skillDefName = skillNode.SelectSingleNode("def")?.InnerText;
                        int level = 0;
                        int.TryParse(skillNode.SelectSingleNode("level")?.InnerText, out level);

                        float xp = 0;
                        float.TryParse(skillNode.SelectSingleNode("xpSinceLastLevel")?.InnerText, out xp);

                        SkillDef skillDef = DefDatabase<SkillDef>.GetNamedSilentFail(skillDefName);
                        if (skillDef == null) continue;

                        SkillRecord skillRecord = p.skills.GetSkill(skillDef);
                        if (skillRecord == null) {
                            skillRecord = new SkillRecord(p, skillDef);
                        }

                        skillRecord.Level = level;
                        skillRecord.xpSinceLastLevel = xp;

                        try {
                            string passionTypeName = skillNode.SelectSingleNode("passion")?.InnerText;
                            if (passionTypeName != null) {
                                skillRecord.passion = (Passion)Enum.Parse(typeof(Passion), passionTypeName);
                            }
                        } catch (Exception) { }
                    }
                }
                //Debug.Message("got traits and skills");

                // ==== HEALTH ====
                var healthNode = root.SelectSingleNode("saveable[@Class='Pawn_HealthTracker']");
                if (healthNode != null) {


                    XmlNode healthState = healthNode.SelectSingleNode("healthState");
                    if (healthState?.InnerText == "Dead") {
                        p.health.SetDead();
                    }

                    XmlNodeList hediffsList = healthNode.SelectNodes("hediffSet/hediffs/li");
                    if (hediffsList != null) {

                        Scribe.mode = LoadSaveMode.LoadingVars;
                        p.health?.hediffSet?.hediffs?.RemoveAll(_ => true);
                        //probably should pre-analyze hediffs prior to instantiating
                        foreach (XmlNode hediffNode in hediffsList) {
                            var sourceNode = hediffNode.SelectSingleNode("source");
                            var source = sourceNode?.InnerText;
                            //Debug.Message("Source is {0} in hediff {1}", source, hediffNode.OuterXml);
                            if (source != null) {
                                ThingDef sourceThingDef = DefDatabase<ThingDef>.GetNamedSilentFail(source);
                                //Debug.Message("Found non-null source node: {0}. Def: {1}", sourceNode.OuterXml, sourceThingDef);
                                if (sourceThingDef == null) {
                                    hediffNode.RemoveChild(sourceNode);
                                    //Debug.Message("def not found, removing node, result: {0}", hediffNode.OuterXml);
                                    //continue; //skip hediffs with unknown source
                                    //} else {
                                    //Debug.Message("def found: {0}", sourceThingDef);
                                }
                            }
                            try {
                                Hediff hediff = ScribeExtractor.SaveableFromNode<Hediff>(hediffNode, null);
                                if (hediff != null) {
                                    if (hediff.source != null && hediff.Part != null) {
                                        p.health.AddHediff(hediff);
                                    }
                                }
                            } catch (Exception e) {
                            }
                        }
                        Scribe.mode = LoadSaveMode.Inactive;
                    }
                }
                //Debug.Message("got health");

                // ==== APPAREL ====
                var apparelNode = root.SelectSingleNode("apparel");
                if (apparelNode != null) {
                    XmlNodeList apparelList = apparelNode.SelectNodes("item");
                    foreach (XmlNode item in apparelList) {
                        string defName = item.Attributes?.GetNamedItem("def")?.Value;
                        string stuffDefName = item.Attributes?.GetNamedItem("stuffDef")?.Value;

                        ThingDef stuffDef = null;
                        ThingDef thingDef = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
                        if (stuffDefName != null) {
                            stuffDef = DefDatabase<ThingDef>.GetNamedSilentFail(stuffDefName);
                        }

                        if (thingDef != null) {
                            Apparel apparel = (Apparel)ThingMaker.MakeThing(thingDef, stuffDef);
                            if (apparel is Apparel) {
                                p.apparel.Wear(apparel, false);
                            }
                        }
                    }
                }
                return p;


            } catch (Exception e) {
                Debug.Message("Exception while creating pawn: {0}", e);
                return PawnGenerator.GeneratePawn(PawnKindDefOf.AncientSoldier, rp.faction);
            }
        }

        private Thing MakeThingFromItemTile(ItemTile itemTile, bool enableLogging = false) {

            try {
                if (enableLogging) {
                    //Debug.Message("Trying to create new inner item {0}", itemTile.defName);
                }

                if (itemTile.defName == "Pawn") {
                    //Debug.Message("Now need to instantiate pawn");
                    return MakePawnWithRawXml(itemTile.itemXml);
                }

                if (itemTile.defName == "Corpse") {
                    if (itemTile.innerItems != null) {
                        //Debug.Message("Creating corpse");
                        Pawn p = (Pawn)MakeThingFromItemTile(itemTile.innerItems.First());
                        Corpse corpse = null;
                        if (p.Corpse != null) {
                            corpse = p.Corpse;
                        } else {
                            corpse = (Corpse)ThingMaker.MakeThing(p.RaceProps.corpseDef);
                            corpse.InnerPawn = p;
                        }
                        corpse.timeOfDeath = (int)(itemTile.corpseDeathTime + (ticksInYear * blueprint.dateShift));

                        CompRottable rottable = corpse.TryGetComp<CompRottable>();
                        if (rottable != null) rottable.RotProgress = ticksInYear * (-blueprint.dateShift);
                        return corpse;
                    }
                    return null;
                }

                if (itemTile.defName.Contains("Corpse") || itemTile.defName.Contains("Minified")) { //should bypass older minified things and corpses
                    if (itemTile.innerItems == null) return null;
                }

                if (itemTile.defName == "Hive") return null; //Ignore hives, probably should add more comprehensive ignore list here.

                ThingDef thingDef = DefDatabase<ThingDef>.GetNamed(itemTile.defName, false); //here thingDef is definitely not null because it was checked earlier

                ThingDef stuffDef = null; //but stuff can still be null, or can be missing, so we have to check and use default just in case.
                if (itemTile.stuffDef != null && thingDef.MadeFromStuff) { //some mods may alter thing and add stuff parameter to it. this will result in a bug on a vanilla, so need to double-check here
                    stuffDef = DefDatabase<ThingDef>.GetNamed(itemTile.stuffDef, false);
                }

                if (stuffDef == null) {
                    if (itemTile.isWall && thingDef.MadeFromStuff) {
                        stuffDef = ThingDefOf.BlocksGranite; //walls from modded materials becomes granite walls.
                    } else {
                        stuffDef = GenStuff.DefaultStuffFor(thingDef);
                    }
                }

                Thing thing = ThingMaker.MakeThing(thingDef, stuffDef);
                if (thing.def.defName.Contains("AJO")) {
                    Debug.Message("Added new thing: {0}", thing);
                }

                if (thing != null) {
                    if (itemTile.innerItems != null && thing is IThingHolder) {
                        //Debug.Message("Found inners");
                        foreach (ItemTile innerTile in itemTile.innerItems) {
                            Thing innerThing = MakeThingFromItemTile(innerTile, true);
                            ((IThingHolder)thing).GetDirectlyHeldThings().TryAdd(innerThing);
                        }
                    }

                    if (thingDef.CanHaveFaction) {
                        thing.SetFaction(rp.faction);
                    }

                    //Check quality and attach art
                    CompQuality q = thing.TryGetComp<CompQuality>();
                    if (q != null) {
                        byte category = (byte)Math.Abs(Math.Round(Rand.Gaussian(0, 2)));

                        if (itemTile.art != null) {
                            category += 4;
                            if (category > 6) category = 6;
                            q.SetQuality((QualityCategory)category, ArtGenerationContext.Outsider); //setquality resets art, so it should go before actual setting art
                            thing.TryGetComp<CompArt>()?.InitializeArt(itemTile.art.author, itemTile.art.title, itemTile.art.TextWithDatesShiftedBy(blueprint.dateShift));
                        } else {
                            if (category > 6) category = 6;
                            q.SetQuality((QualityCategory)category, ArtGenerationContext.Outsider);
                        }
                    }


                    if (itemTile.stackCount > 1) {
                        thing.stackCount = Rand.Range(1, Math.Min(thingDef.stackLimit, itemTile.stackCount));


                        //Spoil things that can be spoiled. You shouldn't find a fresh meat an the old ruins.
                        CompRottable rottable = thing.TryGetComp<CompRottable>();
                        if (rottable != null) {
                            //if deterioration degree is > 0.5 you definitely won't find any food.
                            //anyway, there is a chance that you also won't get any food even if deterioriation is relatively low. animalr, raiders, you know.
                            if (options.canHaveFood) {
                                rottable.RotProgress = (Rand.Value * 0.5f + options.deteriorationMultiplier) * (rottable.PropsRot.TicksToRotStart);
                            } else {
                                rottable.RotProgress = rottable.PropsRot.TicksToRotStart + 1;
                            }
                        }
                    }

                    if (itemTile.attachedText != null && thing is ThingWithComps) {
                        ThingWithComps thingWithComps = thing as ThingWithComps;
                        Type CompTextClass = Type.GetType("SaM.CompText, Signs_and_Memorials");
                        if (CompTextClass != null) {
                            System.Object textComp = null;
                            for (int i = 0; i < thingWithComps.AllComps.Count; i++) {
                                var val = thingWithComps.AllComps[i];
                                if (val.GetType() == CompTextClass) {
                                    textComp = val;
                                }
                            }

                            //var textComp = Activator.CreateInstance(CompTextClass);
                            if (textComp != null) {
                                textComp?.GetType()?.GetField("text").SetValue(textComp, itemTile.attachedText);
                            }
                            //thingWithComps.
                        }
                    }

                    //Substract some hit points. Most lilkely below 400 (to make really strudy structures stay almost untouched. No more 1% beta poly walls)
                    var maxDeltaHP = Math.Min(thing.MaxHitPoints - 1, (int)Math.Abs(Rand.Gaussian(0, 200)));
                    thing.HitPoints = thing.MaxHitPoints - Rand.Range(0, maxDeltaHP);

                    //Forbid haulable stuff
                    if (thing.def.EverHaulable) {
                        thing.SetForbidden(true, false);
                    }

                    if (thing is Building_Storage) {
                        ((Building_Storage)thing).settings.Priority = StoragePriority.Unstored;
                    }
                }
                return thing;
            } catch (Exception e) {
                Debug.Message("Failed to spawn item {0} because of {1}", itemTile.defName, e);
                return null;
            }
        }


        public BlueprintTransferUtility(Blueprint blueprint, Map map, ResolveParams rp) {
            this.blueprint = blueprint;
            this.map = map;
            this.rp = rp;
            mapOriginX = rp.rect.BottomLeft.x;
            mapOriginZ = rp.rect.BottomLeft.z;
            this.options = rp.GetCustom<ScatterOptions>(Constants.ScatterOptions);
        }

        public void RemoveIncompatibleItems() {
            //Each item should be checked if it can be placed or not. This should help preventing situations when simulated scavenging removes things which anyway won't be placed.
            //For each placed item it's cost should be calculated
            for (int x = 0; x < blueprint.width; x++) {
                for (int z = 0; z < blueprint.height; z++) {

                    if (blueprint.itemsMap[x, z] == null) { blueprint.itemsMap[x, z] = new List<ItemTile>(); }//to make thngs easier add empty list to every cell

                    IntVec3 mapLocation = new IntVec3(x + mapOriginX, 0, z + mapOriginZ);
                    if (!mapLocation.InBounds(map)) continue;

                    List<ItemTile> items = blueprint.itemsMap[x, z];
                    TerrainTile terrain = blueprint.terrainMap[x, z];
                    TerrainDef terrainDef = null;

                    if (terrain != null) {
                        terrainDef = DefDatabase<TerrainDef>.GetNamed(terrain.defName, false);
                        if (terrainDef == null) {
                            blueprint.terrainMap[x, z] = null; //no terrain def means terrain can't be generated.
                            terrain = null;
                        }
                    }

                    TerrainDef existingTerrain = map.terrainGrid.TerrainAt(mapLocation);
                    if (terrainDef != null && terrainDef.terrainAffordanceNeeded != null && !existingTerrain.affordances.Contains(terrainDef.terrainAffordanceNeeded)) {
                        terrainDef = null;
                        blueprint.terrainMap[x, z] = null; //erase terrain if underlying terrain can't support it.
                        blueprint.roofMap[x, z] = false; //removing roof as well just in case
                    }

                    List<ItemTile> itemsToRemove = new List<ItemTile>();
                    foreach (ItemTile item in items) {

                        ThingDef thingDef = DefDatabase<ThingDef>.GetNamed(item.defName, false);
                        if (thingDef == null) {
                            itemsToRemove.Add(item);
                            continue;
                        }

                        if (thingDef.terrainAffordanceNeeded != null) {
                            if (thingDef.EverTransmitsPower && options.shouldKeepDefencesAndPower) continue; //ignore affordances for power transmitters if we need to keep defence systems

                            if (terrainDef != null && terrainDef.terrainAffordanceNeeded != null && existingTerrain.affordances.Contains(terrainDef.terrainAffordanceNeeded)) {
                                if (!terrainDef.affordances.Contains(thingDef.terrainAffordanceNeeded)) { //if new terrain can be placed over existing terrain, checking if an item can be placed over a new terrain
                                    itemsToRemove.Add(item);
                                    blueprint.roofMap[x, z] = false;
                                }
                            } else {
                                if (!existingTerrain.affordances.Contains(thingDef.terrainAffordanceNeeded)) {//otherwise checking if the item can be placed over the existing terrain.
                                    itemsToRemove.Add(item);
                                    blueprint.roofMap[x, z] = false;
                                }
                            }
                        }
                    }

                    foreach (ItemTile item in itemsToRemove) {
                        if (item.isWall || item.isDoor) {
                            blueprint.RemoveWall(item.location.x, item.location.z);
                        }

                        items.Remove(item);
                    }
                }
            }
        }

        public void Transfer() {
            //Planting blueprint
            float totalCost = 0;

            for (int z = 0; z < blueprint.height; z++) {
                for (int x = 0; x < blueprint.width; x++) {

                    IntVec3 mapLocation = new IntVec3(x + mapOriginX, 0, z + mapOriginZ);
                    if (!mapLocation.InBounds(map)) continue;


                    //Check if thepoint is in allowed bounds of the map
                    if (!mapLocation.InBounds(map) || mapLocation.InNoBuildEdgeArea(map)) {
                        continue; //ignore invalid cells
                    }

                    //Construct terrain if some specific terrain stored in the blueprint
                    if (blueprint.terrainMap[x, z] != null) {
                        TerrainDef blueprintTerrain = TerrainDef.Named(blueprint.terrainMap[x, z].defName);
                        if (!map.terrainGrid.TerrainAt(mapLocation).IsWater) {
                            map.terrainGrid.SetTerrain(mapLocation, blueprintTerrain);
                            totalCost += blueprint.terrainMap[x, z].cost;
                        }
                    }

                    /*if (roofMap[x, z] == true) {
                        map.roofGrid.SetRoof(mapLocation, RoofDefOf.RoofConstructed);
                    }*/ //no roof yet


                    //Add items
                    if (blueprint.itemsMap[x, z] != null && blueprint.itemsMap[x, z].Count > 0/* && cellUsed[mapLocation.x, mapLocation.z] == false*/) {

                        bool cellIsAlreadyCleared = false;

                        foreach (ItemTile itemTile in blueprint.itemsMap[x, z]) {
                            //if (!itemTile.defName.ToLower().Contains("wall")) { Debug.Message("Processing item {0} at {1}, {2}", itemTile.defName, x, z); }

                            if (!cellIsAlreadyCleared) { //first item to be spawned should also clear place for itself. we can't do it beforehand because we don't know it it will be able and get a chance to be spawned.
                                bool forceCleaning = (blueprint.wallMap[x, z] > 1) && Rand.Chance(0.9f);

                                if (!ClearCell(mapLocation, map, forceCleaning)) {
                                    break; //if cell was not cleared successfully -> break things placement cycle and move on to the next item
                                } else {
                                    cellIsAlreadyCleared = true;
                                }
                            }

                            if ((blueprint.wallMap[x, z] > 1 || blueprint.wallMap[x, z] == -1) && !map.roofGrid.Roofed(mapLocation)) {
                                map.roofGrid.SetRoof(mapLocation, RoofDefOf.RoofConstructed);
                            }

                            Thing thing = MakeThingFromItemTile(itemTile);
                            if (thing != null) {
                                try {
                                    GenSpawn.Spawn(thing, mapLocation, map, new Rot4(itemTile.rot));
                                    try {
                                        switch (thing.def.tickerType) {
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
                                        //Debug.Message("Ticked");

                                    } catch (Exception e) {
                                        Debug.Message("Exception while tried to perform tick for {0}", thing.def.defName);
                                        thing.Destroy();
                                        throw e;
                                    }

                                    //Debug.Message("Setting up props");
                                    //Breakdown breakdownables: it't yet impossible to silently breakdown an item which is not spawned.
                                    CompBreakdownable b = thing.TryGetComp<CompBreakdownable>();
                                    if (b != null) {
                                        if (Rand.Chance(0.8f)) {
                                            b.DoBreakdown();
                                        }
                                    }

                                    //reduce HP for haulable things in water
                                    if (thing.def.EverHaulable) {
                                        TerrainDef t = map.terrainGrid.TerrainAt(mapLocation);
                                        if (t != null && t.IsWater) {
                                            thing.HitPoints = (thing.HitPoints - 10) / Rand.Range(5, 20) + Rand.Range(1, 10); //things in marsh or river are really in bad condition
                                        }
                                    }
                                } catch (Exception e) {
                                    Debug.Message("Failed to spawn item {0} because of {1}", thing, e);
                                    //ignore
                                }
                            }
                        }
                    }
                }
            }

            //Lords are for significant resistance only. Otherwise lords will be spawned for each small ruins chunk.
            if (options.shouldAddSignificantResistance) {
                Lord lord = LordMaker.MakeNewLord(rp.faction, new LordJob_DefendBase(rp.faction, new IntVec3(mapOriginX + (blueprint.width) / 2, 0, mapOriginZ + (blueprint.height) / 2)), map);
            }

            Debug.Message("Transferred blueprint of total cost of approximately {0}", totalCost);
        }

        public void AddFilthAndRubble() {
            for (int z = 0; z < blueprint.height; z++) {
                for (int x = 0; x < blueprint.width; x++) {


                    IntVec3 mapLocation = new IntVec3(x + mapOriginX, 0, z + mapOriginZ);
                    if (!mapLocation.InBounds(map)) continue;
                    TerrainDef td = map.terrainGrid.TerrainAt(mapLocation);

                    if (!options.shouldCutBlueprint && (td == null || !td.Removable)) {
                        continue; //if base is uncut, scatter filth only on constructed surfaces.
                    }


                    ThingDef[] filthDef = { ThingDefOf.Filth_Dirt, ThingDefOf.Filth_Trash, ThingDefOf.Filth_Ash };
                    FilthMaker.MakeFilth(mapLocation, map, filthDef[0], Rand.Range(0, 3));

                    while (Rand.Value > 0.7) {
                        FilthMaker.MakeFilth(mapLocation, map, filthDef[Rand.Range(0, 2)], Rand.Range(1, 5));
                    }

                    if (options.shouldKeepDefencesAndPower && Rand.Chance(0.05f)) {
                        FilthMaker.MakeFilth(mapLocation, map, ThingDefOf.Filth_Blood, Rand.Range(1, 5));
                    }

                    if (Rand.Chance(0.01f)) { //chance to spawn slag chunk
                        List<Thing> things = map.thingGrid.ThingsListAt(mapLocation);
                        bool canPlace = true;
                        foreach (Thing t in things) {
                            if (t.def.fillPercent > 0.5) canPlace = false;
                        }

                        if (canPlace) {
                            Thing slag = ThingMaker.MakeThing(ThingDefOf.ChunkSlagSteel);
                            GenSpawn.Spawn(slag, mapLocation, map, new Rot4(Rand.Range(0, 4)));
                        }
                    }
                }
            }
        }
    }
}
