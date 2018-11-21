using System;
using System.Collections.Generic;
using System.IO;
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

        public bool CanGenerate() {
            if (map.areaManager.Home.ActiveCells.Count() < 300) return false;
            return true;
        }

        public string Generate() {

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
            fileBuilder.AppendFormat("<world seed=\"{0}\" tile=\"{1}\" />\n", Find.World.info.seedString, map.Parent.Tile);

            int itemsCount = 0;
            int averageHP = 0;

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
                                    averageHP = (averageHP * itemsCount +
                                                 thing.HitPoints / Math.Max(thing.MaxHitPoints, 1)) / (itemsCount + 1);
                                    itemsCount++;

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

                                    if (thing.def.passability == Traversability.Impassable || thing.def.fillPercent > 0.99) {
                                        itemsBuilder.Append(" actsAsWall=\"1\""); //not always a wall, but should act as a wall
                                    }

                                    if (thing.def.IsDoor) {
                                        itemsBuilder.Append(" isDoor=\"1\"");
                                    }

                                    itemsBuilder.Append(" />\n");
                                }
                            }
                        }
                    }

                    if ((float) itemsCount / ((xmax - xmin) * (zmax - zmin)) < 0.01) {
                        //too low density of user generated things.
                        Debug.Message("Too low ruins density. Ignoring.");
                        return null;
                    }
                    if (averageHP < 0.75) {
                        //average HP < 0.75 means VERY worn base. It's very likely this base is just a bunch of claimed ruins, and we want to prevent recycling ruins as is
                        Debug.Message("Too low ruins average HP. Ignoring.");
                        return null;
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
