using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.IO;
using System.Xml;

using Verse;
using RimWorld;

/**
 * This class loads a particular blueprint as much as possible for current game setup.
 * I.e. it does not only creates tiles based on blueprint XML, but it also checks for presence of every item where applicable.
 * It can filter out missing items, but it does not yet filter missing hediffs for pawns.
 * */

namespace RealRuins {

    class BlueprintLoader {
        public static Blueprint LoadWholeBlueprintAtPath(string path, ScatterOptions options) {
            BlueprintLoader loader = new BlueprintLoader(path);
            loader.LoadBlueprint();
            loader.PostProcess(options);
            return loader.blueprint;
        }

        public static Blueprint LoadRandomBlueprintPartAtPath(string path, IntVec3 size, ScatterOptions options) {
            BlueprintLoader loader = new BlueprintLoader(path);
            loader.LoadBlueprint();
            loader.CutRandomRectOfSize(size);
            loader.PostProcess(options);
            return loader.blueprint;
        }


        string snapshotName = null;
        Blueprint blueprint = null;

        public BlueprintLoader(string path) {
            snapshotName = path;
        }

        private void LoadBlueprint() {
            Debug.Message("Loading blueprint at path", snapshotName);

            string deflatedName = snapshotName;
            if (Path.GetExtension(snapshotName).Equals(".bp")) {

                deflatedName = snapshotName + ".xml";
                if (!File.Exists(deflatedName)) {
                    string data = Compressor.UnzipFile(snapshotName);
                    File.WriteAllText(deflatedName, data);
                }
            }

            XmlDocument snapshot = new XmlDocument();
            snapshot.Load(deflatedName);

            XmlNodeList elemList = snapshot.GetElementsByTagName("cell");
            int blueprintWidth = int.Parse(snapshot.FirstChild.Attributes["width"].Value);
            int blueprintHeight = int.Parse(snapshot.FirstChild.Attributes["height"].Value);
            Version blueprintVersion = new Version(snapshot.FirstChild?.Attributes["version"]?.Value ?? "0.0.0.0");
            blueprint = new Blueprint(blueprintWidth, blueprintHeight, blueprintVersion);

            //Snapshot year is an in-game year when snapshot was taken. Thus, all corpse ages, death times, art events and so on are in between of 5500 and [snapshotYear]
            blueprint.snapshotYear = int.Parse(snapshot.FirstChild.Attributes["inGameYear"]?.Value ?? "5600");
            //To prevent artifacts from future we need to shift all dates by some number to the past by _at_least_ (snaphotYear - 5500) years


            if (!RealRuins.SingleFile && (blueprintHeight > 400 || blueprintWidth > 400 || blueprintHeight < 10 || blueprintWidth < 10)) {
                Debug.Message("SKIPPED due to unacceptable linear dimensions", snapshotName);
                blueprint = null;
                return; //wrong size. too small or too large
            }

            /*
             * TODO: Size should be checked outside
             * 
            if (!RealRuins.SingleFile && blueprintHeight * blueprintWidth < options.minimumSizeRequired) {
                Debug.Message("SKIPPED due to area vs options", snapshotName);
                return null;
            }
            */

            /*
            //base deterioration chance mask. is used to create freeform deterioration which is much more fun than just circular
            //deterioration chance may depends on material and cost, but is always based on base chance
            terrainIntegrity = new float[blueprintWidth, blueprintHeight]; //integrity of floor tiles
            itemsIntegrity = new float[blueprintWidth, blueprintHeight]; //base integrity of walls, roofs and items
            */

            //should food ever be spawned for this ruins
            //canHaveFood = Rand.Chance((1.0f - deteriorationDegree) / 4);

            int itemNodes = 0;
            int terrainNodes = 0;

            foreach (XmlNode cellNode in elemList) {
                int x = int.Parse(cellNode.Attributes["x"].Value);
                int z = int.Parse(cellNode.Attributes["z"].Value);
                blueprint.itemsMap[x, z] = new List<ItemTile>();

                foreach (XmlNode cellElement in cellNode.ChildNodes) {
                    try {
                        if (cellElement.Name.Equals("terrain")) {
                            terrainNodes++;
                            TerrainTile terrain = new TerrainTile(cellElement);
                            terrain.location = new IntVec3(x, 0, z);
                            blueprint.terrainMap[x, z] = terrain;

                        } else if (cellElement.Name.Equals("item")) {
                            itemNodes++;
                            ItemTile tile = new ItemTile(cellElement);

                            //replace all collapsed rocks with walls
                            if (tile.defName == ThingDefOf.CollapsedRocks.defName) {
                                tile = ItemTile.WallReplacementItemTile(tile.location);
                            }

                            //Trying to load corresponding definition to check if the object is accessible
                            ThingDef thingDef = DefDatabase<ThingDef>.GetNamed(tile.defName, false);
                            if (thingDef != null) {
                                if (thingDef.fillPercent == 1.0f || tile.isWall || tile.isDoor) {
                                    blueprint.wallMap[x, z] = -1; //place wall
                                }
                                tile.location = new IntVec3(x, 0, z);
                                blueprint.itemsMap[x, z].Add(tile); //save item if it's def is valid.
                            } else {
                                if (tile.isDoor) { //replacing unavailable door with abstract default door
                                    tile.defName = ThingDefOf.Door.defName;
                                    tile.location = new IntVec3(x, 0, z);
                                    blueprint.itemsMap[x, z].Add(tile); //replacement door is ok
                                } else if (tile.isWall || tile.defName.ToLower().Contains("wall")) { //replacing unavailable impassable 100% filling block (which was likely a wall) with a wall
                                    tile.defName = ThingDefOf.Wall.defName;
                                    tile.location = new IntVec3(x, 0, z);
                                    blueprint.itemsMap[x, z].Add(tile); //now it's a wall
                                } else if (tile.defName == "Corpse") {
                                    tile.location = new IntVec3(x, 0, z);
                                    blueprint.itemsMap[x, z].Add(tile); // corpse is ok
                                }
                            }

                        } else if (cellElement.Name.Equals("roof")) {
                            blueprint.roofMap[x, z] = true;
                        }
                    } catch (Exception) {
                        //ignore invalid or unloadable cells
                    }
                }
            }

        }

        private void CutRandomRectOfSize(IntVec3 size) {
            if (blueprint == null) return;
            if (blueprint.width <= size.x && blueprint.height <= size.z) return; //piece size os larger than the blueprint itself => don't alter blueprint

            int centerX = blueprint.width / 2;
            int centerZ = blueprint.height / 2;
            if (blueprint.width > size.x) centerX = Rand.Range(size.x / 2, blueprint.width - size.x / 2);
            if (blueprint.height > size.z) centerZ = Rand.Range(size.z / 2, blueprint.height - size.z / 2);

            int minX = Math.Max(0, centerX - size.x / 2);
            int maxX = Math.Min(blueprint.width - 1, centerX + size.x / 2);
            int minZ = Math.Max(0, centerZ - size.z / 2);
            int maxZ = Math.Min(blueprint.height - 1, centerZ + size.z / 2);

            Blueprint result = new Blueprint(maxX - minX, maxZ - minZ, blueprint.version);
            for (int z = minZ; z < maxZ; z++) {
                for (int x = minX; x < maxX; x++) {
                    result.itemsMap[x - minX, z - minZ] = blueprint.itemsMap[x, z];
                    result.roofMap[x - minX, z - minZ] = blueprint.roofMap[x, z];
                    result.terrainMap[x - minX, z - minZ] = blueprint.terrainMap[x, z];
                    result.wallMap[x - minX, z - minZ] = blueprint.wallMap[x, z];
                }
            }

            result.snapshotYear = blueprint.snapshotYear;
            blueprint = result;
        }

        private void PostProcess(ScatterOptions options) {
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
                            if (item.innerItems == null && item.itemXml == null) {
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
            blueprint.UpdateCostData();

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
