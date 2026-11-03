using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using RimWorld;
using Verse;

/**
 * Each object of this class represents one single blueprint in mod's internal format. It is partially internal classes, partially raw xml.
 * This class also provides related information like blueprint total cost, blueprint defence ability, blueprint size and so on.
 * */

namespace RealRuins {
    class Blueprint {

        // ------------ blueprint internal data structures --------------
        public int width { get; private set; }
        public int height { get; private set; }
        public int originX { get; private set; }
        public int originZ { get; private set; }
        public readonly Version version;

        public float totalCost { get; private set; }
        public float legacyTotalCost { get; private set; } //same with the old cost scheme, for comparison
        public float itemsDensity { get; private set; }
        public int itemsCount { get; private set; }
        public int terrainTilesCount { get; private set; }

        //rooms info. used to create deterioration maps.
        //earlier it was used by deterioration processor only, but now I hsve a few more places where I need this data, so I moved it to the blueprint level
        public int roomsCount { get; private set; }
        public List<int> roomAreas { get; private set; }

        //Real year of the snapshot (5500 + X). used to calculate random offset to make things look realistic (no corpses and art from the future)
        private int snapshotYearInt;
        public int snapshotYear {
            get => snapshotYearInt;
            set {
                snapshotYearInt = value;
                dateShift = -(value - 5500) - ThreadSafeRand.Range(5, 500);
            }
        }
        public int dateShift { get; private set; }

        //map of walls to create room-based deterioration. 0 means "not traversed", -1 means "wall or similar", otherwise it's a room number
        //where 1 is a virtual "outside" room. Technically it's possible to have no "outside" if in the original blueprint there is an
        //continous wall around the whole area. In this case room index 1 will be assigned to the first room traversed.
        public readonly int[,] wallMap;
        public readonly TerrainTile[,] terrainMap;
        public readonly bool[,] roofMap;
        public readonly List<ItemTile>[,] itemsMap;



        public Blueprint(int originX, int originZ, int width, int height, Version version) {
            this.version = version;
            this.width = width;
            this.height = height;
            this.originX = originX;
            this.originZ = originZ;
            wallMap = new int[width, height];
            roofMap = new bool[width, height];
            itemsMap = new List<ItemTile>[width, height];
            terrainMap = new TerrainTile[width, height];
        }

        public void CutIfExceedsBounds(IntVec3 size) {
            if (width > size.x) width = size.x;
            if (height > size.z) height = size.z;
        }



        public void UpdateBlueprintStats(bool includeCost = false) {
            totalCost = 0;
            legacyTotalCost = 0;
            terrainTilesCount = 0;
            itemsCount = 0;
            for (int x = 0; x < width; x ++) {
                for (int z = 0; z < height; z ++) {
                    var items = itemsMap[x, z];
                    if (items == null) continue;
                    foreach (ItemTile item in items) {
                        ThingDef thingDef = DefDatabase<ThingDef>.GetNamed(item.defName, false);
                        ThingDef stuffDef = (item.stuffDef != null) ? DefDatabase<ThingDef>.GetNamed(item.stuffDef, false) : null;
                        if (thingDef == null) continue; //to handle corpses

                        UpdateItemStats(item, thingDef, stuffDef, includeCost);
                        if (includeCost) {
                            totalCost += item.cost;
                            legacyTotalCost += item.legacyCost;
                        }

                        itemsCount++;
                    }

                    var terrainTile = terrainMap[x, z];
                    if (terrainTile != null) {
                        TerrainDef terrainDef = DefDatabase<TerrainDef>.GetNamed(terrainTile.defName, false);
                        if (terrainDef != null && includeCost) {
                            long started = CostTiming.Now;
                            try {
                                terrainTile.legacyCost = terrainDef.LegacyMarketCost();
                                legacyTotalCost += terrainTile.legacyCost;
                            } catch (Exception) { }
                            started = CostTiming.AddLegacy(started);
                            try {
                                terrainTile.cost = terrainDef.AbstractMarketValue();
                            } catch (Exception) {
                                terrainTile.cost = terrainTile.legacyCost;
                            }
                            CostTiming.AddNew(started);
                            totalCost += terrainTile.cost;
                        }
                        terrainTilesCount++;
                    }
                }
            }
            itemsDensity = (float)itemsCount / (width * height);
            //Debug.Message("Recalculated blueprint stats. Processed {0} items and {1} tiles, got {2} cost", itemsCount, terrainTilesCount, totalCost);
        }

        //Sets weight and, if asked, both cost schemes of an item and of its contents. The new cost of a container (bookcase, minified thing,
        //sarcophagus) includes its contents, so cost caps see the whole value; scavengers take the contents out one by one (ScavengingProcessor).
        //The legacy cost keeps the old scheme: the tile itself only.
        //Since at this moment not all things are filtered yet, cost can't be calculated for some of them: those fall back to the legacy cost, or 0.
        private static void UpdateItemStats(ItemTile item, ThingDef thingDef, ThingDef stuffDef, bool includeCost) {
            if (includeCost) {
                long started = CostTiming.Now;
                item.legacyCost = 0;
                try {
                    item.legacyCost = thingDef.LegacyMarketCost(stuffDef) * item.stackCount;
                } catch (Exception) { }
                started = CostTiming.AddLegacy(started);

                try {
                    item.cost = thingDef.AbstractMarketValue(stuffDef) * item.stackCount;
                } catch (Exception) {
                    item.cost = item.legacyCost;
                }
                CostTiming.AddNew(started);
            }

            if (item.defName.Contains("Wall")) item.weight = 5; //walls are on some reason always have weight of 1, which is obviously not correct. The game itself does not need walls weight, so that's ok, but we need.

            item.weight = thingDef.ThingWeight(stuffDef);

            if (item.stackCount != 0) item.weight *= item.stackCount;
            if (item.weight == 0) {
                if (item.stackCount != 0) {
                    item.weight = 0.5f * item.stackCount;
                } else {
                    item.weight = 1.0f;
                }
            }

            if (item.innerItems == null) return;
            foreach (ItemTile inner in item.innerItems) {
                ThingDef innerDef = DefDatabase<ThingDef>.GetNamed(inner.defName, false);
                if (innerDef == null || innerDef.race != null) { //corpses, pawns (cryptosleep) and missing defs: no value, they are not loot
                    inner.cost = 0;
                    inner.legacyCost = 0;
                    continue;
                }
                ThingDef innerStuff = (inner.stuffDef != null) ? DefDatabase<ThingDef>.GetNamed(inner.stuffDef, false) : null;
                UpdateItemStats(inner, innerDef, innerStuff, includeCost);
                if (includeCost) item.cost += inner.cost;
            }
        }

        //Top tiles by how much the new cost scheme differs from the legacy one, for the comparison log
        public string CostComparisonSummary(int topCount = 5) {
            var tiles = new List<Tile>();
            for (int x = 0; x < width; x++) {
                for (int z = 0; z < height; z++) {
                    if (itemsMap[x, z] != null) tiles.AddRange(itemsMap[x, z]);
                    if (terrainMap[x, z] != null) tiles.Add(terrainMap[x, z]);
                }
            }

            var builder = new StringBuilder();
            builder.AppendFormat("cost {0:F0}, legacy {1:F0} (x{2:F2}). Largest differences:", totalCost, legacyTotalCost, legacyTotalCost > 0 ? totalCost / legacyTotalCost : 0);
            foreach (Tile tile in tiles.OrderByDescending(t => Math.Abs(t.cost - t.legacyCost)).Take(topCount)) {
                ItemTile itemTile = tile as ItemTile;
                string stuff = itemTile?.stuffDef != null ? " (" + itemTile.stuffDef + ")" : "";
                string count = itemTile != null && itemTile.stackCount > 1 ? " x" + itemTile.stackCount : "";
                string contents = itemTile?.innerItems != null ? " +" + itemTile.innerItems.Count + " inside" : "";
                builder.AppendFormat(" {0}{1}{2}{3}: {4:F0} vs {5:F0};", tile.defName, stuff, count, contents, tile.cost, tile.legacyCost);
            }
            return builder.ToString();
        }

        // -------------- walls processing ------------
        //Wall map management: wall map is used to determine which rooms are opened and which are not. Similar to game engine regions, but much simplier and smaller.
        public void MarkRoomAsOpenedAt(int posX, int posZ) {
            int value = wallMap[posX, posZ];
            if (value < 2) return; //do not re-mark walls, uncalculated and already marked

            for (int x = 0; x < width; x++) {
                for (int z = 0; z < height; z++) {
                    if (wallMap[x, z] == value) wallMap[x, z] = 1;
                }
            }
        }

        //This method affects wall map only, it does not actually remove a wall.
        //Not an issue (FIXES_QUEUE L1): the full map scan below runs only when the removal merges different rooms, and each merge
        //retires at least one room number, so all calls together do at most (rooms count + rooms created here - 1) scans.
        public void RemoveWall(int posX, int posZ) {
            if (posX < 0 || posZ < 0 || posX >= width || posZ >= height) return;
            if (wallMap[posX, posZ] != -1) return; //alerady no wall there
            int? newValue = null;

            //determine new value. if we're on the edge, the room will be opened
            if (posX == 0 || posX == width - 1 || posZ == 0 || posZ == height - 1) {
                newValue = 1;
            }

            List<int> adjacentRoomNumbers = new List<int>();
            if (posX > 0) adjacentRoomNumbers.Add(wallMap[posX - 1, posZ]);
            if (posX < width - 1) adjacentRoomNumbers.Add(wallMap[posX + 1, posZ]);
            if (posZ > 0) adjacentRoomNumbers.Add(wallMap[posX, posZ - 1]);
            if (posZ < height - 1) adjacentRoomNumbers.Add(wallMap[posX, posZ + 1]);
            adjacentRoomNumbers.RemoveAll((int room) => room == -1);
            List<int> distinct = adjacentRoomNumbers.Distinct().ToList();
            // Debug.Message("Combining rooms: {0}", distinct);
            if (newValue == null && distinct.Count > 0) {
                if (distinct.Contains(1)) {
                    distinct.Remove(1);
                    newValue = 1;
                } else {
                    newValue = distinct.Pop();
                }
            }

            //cell surrounded by walls becomes a new room, so destroying the rest of a thick wall later still connects it
            if (newValue == null) {
                newValue = roomsCount;
                roomsCount++;
                roomAreas?.Add(1);
            }
            wallMap[posX, posZ] = newValue.Value;

            if (distinct.Count > 0) {
                for (int x = 0; x < width; x++) {
                    for (int z = 0; z < height; z++) {
                        if (distinct.Contains(wallMap[x, z])) wallMap[x, z] = newValue ?? 1;
                    }
                }
            }
        }

        //This method enumerates closed areas in the blueprint base on a prefilled wallmap array. WallMap at this moment should have zeros on unprocessed tiles and "-1"s on tiles with walls.
        //After the method is completed, wallmap will still have -1s for walls, but all other cells will contain room indices starting from 1 (outside)
        public void FindRooms() {
            int currentRoomIndex = 1;
            roomAreas = new List<int>() { 0 }; //we don't have a room indexed zero, so place it here as if it were processed already

            void TraverseCells(List<IntVec3> points) { //BFS
                int area = 0;
                List<IntVec3> nextLevel = new List<IntVec3>();
                foreach (IntVec3 point in points) {
                    if (point.x < 0 || point.z < 0 || point.x >= width || point.z >= height) continue; //ignore out of bounds
                    if (wallMap[point.x, point.z] != 0) continue; //ignore processed points

                    wallMap[point.x, point.z] = currentRoomIndex;
                    area++;

                    nextLevel.Add(new IntVec3(point.x - 1, 0, point.z));
                    nextLevel.Add(new IntVec3(point.x + 1, 0, point.z));
                    nextLevel.Add(new IntVec3(point.x, 0, point.z - 1));
                    nextLevel.Add(new IntVec3(point.x, 0, point.z + 1));
                }

                if (roomAreas.Count == currentRoomIndex) {
                    roomAreas.Add(0);
                }

                roomAreas[currentRoomIndex] += area;

                if (nextLevel.Count > 0) {
                    TraverseCells(nextLevel);
                }
            }

            //mark edge cells as alreay opened
            for (int z = 0; z < height; z++) {
                wallMap[0, z] = 1;
                wallMap[width - 1, z] = 1;
            }
            for (int x = 0; x < width; x++) {
                wallMap[x, 0] = 1;
                wallMap[x, height - 1] = 1;
            }

            //For each unmarked point we can interpret our map as a tree with root at current point and branches going to four directions. For this tree (with removed duplicate nodes) we can implement BFS traversing.
            for (int z = 0; z < height; z++) {
                for (int x = 0; x < width; x++) {
                    if (wallMap[x, z] == 0) {
                        TraverseCells(new List<IntVec3>() { new IntVec3(x, 0, z) });
                        currentRoomIndex += 1;
                    }
                }
            }

            roomsCount = currentRoomIndex;
            //Debug.Message("Traverse completed. Found {0} rooms", currentRoomIndex);
        }

        public Blueprint RandomPartCenteredAtRoom(IntVec3 size) {
            //Debug.Message("selecting random part of size {0} centered at room", size.x);
            if (roomsCount == 0) {
                FindRooms();
            }
            //Debug.Message("Processed rooms, found {0}", roomsCount);

            if (roomsCount < 3) {
                //no rooms => selecting arbitrary piece of the blueprint
                //Debug.PrintIntMap(wallMap, delta: +1);
                return Part(new IntVec3(Rand.Range(size.x, width - size.x), 0, Rand.Range(size.z, height - size.z)), size);
            }


            int selectedRoomIndex = 0;
            selectedRoomIndex = Rand.Range(2, roomsCount);
            //Debug.Message("Selected room {0}", selectedRoomIndex);

            int minX = width; int maxX = 0;
            int minZ = height; int maxZ = 0;

            for (int x = 0; x < width; x ++) {
                for (int z = 0; z < height; z ++) {
                    if (wallMap[x, z] == selectedRoomIndex) {
                        if (x > maxX) maxX = x;
                        if (x < minX) minX = x;
                        if (z > maxZ) maxZ = z;
                        if (z < minZ) minZ = z;
                    }
                }
            }
            //Debug.Message("Framed room by {0}..{1}, {2}..{3}", minX, maxX, minZ, maxZ);

            IntVec3 center = new CellRect(minX, minZ, maxX - minX, maxZ - minZ).RandomCell;

            //Debug.Message("Selected random cell {0}, {1}", center.x, center.z);

            return Part(center, size);
        }

        //WARNING: Original blueprint becomes non-usable after slicing out it's part.
        public Blueprint Part(IntVec3 location, IntVec3 size) {

            if (width <= size.x && height <= size.z) return this; //piece size is larger than the blueprint itself => don't alter blueprint

            int centerX = location.x;
            int centerZ = location.z;

            //max bounds are exclusive. min bounds are clamped inside the blueprint: the center can be off it when the blueprint is
            //smaller than the piece in one dimension only (see RandomPartCenteredAtRoom), and that used to give a negative size.
            int minX = Math.Max(0, Math.Min(centerX - size.x / 2, width - 1));
            int maxX = Math.Min(width, minX + size.x);
            int minZ = Math.Max(0, Math.Min(centerZ - size.z / 2, height - 1));
            int maxZ = Math.Min(height, minZ + size.z);

            //Debug.Message("Cutting area of size {0}x{1}. Frame is {2}..{3}, {4}..{5}", size.x, size.z, minX, maxX, minZ, maxZ);

            int relocatedTilesCount = 0;


            Blueprint result = new Blueprint(originX, originZ, maxX - minX, maxZ - minZ, version);
            for (int z = minZ; z < maxZ; z++) {
                for (int x = minX; x < maxX; x++) {
                    var relativeLocation = new IntVec3(x - minX, 0, z - minZ);
                    result.roofMap[x - minX, z - minZ] = roofMap[x, z];
                    result.terrainMap[x - minX, z - minZ] = terrainMap[x, z];
                    result.itemsMap[x - minX, z - minZ] = itemsMap[x, z];

                    if (wallMap[x, z] == -1) result.wallMap[x - minX, z - minZ] = -1;
                    else result.wallMap[x - minX, z - minZ] = 0;

                    if (result.itemsMap[x - minX, z - minZ] != null) {
                        foreach (Tile t in result.itemsMap[x - minX, z - minZ]) {
                            t.location = relativeLocation;
                            relocatedTilesCount++;
                        }
                    }

                    if (result.terrainMap[x - minX, z - minZ] != null) result.terrainMap[x - minX, z - minZ].location = relativeLocation;
                }
            }

            result.snapshotYear = snapshotYear;
            //Debug.Message("Cutting completed, relocated {0} tiles", relocatedTilesCount);
            return result;
        }
    }
}
