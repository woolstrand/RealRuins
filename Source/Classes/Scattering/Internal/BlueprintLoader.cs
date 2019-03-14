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
        public static Blueprint LoadWholeBlueprintAtPath(string path) {
            BlueprintLoader loader = new BlueprintLoader(path);
            loader.LoadBlueprint();
            return loader.blueprint;
        }

        public static Blueprint LoadRandomBlueprintPartAtPath(string path, IntVec3 size) {
            BlueprintLoader loader = new BlueprintLoader(path);
            loader.LoadBlueprint();
            loader.CutRandomRectOfSize(size);
            return loader.blueprint;
        }


        string snapshotName = null;
        Blueprint blueprint = null;

        public BlueprintLoader(string path) {
            snapshotName = path;
        }

        private void LoadBlueprint() {
            Debug.Message("Loading blueprint at path {0}", snapshotName);

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
            Debug.Message("Cutting area of size {0}x{1}", size.x, size.z);

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
                    var location = new IntVec3(x - minX, 0, z - minZ);
                    result.roofMap[x - minX, z - minZ] = blueprint.roofMap[x, z];
                    result.terrainMap[x - minX, z - minZ] = blueprint.terrainMap[x, z];
                    result.wallMap[x - minX, z - minZ] = blueprint.wallMap[x, z];
                    result.itemsMap[x - minX, z - minZ] = blueprint.itemsMap[x, z];

                    if (result.itemsMap[x - minX, z - minZ] != null) {
                        foreach (Tile t in result.itemsMap[x - minX, z - minZ]) {
                            t.location = location;
                        }
                    }

                    if (result.terrainMap[x - minX, z - minZ] != null) result.terrainMap[x - minX, z - minZ].location = location;
                }
            }

            result.snapshotYear = blueprint.snapshotYear;
            blueprint = result;
        }
    }
}
