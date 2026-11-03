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
                        // Handle corpses specially - they don't have a standard ThingDef, but we can process them if they have inner items
                        if (item.defName.ToLower() == "corpse") {
                            // Check if corpse has a pawn inside; if not, remove it
                            if ((item.innerItems?.Count() ?? 0) == 0 && item.itemXml == null) {
                                itemsToRemove.Add(item);
                            }
                            continue; // Skip the standard thingDef lookup for corpses
                        }

                        ThingDef thingDef = DefDatabase<ThingDef>.GetNamed(item.defName, false);

                        if (thingDef == null) {
                            itemsToRemove.Add(item);
                            continue;
                        }

                        //remove items we don't want to see in the ruins
                        //toxic wastepacks dissolve into pollution, nobody wants to loot them (ThingDefOf.Wastepack is null without Biotech)
                        if (thingDef == ThingDefOf.Campfire || thingDef == ThingDefOf.TorchLamp || thingDef == ThingDefOf.Wastepack) {
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

            //Perform removing all items exceeding maximum cost. Limits of 0 and 1000 mean "no limit" actually
            if (options.itemCostLimit <= 0 || options.itemCostLimit >= 1000) return;

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
                        //a container is judged by its own value: too expensive contents are taken out, the cheap ones stay inside
                        RemoveExpensiveContents(item, options.itemCostLimit);
                        if (item.OwnCost > options.itemCostLimit) { //removes only items where at least one item is more expensive than limit we have. limiting stacks is done later.
                            itemsToRemove.Add(item);
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

        //Removes contents more expensive than the limit, at any depth
        private static void RemoveExpensiveContents(ItemTile container, float limit) {
            if (container.innerItems == null) return;
            foreach (ItemTile inner in container.innerItems.ToList()) {
                RemoveExpensiveContents(inner, limit);
                if (inner.OwnCost > limit) inner.TakeOutOfContainer();
            }
        }
    }
}
