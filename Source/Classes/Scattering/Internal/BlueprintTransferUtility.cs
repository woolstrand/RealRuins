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

namespace RealRuins
{
    class BlueprintTransferUtility
    {

        private const long ticksInYear = 3600000;

        // Cached blacklists for efficient O(1) lookup
        private HashSet<string> spawnBlacklistCache;
        private HashSet<string> materialBlacklistCache;
        private string fallbackMaterialName;

        private ThingDef cachedFallbackMaterial;

        Blueprint blueprint;
        ResolveParams rp;
        ScatterOptions options;
        Map map;

        int mapOriginX;
        int mapOriginZ;


        // Clear the cell from other destroyable objects
        private bool ClearCell(IntVec3 location, Map map, bool shouldForceClear = true)
        {
            try
            {
                List<Thing> items = map.thingGrid.ThingsListAt(location);
                foreach (Thing item in items)
                {
                    if (item.def.thingClass.ToString().Contains("DubsBadHygiene")) return false; //Don't mess with bad hygiene because it put app into an endless cycle on removal

                    if (!item.def.destroyable)
                    {
                        return false;
                    }
                    if (item.def.mineable && !shouldForceClear)
                    {//mountain is destroyable only when forcing
                        return false;
                    }
                }

                for (int index = items.Count - 1; index >= 0; index--)
                {
                    items[index].DeSpawn(DestroyMode.Vanish);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }


        private ThingDef GetFallbackMaterial()
        {
            if (cachedFallbackMaterial != null) return cachedFallbackMaterial;
            
            if (!string.IsNullOrEmpty(fallbackMaterialName))
            {
                cachedFallbackMaterial = DefDatabase<ThingDef>.GetNamed(fallbackMaterialName, false);
            }
            
            if (cachedFallbackMaterial == null)
            {
                // Fallback to wood if specified material not found
                cachedFallbackMaterial = DefDatabase<ThingDef>.GetNamed("Wood", false);
            }
            
            return cachedFallbackMaterial;
        }

        private Thing MakeThingFromItemTile(ItemTile itemTile, bool enableLogging = false, int? x = null, int? z = null)
        {
            try
            {
                // Check if this item is blacklisted
                if (spawnBlacklistCache.Contains(itemTile.defName))
                {
                    if (enableLogging)
                    {
                        Debug.Extra(Debug.BlueprintTransfer, "Skipping blacklisted item {0}", itemTile.defName);
                    }
                    return null;
                }

                if (enableLogging)
                {
                    Debug.Log(Debug.BlueprintTransfer, "Trying to create new inner item {0}", itemTile.defName);
                }

                if (itemTile.defName.ToLower() == "pawn")
                {
                    Debug.Log(Debug.BlueprintTransfer, "Now need to instantiate pawn at {0}, {1}", x, z);
                    PawnRestoreUtility restoreUtil = new PawnRestoreUtility(blueprint.dateShift, rp.faction);
                    return restoreUtil.MakePawnWithRawXml(itemTile.itemXml);
                }

                if (itemTile.defName.ToLower() == "corpse")
                {
                    if (itemTile.innerItems != null)
                    {
                        Debug.Log(Debug.BlueprintTransfer, "Now need to instantiate corpse at {0}, {1}", x, z);
                        Pawn p = (Pawn)MakeThingFromItemTile(itemTile.innerItems.First(), x: x, z: z,enableLogging: true);
                        Find.WorldPawns.PassToWorld(p);
                        Corpse corpse = null;
                        if (p.Corpse != null)
                        {
                            corpse = p.Corpse;
                        }
                        else
                        {
                            // Ensure the pawn is marked as dead before creating the corpse
                            if (p != null && !p.Dead)
                            {
                                p.health.SetDead();
                            }
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

                if (itemTile.defName.ToLower().Contains("corpse") || itemTile.defName.ToLower().Contains("minified"))
                { //should bypass older minified things and corpses
                    if ((!itemTile.innerItems?.Any()) ?? true) return null;
                }

                if (itemTile.defName == "Hive") return null; //Ignore hives, probably should add more comprehensive ignore list here.

                ThingDef thingDef = DefDatabase<ThingDef>.GetNamed(itemTile.defName, false); //here thingDef is definitely not null because it was checked earlier
                if (thingDef.category == ThingCategory.Ethereal) return null; //don't spawn ethereals like drop pod landing sites and so on

                ThingDef stuffDef = null; //but stuff can still be null, or can be missing, so we have to check and use default just in case.
                if (itemTile.stuffDef != null && thingDef.MadeFromStuff)
                { //some mods may alter thing and add stuff parameter to it. this will result in a bug on a vanilla, so need to double-check here
                    stuffDef = DefDatabase<ThingDef>.GetNamed(itemTile.stuffDef, false);
                    
                    // Check if this material is blacklisted
                    if (materialBlacklistCache.Contains(itemTile.stuffDef))
                    {
                        ThingDef fallback = GetFallbackMaterial();
                        if (fallback != null)
                        {
                            stuffDef = fallback;
                        }
                        else
                        {
                            // No valid fallback material, discard this item
                            return null;
                        }
                    }
                }

                if (stuffDef == null)
                {
                    if (itemTile.isWall && thingDef.MadeFromStuff)
                    {
                        stuffDef = ThingDefOf.BlocksGranite; //walls from modded materials becomes granite walls.
                    }
                    else
                    {
                        stuffDef = GenStuff.DefaultStuffFor(thingDef);
                    }
                }

                Thing thing = ThingMaker.MakeThing(thingDef, stuffDef);


                if (thing != null)
                {
                    if (itemTile.innerItems != null && thing is IThingHolder)
                    {
                        //Debug.Message("Found inners");
                        foreach (ItemTile innerTile in itemTile.innerItems)
                        {
                            Thing innerThing = MakeThingFromItemTile(innerTile, false);
                            if (innerThing != null)
                            {
                                ((IThingHolder)thing).GetDirectlyHeldThings().TryAdd(innerThing);
                            }
                        }
                        if (thing.GetInnerIfMinified() == null) return null;
                    }

                    if (thingDef.CanHaveFaction)
                    {
                        if (thingDef.IsDoor || thingDef.IsWall) {
                            if (Rand.Chance(options.doorOwnershipProbability)) {
                                thing.SetFaction(rp.faction);
                            } else {
                                thing.SetFaction(null);
                            }
                        }
                    }

                    //Check quality and attach art
                    CompQuality q = thing.TryGetComp<CompQuality>();
                    if (q != null)
                    {
                        byte category = (byte)Math.Abs(Math.Round(Rand.Gaussian(0, 2)));

                        if (itemTile.art != null)
                        {
                            if (category > 6) category = 6;
                            q.SetQuality((QualityCategory)category, ArtGenerationContext.Outsider); //setquality resets art, so it should go before actual setting art
                            thing.TryGetComp<CompArt>()?.InitializeArt(itemTile.art.author, itemTile.art.title, itemTile.art.TextWithDatesShiftedBy(blueprint.dateShift));
                        }
                        else
                        {
                            if (category > 6) category = 6;
                            q.SetQuality((QualityCategory)category, ArtGenerationContext.Outsider);
                        }
                    }


                    if (itemTile.stackCount > 1)
                    {
                        thing.stackCount = itemTile.stackCount;


                        //Spoil things that can be spoiled. You shouldn't find a fresh meat an the old ruins.
                        CompRottable rottable = thing.TryGetComp<CompRottable>();
                        if (rottable != null)
                        {
                            //if deterioration degree is > 0.5 you definitely won't find any food.
                            //anyway, there is a chance that you also won't get any food even if deterioriation is relatively low. animalr, raiders, you know.
                            if (options.canHaveFood)
                            {
                                rottable.RotProgress = (Rand.Value * 0.5f + options.deteriorationMultiplier) * (rottable.PropsRot.TicksToRotStart);
                            }
                            else
                            {
                                rottable.RotProgress = rottable.PropsRot.TicksToRotStart + 1;
                            }
                        }
                    }

                    if (itemTile.attachedText != null && thing is ThingWithComps)
                    {
                        ThingWithComps thingWithComps = thing as ThingWithComps;
                        Type CompTextClass = Type.GetType("SaM.CompText, Signs_and_Memorials");
                        if (CompTextClass != null)
                        {
                            System.Object textComp = null;
                            for (int i = 0; i < thingWithComps.AllComps.Count; i++)
                            {
                                var val = thingWithComps.AllComps[i];
                                if (val.GetType() == CompTextClass)
                                {
                                    textComp = val;
                                }
                            }

                            //var textComp = Activator.CreateInstance(CompTextClass);
                            if (textComp != null)
                            {
                                textComp?.GetType()?.GetField("text").SetValue(textComp, itemTile.attachedText);
                            }
                            //thingWithComps.
                        }
                    }

                    if (thing is UnfinishedThing)
                    {
                        ((UnfinishedThing)thing).workLeft = 10000;
                        ((UnfinishedThing)thing).Creator = Find.WorldPawns.AllPawnsAliveOrDead.RandomElement();
                    }

                    //Subtract some hit points. Most lilkely below 400 (to make really strudy structures stay almost untouched. No more 1% beta poly walls)
                    var maxDeltaHP = 0;
                    if (!options.forceFullHitPoints)
                    {
                        maxDeltaHP = Math.Min(thing.MaxHitPoints - 1, (int)Math.Abs(Rand.Gaussian(0, 200)));
                    }
                    thing.HitPoints = thing.MaxHitPoints - Rand.Range(0, maxDeltaHP);

                    //Forbid haulable stuff
                    if (thing.def.EverHaulable)
                    {
                        thing.SetForbidden(true, false);
                    }

                    if (thing is Building_Storage)
                    {
                        ((Building_Storage)thing).settings.Priority = StoragePriority.Unstored;
                    }
                }
                return thing;
            }
            catch (Exception e)
            {
                Debug.Log(Debug.BlueprintTransfer, "Failed to spawn item {0} because of {1}", itemTile.defName, e);
                return null;
            }
        }


        public BlueprintTransferUtility(Blueprint blueprint, Map map, ResolveParams rp, ScatterOptions options)
        {
            this.blueprint = blueprint;
            this.map = map;
            this.rp = rp;
            this.options = options;

            // Parse and cache blacklists for efficient O(1) lookup
            spawnBlacklistCache = new HashSet<string>();
            if (!string.IsNullOrEmpty(RealRuins_ModSettings.spawnBlacklist))
            {
                foreach (string line in RealRuins_ModSettings.spawnBlacklist.Split(new[] { "\r\n", "\r", "\n", "," }, StringSplitOptions.RemoveEmptyEntries))
                {
                    string trimmed = line.Trim();
                    if (!string.IsNullOrEmpty(trimmed))
                    {
                        spawnBlacklistCache.Add(trimmed);
                    }
                }
            }

            materialBlacklistCache = new HashSet<string>();
            if (!string.IsNullOrEmpty(RealRuins_ModSettings.materialBlacklist))
            {
                foreach (string line in RealRuins_ModSettings.materialBlacklist.Split(new[] { "\r\n", "\r", "\n", "," }, StringSplitOptions.RemoveEmptyEntries))
                {
                    string trimmed = line.Trim();
                    if (!string.IsNullOrEmpty(trimmed))
                    {
                        materialBlacklistCache.Add(trimmed);
                    }
                }
            }

            fallbackMaterialName = RealRuins_ModSettings.fallbackMaterial;
            cachedFallbackMaterial = null; // Will be resolved lazily

            Debug.Log("Transferring blueprint of faction {0}", rp.faction?.Name ?? "none");

            if (blueprint == null) { Debug.Error(Debug.BlueprintTransfer, "Attempting to configure transfer utility with empty blueprint!"); return; }
            if (map == null) { Debug.Error(Debug.BlueprintTransfer, "Attempting to configure transfer utility with empty map!"); return; }
            if (options == null) { Debug.Error(Debug.BlueprintTransfer, "Attempting to configure transfer utility with empty options!"); return; }

            mapOriginX = rp.rect.minX + rp.rect.Width / 2 - blueprint.width / 2;
            mapOriginZ = rp.rect.minZ + rp.rect.Height / 2 - blueprint.height / 2;

            if (mapOriginX < 0) mapOriginX = 0;
            if (mapOriginZ < 0) mapOriginZ = 0;

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
                }
                else
                {
                    Debug.Warning(Debug.BlueprintTransfer, "Tried to override position, but map exceeded bounds and position was reverted due to corresponding options flag.");
                    Debug.Warning(Debug.BlueprintTransfer, "New position: {0}, {1}", mapOriginX, mapOriginZ);
                }
            }
        }

        public void RemoveIncompatibleItems()
        {
            //Each item should be checked if it can be placed or not. This should help preventing situations when simulated scavenging removes things which anyway won't be placed.
            //For each placed item it's cost should be calculated
            if (blueprint.roofMap == null) Debug.Log(Debug.BlueprintTransfer, "Trying to process blueprint with empty roof map");
            if (map == null) Debug.Log(Debug.BlueprintTransfer, "Trying to process blueprint but map is still null");

            try
            {
                int totalItems = 0;
                int removedItems = 0;
                for (int x = 0; x < blueprint.width; x++)
                {
                    for (int z = 0; z < blueprint.height; z++)
                    {

                        //Debug.Extra(Debug.BlueprintTransfer, "Starting cell {0} {1}...", x, z);
                        if (blueprint.itemsMap[x, z] == null) { blueprint.itemsMap[x, z] = new List<ItemTile>(); }//to make thngs easier add empty list to every cell

                        IntVec3 mapLocation = new IntVec3(x + mapOriginX, 0, z + mapOriginZ);
                        if (!mapLocation.InBounds(map)) continue;

                        List<ItemTile> items = blueprint.itemsMap[x, z];
                        TerrainTile terrain = blueprint.terrainMap[x, z];
                        TerrainDef terrainDef = null;

                        if (terrain != null)
                        {
                            terrainDef = DefDatabase<TerrainDef>.GetNamed(terrain.defName, false);
                            if (terrainDef == null)
                            {
                                blueprint.terrainMap[x, z] = null; //no terrain def means terrain can't be generated.
                                terrain = null;
                            }
                        }

                        TerrainDef existingTerrain = map.terrainGrid?.TerrainAt(mapLocation);
                        if (existingTerrain != null && terrainDef != null &&
                            existingTerrain.affordances != null &&
                            terrainDef.terrainAffordanceNeeded != null && !existingTerrain.affordances.Contains(terrainDef.terrainAffordanceNeeded))
                        {
                            terrainDef = null;
                            blueprint.terrainMap[x, z] = null; //erase terrain if underlying terrain can't support it.
                            blueprint.roofMap[x, z] = false; //removing roof as well just in case
                        }

                        //Debug.Extra(Debug.BlueprintTransfer, "Preprocessed cell {0} {1}, moving to items...", x, z);
                        List<ItemTile> itemsToRemove = new List<ItemTile>();
                        foreach (ItemTile item in items)
                        {
                            totalItems++;
                            // Corpses are initialized without defs and are processed separately.
                            if (item.defName.ToLower() == "corpse") continue;

                            ThingDef thingDef = DefDatabase<ThingDef>.GetNamed(item.defName, false);
                            if (thingDef == null)
                            {
                                Debug.Extra(Debug.BlueprintTransfer, "No def for thing {0} at {1}, {2}, discarding", item.defName, x, z);
                                itemsToRemove.Add(item);
                                continue;
                            }

                            Debug.Extra(Debug.BlueprintTransfer, "Making thorough check for thing {0} at {1}, {2}", item.defName, x, z);
                            if (!options.overwritesEverything && thingDef.terrainAffordanceNeeded != null)
                            {
                                if (thingDef.EverTransmitsPower && options.shouldKeepDefencesAndPower) continue; //ignore affordances for power transmitters if we need to keep defence systems

                                if (terrainDef != null && terrainDef.terrainAffordanceNeeded != null && existingTerrain.affordances.Contains(terrainDef.terrainAffordanceNeeded))
                                {
                                    if (!terrainDef.affordances.Contains(thingDef.terrainAffordanceNeeded))
                                    { //if new terrain can be placed over existing terrain, checking if an item can be placed over a new terrain
                                        itemsToRemove.Add(item);
                                        blueprint.roofMap[x, z] = false;
                                        Debug.Extra(Debug.BlueprintTransfer, "Discarded because of unsuitable terrain");
                                    }
                                }
                                else
                                {
                                    if (!(existingTerrain.affordances?.Contains(thingDef.terrainAffordanceNeeded) ?? true))
                                    {//otherwise checking if the item can be placed over the existing terrain.
                                        Debug.Extra(Debug.BlueprintTransfer, "Discarded because of unsuitable terrain");
                                        itemsToRemove.Add(item);
                                        blueprint.roofMap[x, z] = false;
                                    }
                                }
                            }
                        }

                        foreach (ItemTile item in itemsToRemove)
                        {
                            if (item.isWall || item.isDoor)
                            {
                                blueprint.RemoveWall(item.location.x, item.location.z);
                            }

                            items.Remove(item);
                            removedItems++;
                        }
                    }
                }


                Debug.Extra(Debug.BlueprintTransfer, "Finished check, recalculating stats");
                blueprint.UpdateBlueprintStats(true);
                Debug.Log(Debug.BlueprintTransfer, "Blueprint transfer utility did remove {0}/{1} incompatible items. New cost: {2}", removedItems, totalItems, blueprint.totalCost);
            }
            catch (Exception e)
            {
                Debug.Error(Debug.BlueprintTransfer, "Exception while trying to cleanup blueprint details. This should not normally happen, so please report this case: {0}", e.ToString());
            }
        }


        public void Transfer(CoverageMap coverageMap)
        {
            //Planting blueprint
            float totalCost = 0;
            int transferredTerrains = 0;
            int transferredTiles = 0;
            int totalTerrains = 0;
            int totalItems = 0;

            //update rect to actual placement rect using width and height
            rp.rect = new CellRect(mapOriginX, mapOriginZ, blueprint.width, blueprint.height);

            Debug.Extra(Debug.BlueprintTransfer, "Clearing map...");

            for (int z = 0; z < blueprint.height; z++)
            {
                for (int x = 0; x < blueprint.width; x++)
                {
                    try
                    {
                        IntVec3 mapLocation = new IntVec3(x + mapOriginX, 0, z + mapOriginZ);
                        //Check if thepoint is in allowed bounds of the map
                        if (!mapLocation.InBounds(map) || mapLocation.InNoBuildEdgeArea(map))
                        {
                            continue; //ignore invalid cells
                        }

                        if (options.overwritesEverything || Rand.Chance(0.6f))
                        {
                            if (blueprint.terrainMap[x, z] != null ||
                                blueprint.itemsMap[x, z].Count > 0 ||
                                blueprint.wallMap[x, z] > 1)
                            {
                                ClearCell(mapLocation, map, true);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.Warning(Debug.BlueprintTransfer, "Failed to clean cell at {0}, {1} because of {2}", x, z, e);
                    }
                }
            }

            Debug.Extra(Debug.BlueprintTransfer, "Transferring map objects");
            for (int z = 0; z < blueprint.height; z++)
            {
                for (int x = 0; x < blueprint.width; x++)
                {

                    IntVec3 mapLocation = new IntVec3(x + mapOriginX, 0, z + mapOriginZ);
                    if (coverageMap != null)
                    {
                        if (coverageMap.isMarked(mapLocation.x, mapLocation.z) == true)
                        { //cell was used earlier
                            continue; //skip already covered tiles
                        }
                        else
                        {
                            if (blueprint.wallMap[x, z] > 1 || blueprint.wallMap[x, z] == -1) coverageMap.Mark(mapLocation.x, mapLocation.z); //mark cell as used
                        }
                    }


                    //Check if thepoint is in allowed bounds of the map
                    if (!mapLocation.InBounds(map) || mapLocation.InNoBuildEdgeArea(map))
                    {
                        continue; //ignore invalid cells
                    }

                    try
                    {
                        //Construct terrain if some specific terrain stored in the blueprint
                        if (blueprint.terrainMap[x, z] != null)
                        {
                            totalTerrains++;
                            TerrainDef blueprintTerrain = TerrainDef.Named(blueprint.terrainMap[x, z].defName);
                            if (!map.terrainGrid.TerrainAt(mapLocation).IsWater)
                            {
                                map.terrainGrid.SetTerrain(mapLocation, blueprintTerrain);
                                totalCost += blueprint.terrainMap[x, z].cost;
                                transferredTerrains++;
                            }
                        }


                        //construct roof everywhere where it exists in blueprint (for ruins ignoring outside: room with index 1).
                        if (blueprint.roofMap[x, z] == true && (options.overwritesEverything || blueprint.wallMap[x, z] != 1))
                        {
                            // Only set roof if terrain allows it (not over water, etc.)
                            TerrainDef terrainAtLocation = map.terrainGrid?.TerrainAt(mapLocation);
                            if (terrainAtLocation != null && !terrainAtLocation.IsWater)
                            {
                                map.roofGrid.SetRoof(mapLocation, RoofDefOf.RoofConstructed);
                            }
                        }


                        //Debug.Extra(Debug.BlueprintTransfer, "Transferred terrain and roof at cell ({0}, {1})", x, z);
                    }
                    catch (Exception e)
                    {
                        Debug.Extra(Debug.BlueprintTransfer, "Failed to transfer terrain {0} at {1}, {2} because of {3}", blueprint.terrainMap[x, z].defName, x, z, e);
                    }

                    //Add items
                    if (blueprint.itemsMap[x, z] != null && blueprint.itemsMap[x, z].Count > 0/* && cellUsed[mapLocation.x, mapLocation.z] == false*/)
                    {
                        totalItems += blueprint.itemsMap[x, z].Count;
                        foreach (ItemTile itemTile in blueprint.itemsMap[x, z])
                        {
                            Debug.Extra(Debug.BlueprintTransfer, "Creating thing {2} at cell ({0}, {1})", x, z, itemTile.defName);
                            Thing thing = MakeThingFromItemTile(itemTile, false, x, z);
                            if (thing != null)
                            {
                                try
                                {
                                    Rot4 rotation = new Rot4(itemTile.rot);
                                    //check if there is anything we have to despawn in order to spawn our new item
                                    //we have to do this, because many dubs bad hygiene items enter endless cycle when removed during mapgen.
                                    foreach (IntVec3 occupiedCell in GenAdj.CellsOccupiedBy(mapLocation, rotation, thing.def.Size))
                                    {
                                        foreach (Thing existingItem in map.thingGrid.ThingsAt(occupiedCell).ToList())
                                        {
                                            if (GenSpawn.SpawningWipes(thing.def, existingItem.def))
                                            {
                                                if (thing.def.thingClass.ToString().Contains("DubsBadHygiene")) throw new Exception("Can't spawn item because it will destroy Dubs Bad Hygiene Item and it will lead to app freeze.");
                                                existingItem.Destroy(DestroyMode.Vanish);
                                            }
                                        }
                                    }





                                    GenSpawn.Spawn(thing, mapLocation, map, rotation);
                                    Debug.Extra(Debug.BlueprintTransfer, "Spawned");
                                    try
                                    {
                                        switch (thing.def.tickerType)
                                        {
                                            case TickerType.Never:
                                                break;
                                            case TickerType.Normal:
                                                thing.DoTick();
                                                break;
                                            case TickerType.Long:
                                                thing.TickLong();
                                                break;
                                            case TickerType.Rare:
                                                thing.TickRare();
                                                break;
                                        }
                                        //Debug.Message("Ticked");

                                    }
                                    catch (Exception e)
                                    {
                                        Debug.Log(Debug.BlueprintTransfer, "Exception while tried to perform tick for {0} of cost {1}, retrhrowing...", thing.def.defName, itemTile.cost);
                                        thing.Destroy();
                                        throw e;
                                    }

                                    //Debug.Message("Setting up props");
                                    //Breakdown breakdownables: it't yet impossible to silently breakdown an item which is not spawned.
                                    CompBreakdownable b = thing.TryGetComp<CompBreakdownable>();
                                    if (b != null && options.enableDeterioration)
                                    {
                                        if (Rand.Chance(0.8f))
                                        {
                                            b.DoBreakdown();
                                        }
                                    }

                                    //reduce HP for haulable things in water
                                    if (thing.def.EverHaulable)
                                    {
                                        TerrainDef t = map.terrainGrid.TerrainAt(mapLocation);
                                        if (t != null && t.IsWater)
                                        {
                                            thing.HitPoints = (thing.HitPoints - 10) / Rand.Range(5, 20) + Rand.Range(1, 10); //things in marsh or river are really in bad condition
                                        }
                                    }
                                    Debug.Extra(Debug.BlueprintTransfer, "Item completed");

                                    transferredTiles++;
                                    totalCost += itemTile.cost;
                                }
                                catch (Exception e)
                                {
                                    Debug.Warning(Debug.BlueprintTransfer, "Failed to spawn item {0} of cost {1} because of exception {2}", thing, itemTile.cost, e.Message);
                                    //ignore
                                }
                            }
                        }
                    }
                }
            }

            Debug.Log(Debug.BlueprintTransfer, "Finished transferring");
            if (options.shouldKeepDefencesAndPower)
            {
                RestoreDefencesAndPower();
            }
            options.uncoveredCost = totalCost;
            Debug.Log(Debug.BlueprintTransfer, "Transferred blueprint of size {0}x{1}, age {2}, total cost of approximately {3}. Items: {4}/{5}, terrains: {6}/{7}", blueprint.width, blueprint.height, -blueprint.dateShift, totalCost, transferredTiles, totalItems, transferredTerrains, totalTerrains);
        }

        public void AddFilthAndRubble()
        {
            ThingDef[] filthDef = { ThingDefOf.Filth_Dirt, ThingDefOf.Filth_Trash, ThingDefOf.Filth_Ash };

            float[,] filthMap = new float[blueprint.width, blueprint.height];
            for (int z = 0; z < blueprint.height; z++)
            {
                for (int x = 0; x < blueprint.width; x++)
                {
                    if (blueprint.itemsMap[x, z].Count() > 0 || blueprint.terrainMap[x, z] != null)
                    {
                        filthMap[x, z] = 1;
                    }
                }
            }
            filthMap.Blur(2);

            for (int z = 0; z < blueprint.height; z++)
            {
                for (int x = 0; x < blueprint.width; x++)
                {
                    try
                    {
                        IntVec3 mapLocation = new IntVec3(x + mapOriginX, 0, z + mapOriginZ);
                        if (!mapLocation.InBounds(map)) continue;

                        if (filthMap[x, z] <= 0 || Rand.Chance(0.2f)) continue;

                        FilthMaker.TryMakeFilth(mapLocation, map, filthDef[0], Rand.Range(0, 3));

                        while (Rand.Value > 0.7)
                        {
                            FilthMaker.TryMakeFilth(mapLocation, map, filthDef[Rand.Range(0, 2)], Rand.Range(1, 5));
                        }

                        if (options.shouldKeepDefencesAndPower && Rand.Chance(0.05f))
                        {
                            FilthMaker.TryMakeFilth(mapLocation, map, ThingDefOf.Filth_Blood, Rand.Range(1, 5));
                        }

                        if (Rand.Chance(0.01f))
                        { //chance to spawn slag chunk
                            List<Thing> things = map.thingGrid.ThingsListAt(mapLocation);
                            bool canPlace = true;
                            foreach (Thing t in things)
                            {
                                if (t.def.fillPercent > 0.5) canPlace = false;
                            }

                            if (canPlace)
                            {
                                Thing slag = ThingMaker.MakeThing(ThingDefOf.ChunkSlagSteel);
                                GenSpawn.Spawn(slag, mapLocation, map, new Rot4(Rand.Range(0, 4)));
                            }
                        }
                    }
                    catch (Exception)
                    {
                        //what a pity
                    }
                }
            }
        }

        private void RestoreDefencesAndPower()
        {
            foreach (var thing in map.spawnedThings)
            {
                if (thing.TryGetComp<CompPowerPlant>() != null || thing.TryGetComp<CompPowerBattery>() != null || (thing.def.building != null && thing.def.building.IsTurret))
                {
                    CompBreakdownable bdcomp = thing.TryGetComp<CompBreakdownable>();
                    if (bdcomp != null)
                    {
                        bdcomp.Notify_Repaired();
                    }
                }
            }
        }
    }
}
