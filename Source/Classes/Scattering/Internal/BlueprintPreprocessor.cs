using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Verse;
using RimWorld;

/** 
 * This class processes blueprint right after loading, removing all completely incompatible or undesired things
 * */

namespace RealRuins {
    class BlueprintPreprocessor {
        public static void ProcessBlueprint(Blueprint blueprint, ScatterOptions options) {
            if (blueprint == null) return;


            //Each item should be checked if it can be placed or not. This should help preventing situations when simulated scavenging removes things which anyway won't be placed.
            //For each placed item it's cost should be calculated
            for (int x = 0; x < blueprint.width; x++) {
                for (int z = 0; z < blueprint.height; z++) {

                    List<ItemTile> items = blueprint.itemsMap[x, z];
                    TerrainTile terrain = blueprint.terrainMap[x, z];
                    TerrainDef terrainDef = null;

                    if (terrain != null) {
                        terrainDef = DefDatabase<TerrainDef>.GetNamed(terrain.defName, false);
                        if (terrainDef == null) {
                            blueprint.terrainMap[x, z] = null; //remove unloadable terrain
                            terrain = null;
                        }
                    }



                    List<ItemTile> itemsToRemove = new List<ItemTile>();
                    if (items == null) continue;

                    foreach (ItemTile item in items) {
                        if (item.defName == "Corpse") continue; //TODO: make some better way of handling corpses
                        //We can't move further with corpse item, because this item's thingDef is always null (actual corpse's def name depends on it's kind)

                        ThingDef thingDef = DefDatabase<ThingDef>.GetNamed(item.defName, false);

                        if (thingDef == null) {
                            itemsToRemove.Add(item);
                            continue;
                        }

                        //remove items we don't want to see in the ruins
                        if (thingDef == ThingDefOf.Campfire || thingDef == ThingDefOf.TorchLamp) {
                            itemsToRemove.Add(item);
                            continue;
                        }

                        if (options.wallsDoorsOnly) { //eleminate almost everything if "doors & walls" setting is active
                            if (!thingDef.IsDoor && !item.defName.ToLower().Contains("wall")) {
                                itemsToRemove.Add(item);
                                continue;
                            }
                        }

                        if (options.disableSpawnItems) { //eleminate haulables if corresponding tick is set
                            if (thingDef.EverHaulable) {
                                itemsToRemove.Add(item);
                                continue;
                            }
                        }

                        if (thingDef.defName.Contains("Animal") || thingDef.defName.Contains("Spot")) {
                            itemsToRemove.Add(item);
                            continue; //remove animal sleeping beds and spots as wild animals tend to concentrate around. remove wedding, butchering and party spots, caravan spots as well
                        }


                        if (thingDef.IsCorpse || thingDef.Equals(ThingDefOf.MinifiedThing)) { //check if corpses and minified things contain inner data, otherwise ignore
                            if ((item.innerItems?.Count() ?? 0) == 0 && item.itemXml == null) {
                                itemsToRemove.Add(item);
                                continue;
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

            //Recalculate cost data after removing some items (should speed up, as cost calculation seems to be cpu-expensive process)
            blueprint.UpdateBlueprintStats(true);

            //Perform removing all items exceeding maximum cost
            for (int x = 0; x < blueprint.width; x++) {
                for (int z = 0; z < blueprint.height; z++) {

                    List<ItemTile> items = blueprint.itemsMap[x, z];
                    TerrainTile terrain = blueprint.terrainMap[x, z];

                    List<ItemTile> itemsToRemove = new List<ItemTile>();
                    if (terrain != null) {
                        if (terrain.cost > options.itemCostLimit) blueprint.terrainMap[x, z] = null;
                    }

                    if (items == null) continue;
                    foreach (ItemTile item in items) {
                        if (options.itemCostLimit > 0 && options.itemCostLimit < 1000) { //filter too expensive items. limit of 1000 means "no limit" actually
                            if (item.cost > options.itemCostLimit) { //removes only items where at least one item is more expensive than limit we have. limiting stacks is done later.
                                itemsToRemove.Add(item);
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
    }
}
