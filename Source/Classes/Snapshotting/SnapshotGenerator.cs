using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Verse;
using RimWorld;

namespace RealRuins {
    class SnapshotGenerator {
        private Map map;
        public SnapshotGenerator(Map map) {
            this.map = map;
        }

        public string Generate() {

            if (map.areaManager.Home.ActiveCells.Count() < 100) return null;

            int xmin = 10000, xmax = 0, zmin = 10000, zmax = 0;
            foreach (IntVec3 cell in map.areaManager.Home.ActiveCells) {
                if (cell.x < xmin) xmin = cell.x;
                if (cell.x > xmax) xmax = cell.x;
                if (cell.z < zmin) zmin = cell.z;
                if (cell.z > zmax) zmax = cell.z;
            }

            Log.Message(string.Format("Home area bounds: ({0}, {1}) - ({2}, {3})", xmin, zmin, xmax, zmax));

            int originX = xmin;// Rand.Range(xmin, (xmin + xmax) / 2);
            int originZ = zmin;// Rand.Range(zmin, (zmin + zmax) / 2);

            int width = xmax - xmin;// Rand.Range(originX + 1, xmax) - originX;
            int height = zmax - zmin;// Rand.Range(originZ + 1, zmax) - originZ;

            Log.Message(string.Format("Origin: {0}, {1}", originX, originZ));

            CellRect rect = new CellRect(originX, originZ, width, height);

            Log.Message(string.Format("Start capturing in area of: ({0},{1})-({2},{3})", rect.minX, rect.minZ, rect.maxX, rect.maxZ));

            StringBuilder fileBuilder = new StringBuilder();
            fileBuilder.AppendFormat("<snapshot width=\"{0}\" height=\"{1}\" biomeDef=\"{2}\">\n", width, height, map.Biome.defName);

            for (int z = rect.minZ; z < rect.maxZ; z++) {
                for (int x = rect.minX; x < rect.maxX; x++) {


                    StringBuilder terrainBuilder = new StringBuilder();
                    StringBuilder itemsBuilder = new StringBuilder();
                    StringBuilder roofBuilder = new StringBuilder();

                    IntVec3 cellVec = new IntVec3(x, 0, z);
                    List<Thing> things = map.thingGrid.ThingsListAt(cellVec);
                    TerrainDef terrain = map.terrainGrid.TerrainAt(cellVec);
                    RoofDef roof = map.roofGrid.RoofAt(cellVec);


                    if (terrain.BuildableByPlayer) {
                        terrainBuilder.AppendFormat("\t\t<terrain def=\"{0}\" />\n", terrain.defName);
                    }



                    if (roof != null) {
                        if (!roof.isNatural) {
                            roofBuilder.AppendFormat("\t\t<roof />\n");
                        }
                    }

                    if (things.Count > 0) {

                        foreach (Thing thing in things) {
                            if (thing.Position.Equals(cellVec)) {//store multicell object only if we're looking at it's origin point.

                                //Use only buildings and items. Ignoring pawns, trees, filth and so on
                                if (thing.def.category == ThingCategory.Building || thing.def.category == ThingCategory.Item) {
                                    if (thing.def.building != null && thing.def.building.isNaturalRock) continue; //ignoring natural rocks too

                                    itemsBuilder.AppendFormat("\t\t<item def=\"{0}\"", thing.def.defName);

                                    if (thing.Stuff != null) {
                                        itemsBuilder.AppendFormat(" stuffDef=\"{0}\"", thing.Stuff.defName);
                                    }

                                    if (thing.stackCount > 1) {
                                        itemsBuilder.AppendFormat(" stackCount=\"{0}\"", thing.stackCount);
                                    }

                                    if (thing.Rotation != null) {
                                        itemsBuilder.AppendFormat(" rot=\"{0}\"", thing.Rotation.AsByte);
                                    }

                                    itemsBuilder.Append(" />\n");
                                }
                            }
                        }
                    }

                    if (terrainBuilder.Length > 0 || itemsBuilder.Length > 0 || roofBuilder.Length > 0) {
                        StringBuilder nodeBuilder = new StringBuilder();
                        nodeBuilder.AppendFormat("\t<cell x=\"{0}\" z=\"{1}\">\n", x - xmin, z - zmin);
                        if (terrainBuilder.Length > 0) {
                            nodeBuilder.Append(terrainBuilder.ToString());
                        }
                        if (itemsBuilder.Length > 0) {
                            nodeBuilder.Append(itemsBuilder.ToString());
                        }
                        if (roofBuilder.Length > 0) {
                            nodeBuilder.Append(roofBuilder.ToString());
                        }
                        nodeBuilder.Append("\t</cell>\n");
                        fileBuilder.Append(nodeBuilder.ToString());
                    }
                }
            }

            fileBuilder.Append("</snapshot>");

            string tmpPath = System.IO.Path.GetTempFileName();
            System.IO.File.WriteAllText(tmpPath, fileBuilder.ToString());

            return tmpPath;
        }
    }
}
