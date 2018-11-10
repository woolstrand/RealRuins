using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using System.Xml;
using System.IO;
using RimWorld.BaseGen;
using Verse.AI.Group;

namespace RealRuins
{

    //stores information about terrain in the blueprint
    class Tile {
        public string defName;
        public float cost = 0.0f; //populated later
        public float weight = 0.0f;
        public IntVec3 location;
    }

    class TerrainTile: Tile
    {

        public TerrainTile(XmlNode node)
        {
            defName = node.Attributes["def"].Value;
        }
    }

    //stores information about item in the blueprint
    class ItemTile: Tile
    {
        public string stuffDef;
        public int stackCount;
        public int rot;
        public bool isWall = false; //is a wall or something as tough and dense as a wall. actually this flag determines if this item can be replaced with a wall if it's impossible to use the original.
        public bool isDoor = false;

        static public ItemTile CollapsedWallItemTile() {
            return CollapsedWallItemTile(new IntVec3(0, 0, 0));
        }

        public static ItemTile CollapsedWallItemTile(IntVec3 location) {
            ItemTile tile = new ItemTile {
                defName = ThingDefOf.CollapsedRocks.defName,
                isDoor = false,
                isWall = true,
                stackCount = 1,
                rot = 0,
                cost = 0,
                weight = 1.0f,
                location = location
            };

            return tile;
        }

        static public ItemTile DefaultDoorItemTile() {
            return DefaultDoorItemTile(new IntVec3(0, 0, 0));
        }

        static public ItemTile DefaultDoorItemTile(IntVec3 location) {
                ItemTile tile = new ItemTile();

            tile.defName = ThingDefOf.Door.defName;
            tile.stuffDef = ThingDefOf.WoodLog.defName;
            tile.isDoor = true;
            tile.isWall = false;
            tile.location = location;
            tile.stackCount = 1;
            tile.rot = 0;
            tile.cost = 0;
            tile.weight = 1.0f;

            return tile;
        }

        public ItemTile() {
        }

        public ItemTile(XmlNode node)
        {
            defName = node.Attributes["def"].Value;
            XmlAttribute stuffDefAttribute = node.Attributes["stuffDef"];
            if (stuffDefAttribute != null) {
                stuffDef = stuffDefAttribute.Value;
            }

            XmlAttribute stackCountAttribute = node.Attributes["stackCount"];
            if (stackCountAttribute != null) {
                stackCount = int.Parse(s: stackCountAttribute.Value);
            } else {
                stackCount = 1;
            }

            XmlAttribute doorAttribute = node.Attributes["isDoor"];
            if (doorAttribute != null || defName.ToLower().Contains("door")) {
                isDoor = true;
            }

            XmlAttribute wallAttribute = node.Attributes["actsAsWall"];
            if (wallAttribute != null || defName.ToLower().Contains("wall") || defName.Equals("Cooler") || defName.Equals("Vent")) { //compatibility
                isWall = true;
            }

            XmlAttribute rotAttribute = node.Attributes["rot"];
            if (rotAttribute != null) {
                rot = int.Parse(s: rotAttribute.Value);
            } else {
                rot = 0;
            }

        }

    }

    class RuinsScatterer {

        private static bool[,] cellUsed;

        public static void PrepareCellUsageFor(Map map) {
            cellUsed = new bool[map.Size.x, map.Size.z];
        }

        public static void FinalizeCellUsage() {
            cellUsed = null;
        }

        static private int totalWorkTime = 0;
        private float totalCost = 0;
        private ScatterOptions options;

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

        private int RandomInBounds(int span, int margin, int jitter) {
            if (span > (margin + jitter) * 2) {
                return Rand.Range(margin + jitter, span - margin - jitter);
            } else {
                return Rand.Range(span / 2 - jitter, span / 2 + jitter);
            }
        }

        private IntVec3 SelectRandomCenterInBounds(int width, int height, int radius, int jitter) {
            return new IntVec3(RandomInBounds(width, radius, jitter), 0, RandomInBounds(height, radius, jitter));
        }

        private double Distance(int x, int z, IntVec3 center) {
            return Math.Sqrt(Math.Pow(center.x - x, 2) + Math.Pow(center.z - z, 2));
        }

        private bool PointInsideCircle(int x, int z, IntVec3 center, int radius) {
            return Math.Pow(center.x - x, 2) + Math.Pow(center.z - z, 2) < Math.Pow(radius, 2);
        }

        //Calculates cost of item made of stuff, or default cost if stuff is null
        //Golden wall is a [Wall] made of [Gold], golden tile is a [GoldenTile] made of default material
        private float ThingComponentsMarketCost(BuildableDef buildable, ThingDef stuffDef = null) {
            float num = 0f;
            //Debug.active = false;
            //Debug.Message("Requesting cost for item: {0} made of {1}", buildable, stuffDef);

            if (buildable == null) return 0; //can be for missing subcomponents, i.e. bed from alpha-poly. Bed does exist, but alpha poly does not.

            if (buildable.costList != null) {
            //    Debug.Message("Going through costList");
                foreach (ThingDefCountClass cost in buildable.costList) {
                    num += (float)cost.count * ThingComponentsMarketCost(cost.thingDef);
                }
            }

            if (buildable.costStuffCount > 0) {
            //    Debug.Message("Using stuffcount");
                if (stuffDef == null) {
                    stuffDef = GenStuff.DefaultStuffFor(buildable);
                }
            //    Debug.Message("Stuffdef is now {0}", stuffDef);

                if (stuffDef != null) {
                    num += (float)buildable.costStuffCount * stuffDef.BaseMarketValue * (1.0f / stuffDef.VolumePerUnit);
                }
            }

            if (num == 0) {
                if (buildable is ThingDef) {
                    if (((ThingDef)buildable).recipeMaker == null) {
                //        Debug.Message("Trying to get base market value");
                //        Debug.active = true;
                        return ((ThingDef)buildable).BaseMarketValue; //on some reason base market value is calculated wrong for, say, golden walls
                    }
                }
            }
            //Debug.active = true;
            return num;
        }


        Map map;
        IntVec3 targetPoint;

        int blueprintWidth;
        int blueprintHeight;

        int minX, maxX, minZ, maxZ; //boundaries of used part of the blueprint
        int mapOriginX, mapOriginZ; //target coordinates for minX, minZ on the map

        int referenceRadius;
        int referenceRadiusJitter;

        TerrainTile[,] terrainMap;
        bool[,] roofMap;
        int[,] wallMap; //map of walls to create room-based deterioration. 0 means "not traversed", -1 means "wall or similar", otherwise it's a room number
        List<ItemTile>[,] itemsMap;

        //base deterioration chance mask.
        //deterioration change also depends on material and cost, but is always based on base chance
        float[,] terrainIntegrity; //integrity of floor tiles
        float[,] itemsIntegrity; //base integrity of walls, roofs and items

        bool canHaveFood = false;

        float deteriorationDegree = 0;
        float scavengersActivity = 0; //depends on how far tile is from villages
        float elapsedTime = 0; //ruins age

        //Wall map management: wall map is used to determine which rooms are opened and which are not. Similar to game engine regions, but much simplier and smaller.
        void MarkRoomAsOpenedAt(int posX, int posZ) {
            int value = wallMap[posX, posZ];
            //Debug.Message("Marking room {0} as opened", value);
            if (value < 2) return; //do not re-mark walls, uncalculated and already marked

            for (int x = minX; x < maxX; x++) {
                for (int z = minZ; z < maxZ; z++) {
                    if (wallMap[x, z] == value) wallMap[x, z] = -1;
                }
            }
        }

        void RemoveWall(int posX, int posZ) {

//            Debug.Message("removing wall at {0}, {1}", posX, posZ);
            if (wallMap[posX, posZ] != -1) return; //alerady no wall there
            int? newValue = null;

            //determine new value. if we're on the edge, the room will be opened
            if (posX == minX || posX == maxX - 1 || posZ == minZ || posZ == maxZ - 1) {
                newValue = 1;
//                Debug.Message("Room is opening to the wild");
            }

            List<int> adjacentRoomNumbers = new List<int>();
            if (posX > 0) adjacentRoomNumbers.Add(wallMap[posX - 1, posZ]);
            if (posX < maxX - 1) adjacentRoomNumbers.Add(wallMap[posX + 1, posZ]);
            if (posZ > 0) adjacentRoomNumbers.Add(wallMap[posX, posZ - 1]);
            if (posZ < maxZ - 1) adjacentRoomNumbers.Add(wallMap[posX, posZ + 1]);
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

            if (distinct.Count > 0) {
                for (int x = minX; x < maxX; x++) {
                    for (int z = minZ; z < maxZ; z++) {
                        if (distinct.Contains(wallMap[x, z])) wallMap[x, z] = newValue ?? 1;
                    }
                }
            }

        }


        private bool LoadRandomXMLSnapshot() {

            int attemptNumber = 0;
            bool result = false;

            while (attemptNumber < 10 && result != true) {

                string snapshotName = SnapshotStoreManager.Instance.RandomSnapshotFilename();
                if (snapshotName == null) {
                    return false;
                }

                result = DoSanityCheckAndLoad(snapshotName);

                if (!result && options.deleteLowQuality) { //remove bad snapshots
                    Debug.Message("DELETING low quality file");
                    File.Delete(snapshotName);
                    string deflatedName = snapshotName + ".xml";
                    if (!File.Exists(deflatedName)) {
                        File.Delete(deflatedName);
                    }
                }

            }

            return result;
        }
        
        private bool DoSanityCheckAndLoad(string snapshotName) {

        //Create the XmlDocument. 
            Debug.Message("Did select file {0} for loading", snapshotName);

            string deflatedName = snapshotName;
            if (Path.GetExtension(snapshotName).Equals(".bp")) {

                Debug.Message("Unpacking...");
                deflatedName = snapshotName + ".xml";
                if (!File.Exists(deflatedName)) {
                    string data = Compressor.UnzipFile(snapshotName);
                    File.WriteAllText(deflatedName, data);
                }
            }
            
            XmlDocument snapshot = new XmlDocument();
            snapshot.Load(deflatedName);

            XmlNodeList elemList = snapshot.GetElementsByTagName("cell");
            blueprintWidth = int.Parse(snapshot.FirstChild.Attributes["width"].Value);
            blueprintHeight = int.Parse(snapshot.FirstChild.Attributes["height"].Value);

            if (blueprintHeight > 350 || blueprintWidth > 350 || blueprintHeight < 10 || blueprintWidth < 10) {
                Debug.Message("SKIPPED due to unacceptable linear dimensions", snapshotName);
                return false; //wrong size. too small or too large
            }

            if (blueprintHeight * blueprintWidth < options.minimumSizeRequired) {
                Debug.Message("SKIPPED due to area vs options", snapshotName);
                return false;
            }

            terrainMap = new TerrainTile[blueprintWidth, blueprintHeight];
            roofMap = new bool[blueprintWidth, blueprintHeight];
            itemsMap = new List<ItemTile>[blueprintWidth, blueprintHeight];

            wallMap = new int[blueprintWidth, blueprintHeight];


            //base deterioration chance mask. is used to create freeform deterioration which is much more fun than just circular
            //deterioration chance may depends on material and cost, but is always based on base chance
            terrainIntegrity = new float[blueprintWidth, blueprintHeight]; //integrity of floor tiles
            itemsIntegrity = new float[blueprintWidth, blueprintHeight]; //base integrity of walls, roofs and items


            //should food ever be spawned for this ruins
            canHaveFood = Rand.Chance((1.0f - deteriorationDegree) / 4);
            int itemNodes = 0;
            int terrainNodes = 0;

            foreach (XmlNode cellNode in elemList) {
                int x = int.Parse(cellNode.Attributes["x"].Value);
                int z = int.Parse(cellNode.Attributes["z"].Value);
                itemsMap[x, z] = new List<ItemTile>();

                foreach (XmlNode cellElement in cellNode.ChildNodes) {
                    if (cellElement.Name.Equals("terrain")) {
                        terrainNodes++;
                        TerrainTile terrain = new TerrainTile(cellElement);
                        terrain.location = new IntVec3(x, 0, z);
                        terrainMap[x, z] = terrain;

                    } else if (cellElement.Name.Equals("item")) {
                        itemNodes++;
                        ItemTile tile = new ItemTile(cellElement);

                        ThingDef thingDef = DefDatabase<ThingDef>.GetNamed(tile.defName, false);
                        if (thingDef != null) {
                            if (thingDef.fillPercent == 1.0f) {
                                wallMap[x, z] = -1; //place wall
                            }
                            tile.location = new IntVec3(x, 0, z);
                            itemsMap[x, z].Add(tile); //save item only if it's def can be loaded.
                        } else {
                            if (tile.isDoor) { //replacing unavailable door with abstract default door
                                tile.defName = ThingDefOf.Door.defName;
                            } else if (tile.isWall || tile.defName.ToLower().Contains("wall")) { //replacing unavailable impassable 100% filling block (which was likely a wall) with a wall
                                tile.defName = ThingDefOf.CollapsedRocks.defName;
                            }
                        }

                    } else if (cellElement.Name.Equals("roof")) {
                        roofMap[x, z] = true;
                    }
                }
            }

            float itemsDensity = (float) (itemNodes + terrainNodes) / (float) (blueprintHeight * blueprintWidth);
            Debug.Message("Items density: {0}", itemsDensity);
            if (itemsDensity < options.minimumDensityRequired) {
                Debug.Message("SKIPPED due to low density");
                return false; //too empty: less than 1% of area is covered with player constructed items
            }

            return true;
        }

        private void BlurIntegrityMap(float[,] map, int stepsCount) {
            float[,] delta = new float[blueprintWidth, blueprintHeight]; //delta integrity for making blur
                                                                         // - then blur the map to create gradient deterioration around the intact area.
            for (int steps = 0; steps < stepsCount; steps++) { //terrain map
                for (int x = minX + 1; x < maxX - 1; x++) {
                    for (int z = minZ + 1; z < maxZ - 1; z++) {
                        delta[x, z] = (map[x - 1, z - 1] + map[x, z - 1] + map[x + 1, z - 1] +
                            map[x - 1, z] + map[x, z] + map[x + 1, z] +
                            map[x - 1, z + 1] + map[x, z + 1] + map[x + 1, z + 1]) / 9.0f;
                    }
                }
                for (int x = minX + 1; x < maxX - 1; x++) {
                    for (int z = minZ + 1; z < maxZ - 1; z++) {
                        if (map[x, z] < 1) {
                            map[x, z] = delta[x, z] * (0.9f + Rand.Value * 0.1f);
                        }
                    }
                }
            }

        }

        private void FallbackIntegrityMapConstructor(IntVec3 center, int radius) {
            if (center.x == 0 && center.z == 0) {
                if (radius * 2 > blueprintWidth) { radius = blueprintWidth / 2 - 1; }
                if (radius * 2 > blueprintHeight) { radius = blueprintHeight / 2 - 1; }
                center = new IntVec3(Rand.Range(radius, blueprintWidth - radius), 0, Rand.Range(radius, blueprintHeight - radius));
            }

            minX = center.x - radius; maxX = center.x + radius;
            minZ = center.z - radius; maxZ = center.z + radius;

            if (minX < 0) minX = 0;
            if (minZ < 0) minZ = 0;
            if (maxX >= blueprintWidth - 1) maxX = blueprintWidth - 1;
            if (maxZ >= blueprintHeight - 1) maxZ = blueprintHeight - 1;

            //Debug.Message("Fallback boudaries are: {0}-{1}, {2}-{3} with blueprint size of {4}x{5}", minX, maxX, minZ, maxZ, blueprintWidth, blueprintHeight);

            mapOriginX = targetPoint.x - (maxX - minX) / 2;
            mapOriginZ = targetPoint.z - (maxZ - minZ) / 2;

            if (mapOriginX < 10) mapOriginX = 10;
            if (mapOriginZ < 10) mapOriginZ = 10;

            if (mapOriginX + (maxX - minX) > map.info.Size.x) mapOriginX = map.info.Size.x - 10 - (maxX - minX);
            if (mapOriginZ + (maxZ - minZ) > map.info.Size.z) mapOriginZ = map.info.Size.z - 10 - (maxZ - minZ);

            for (int x = minX; x < maxX; x++) {
                for (int z = minZ; z < maxZ; z++) {
                    int sqrDistance = (x - center.x) * (x - center.x) + (z - center.z) * (z - center.z);
                    if (sqrDistance < (radius / 2) * (radius / 2)) {
                        terrainIntegrity[x, z] = Rand.Value * 0.4f + 0.8f;
                        itemsIntegrity[x, z] = Rand.Value * 0.4f + 0.8f;
                    } else if (sqrDistance < (radius * radius)) { //edge
                        terrainIntegrity[x, z] = Rand.Value * 0.2f + 0.8f;
                        itemsIntegrity[x, z] = Rand.Value * 0.2f + 0.7f;
                    }
                }
            }

            BlurIntegrityMap(terrainIntegrity, 10);
            BlurIntegrityMap(itemsIntegrity, 7);
        }

        private void UntouchedIntegrityMapConstructor() {
            minX = 0;
            minZ = 0;
            maxX = blueprintWidth - 1;
            maxZ = blueprintHeight - 1;
            
            mapOriginX = targetPoint.x - (maxX - minX) / 2;
            mapOriginZ = targetPoint.z - (maxZ - minZ) / 2;

            if (mapOriginX < 10) mapOriginX = 10;
            if (mapOriginZ < 10) mapOriginZ = 10;

            if (mapOriginX + (maxX - minX) > map.info.Size.x) mapOriginX = map.info.Size.x - 10 - (maxX - minX);
            if (mapOriginZ + (maxZ - minZ) > map.info.Size.z) mapOriginZ = map.info.Size.z - 10 - (maxZ - minZ);

            for (int x = minX; x < maxX; x++) {
                for (int z = minZ; z < maxZ; z++) {
                    terrainIntegrity[x, z] = 1.0f - options.deteriorationMultiplier;
                    itemsIntegrity[x, z] = 1.0f - options.deteriorationMultiplier;
                }
            }

        }

        private void FindRoomsAndConstructIntegrityMaps() {
            int currentRoomIndex = 1;
            List<int> roomAreas = new List<int>() { 0 }; //we don't have a room indexed zero.

            void TraverseCells(List<IntVec3> points) {
                int area = 0;
                List<IntVec3> nextLevel = new List<IntVec3>();
                foreach (IntVec3 point in points) {
                    if (point.x < 0 || point.z < 0 || point.x >= blueprintWidth || point.z >= blueprintHeight) continue; //ignore out of bounds
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

            //Debug.Message("Traversing map");

            //For each unmarked point we can interpret our map as a tree with root at current point and branches going to four directions. For this tree (with removed duplicate nodes) we can implement BFS traversing.
            for (int z = 0; z < blueprintHeight; z++) {
                for (int x = 0; x < blueprintWidth; x++) {
                    if (wallMap[x, z] == 0) {
                        TraverseCells(new List<IntVec3>() { new IntVec3(x, 0, z) });
                        currentRoomIndex += 1;
                    }
                }
            }

            //Debug.Message("Completed. Found {0} rooms", currentRoomIndex);

            if (currentRoomIndex == 1) { //no new rooms were added => blueprint does not have regular rooms or rooms were formed with use of some missing components or materials
                //fallback plan: construct old-fashioned circular deterioration map from a some random point
                FallbackIntegrityMapConstructor(new IntVec3(0, 0, 0), referenceRadius);
                return;
            } else {
                //1. select one random room with suitable area
                List<int> suitableRoomIndices = new List<int>();
                for (int i = 2; i < roomAreas.Count; i++) { //0 is not used, 1 is default "outside" room 
                    if (roomAreas[i] > 4 && roomAreas[i] < 200) {
                        suitableRoomIndices.Add(i);
                    }
                }

                //Debug.Message("Found {0} suitable rooms", suitableRoomIndices.Count);

                int selectedRoomIndex = 0;
                if (suitableRoomIndices.Count == 0) {
                    //1x. no suitable rooms: fallback plan
                    FallbackIntegrityMapConstructor(new IntVec3(0, 0, 0), referenceRadius);
                    return;
                } else {
                    selectedRoomIndex = suitableRoomIndices[Rand.Range(0, suitableRoomIndices.Count)];
                }

               // Debug.Message("Selected suitable room: {0}", selectedRoomIndex);
                
                //2. select a point within this room
                List<IntVec3> suitablePoints = new List<IntVec3>();
                for (int z = 0; z < blueprintHeight; z++) {
                    for (int x = 0; x < blueprintWidth; x++) {
                        if (wallMap[x, z] == selectedRoomIndex) {
                            suitablePoints.Add(new IntVec3(x, 0, z));
                        }
                    }
                } //suitable points count is at least 5
                IntVec3 center = suitablePoints[suitablePoints.Count / 2];

                //Debug.Message("Selected center");

                //3. draw a random circle around that room
                int radius = Rand.Range(referenceRadius - referenceRadiusJitter, referenceRadius + referenceRadiusJitter);
                if (radius * 2 + 1 > blueprintWidth - 4) { radius = (blueprintWidth - 4) / 2 - 1; }
                if (radius * 2 + 1 > blueprintHeight - 4) { radius = (blueprintHeight - 4) / 2 - 1; }
                if (radius > center.x - 1) { center.x = radius + 1; } //shift central point if it's too close to blueprint edge
                if (radius > center.y - 1) { center.y = radius + 1; }
                if (center.x + radius > blueprintWidth - 2) { center.x = blueprintWidth - 2 - radius; }
                if (center.y + radius > blueprintHeight - 2) { center.y = blueprintHeight - 2 - radius; }

                minX = center.x - radius; maxX = center.x + radius;
                minZ = center.z - radius; maxZ = center.z + radius;

                //Ok, too bad with math at 1:00 am
                if (minX < 0) minX = 0;
                if (minZ < 0) minZ = 0;
                if (maxX >= blueprintWidth - 1) maxX = blueprintWidth - 1;
                if (maxZ >= blueprintHeight - 1) maxZ = blueprintHeight - 1;

                //Debug.Message("Regular boudaries are: {0}-{1}, {2}-{3} with blueprint size of {4}x{5}", minX, maxX, minZ, maxZ, blueprintWidth, blueprintHeight);

                mapOriginX = targetPoint.x - (maxX - minX) / 2;
                mapOriginZ = targetPoint.z - (maxZ - minZ) / 2;

                if (mapOriginX < 10) mapOriginX = 10;
                if (mapOriginZ < 10) mapOriginZ = 10;

                if (mapOriginX + (maxX - minX) > map.info.Size.x) mapOriginX = map.info.Size.x - 10 - (maxX - minX);
                if (mapOriginZ + (maxZ - minZ) > map.info.Size.z) mapOriginZ = map.info.Size.z - 10 - (maxZ - minZ);


                //Debug.Message("Checking rooms intersection with border");

                //4. enumerate all rooms in the circle
                //5. enumerate all rooms intersecting the circle outline
                List<int> allRooms = new List<int>();
                List<int> openRooms = new List<int>();
                for (int x = minX; x < maxX; x++) {
                    for (int z = minZ; z < maxZ; z++) {
                        int roomIndex = wallMap[x, z];
                        int sqrDistance = (x - center.x) * (x - center.x) + (z - center.z) * (z - center.z);
                        if (sqrDistance < (radius - 1) * (radius - 1)) {
                            if (!allRooms.Contains(roomIndex)) allRooms.Add(roomIndex);
                        } else if (sqrDistance < (radius * radius)) { //intersecting edge => room is opened to the wild
                            if (!allRooms.Contains(roomIndex)) allRooms.Add(roomIndex);
                            if (!openRooms.Contains(roomIndex)) openRooms.Add(roomIndex);
                            MarkRoomAsOpenedAt(x, z);
                        }
                    }
                }

                //Debug.Message("Finished. Setting core integrity values");

                //if all rooms are intersecting circle outline do fallback plan (circular deterioration chance map)
                if (allRooms.Count == openRooms.Count) {
                    //fallback
                    FallbackIntegrityMapConstructor(center, radius);
                    return;
                } else {
                    //otherwise create the following deterioration map: 
                    List<int> closedRooms = allRooms.ListFullCopy();
                    foreach (int room in openRooms) {
                        closedRooms.Remove(room);
                    }

                    // - points in non-intersecting rooms: no destruction. 
                    for (int x = minX; x < maxX; x++) {
                        for (int z = minZ; z < maxZ; z++) {
                            int roomIndex = wallMap[x, z];
                            if (closedRooms.Contains(roomIndex)) {
                                terrainIntegrity[x, z] = 20.0f; //terrain integrity is used later to calculate boundary based on it.
                                itemsIntegrity[x, z] = 1.0f; //items integrity is just usual map
                            }
                        }
                    }

                    //Debug.Message("Expanding core by one cell");

                    // - then add one pixel width expansion to cover adjacent wall.
                    for (int x = minX + 1; x < maxX - 1; x++) {
                        for (int z = minZ + 1; z < maxZ - 1; z++) {
                            if (terrainIntegrity[x, z] == 0) {
                                float surroundings = terrainIntegrity[x + 1, z + 1] + terrainIntegrity[x, z + 1] + terrainIntegrity[x - 1, z + 1] +
                                    terrainIntegrity[x - 1, z] + terrainIntegrity[x + 1, z] +
                                    terrainIntegrity[x + 1, z - 1] + terrainIntegrity[x, z - 1] + terrainIntegrity[x - 1, z - 1];
                                if (surroundings >= 20.0f) { //core intact terrain has value of 20. newly generated intact terrain has value of 1, so >= 20 always means "core intact nearby". 
                                    terrainIntegrity[x, z] = 1.0f;
                                    itemsIntegrity[x, z] = 1.0f; //core values are the same
                                }
                            }
                        }
                    }

                   // Debug.Message("Normalizing");
                    // normalize terrain core values which were used for border generation
                    for (int x = minX; x < maxX; x++) {
                        for (int z = minZ; z < maxZ; z++) {
                            if (terrainIntegrity[x, z] > 1) {
                                terrainIntegrity[x, z] = 1;
                            }
                        }
                    }

                    //Debug.Message("Blurring");
                    BlurIntegrityMap(terrainIntegrity, 7);
                    BlurIntegrityMap(itemsIntegrity, 4);
                    //At this step we have integrity maps, so we can proceed further and simulate deterioration and scavenging
                    //Debug.Message("Finished");

                }
            }
        }

        private void ProcessItems() {
            //Each item should be checked if it can be placed or not. This should help preventing situations when simulated scavenging removes things which anyway won't be placed.
            //For each placed item it's cost should be calculated
            for (int x = minX; x < maxX; x++) {
                for (int z = minZ; z < maxZ; z++) {

                    if (itemsMap[x, z] == null) { itemsMap[x, z] = new List<ItemTile>(); }//to make thngs easier add empty list to every cell

                    List<ItemTile> items = itemsMap[x, z];
                    TerrainTile terrain = terrainMap[x, z];
                    TerrainDef terrainDef = null;

                    if (terrain != null) {
                        terrainDef = DefDatabase<TerrainDef>.GetNamed(terrain.defName, false);
                        if (terrainDef != null) {
                            terrain.cost = ThingComponentsMarketCost(terrainDef);
                            terrain.weight = 5.0f;
                        } else {
                            terrainMap[x, z] = null; //no terrain def means terrain can't be generated.
                            terrain = null;
                        }
                    }

                    TerrainDef existingTerrain = map.terrainGrid.TerrainAt(new IntVec3(x - minX + mapOriginX, 0, z - minZ + mapOriginZ));
                    if (terrainDef != null && terrainDef.terrainAffordanceNeeded != null && !existingTerrain.affordances.Contains(terrainDef.terrainAffordanceNeeded)) {
                        terrainDef = null;
                        terrainMap[x, z] = null; //erase terrain if underlying terrain can't support it.
                        roofMap[x, z] = false; //removing roof as well just in case
                    }

                    List<ItemTile> itemsToRemove = new List<ItemTile>();
                    foreach (ItemTile item in items) {
                        ThingDef thingDef = DefDatabase<ThingDef>.GetNamed(item.defName, false);

                        if (thingDef == null) {
                            itemsToRemove.Add(item);
                            continue;
                        }

                        if (options.wallsDoorsOnly) { //eleminate almost everything if "doors & walls" setting is active
                            if (!thingDef.IsDoor && !item.defName.ToLower().Contains("wall")) {
                                itemsToRemove.Add(item);
                                continue;
                            }
                        }

                        if (options.disableSpawnItems) { //eleminate haulables if corresponding tich is set
                            if (thingDef.EverHaulable) {
                                itemsToRemove.Add(item);
                                continue;
                            }
                        }

                        if (thingDef.defName.Contains("Animal")) {
                            itemsToRemove.Add(item);
                            continue; //remove animal sleeping beds and spots as wild animals tend to concentrate around.
                        }
                        if (thingDef.IsCorpse || thingDef.Equals(ThingDefOf.MinifiedThing)) { //now corpses are spawned bugged on some reason, so let's ignore. Also we can't handle minified things.
                            itemsToRemove.Add(item);
                            continue;
                        }

                        if (thingDef.terrainAffordanceNeeded != null) {
                            if (thingDef.EverTransmitsPower && options.shouldKeepDefencesAndPower) continue; //ignore affordances for power transmitters if we need to keep defence systems

                            if (terrainDef != null && terrainDef.terrainAffordanceNeeded != null && existingTerrain.affordances.Contains(terrainDef.terrainAffordanceNeeded)) {
                                if (!terrainDef.affordances.Contains(thingDef.terrainAffordanceNeeded)) { //if new terrain can be placed over existing terrain, checking if an item can be placed over a new terrain
                                    itemsToRemove.Add(item);
                                    roofMap[x, z] = false;
                                }
                            } else {
                                if (!existingTerrain.affordances.Contains(thingDef.terrainAffordanceNeeded)) {//otherwise checking if the item can be placed over the existing terrain.
                                    itemsToRemove.Add(item);
                                    roofMap[x, z] = false;
                                }
                            }
                        }
                    }

                    foreach (ItemTile item in itemsToRemove) {
                        if (item.isWall || item.isDoor) {
                            RemoveWall(item.location.x, item.location.z);
                        }

                        items.Remove(item);
                    }

                    //calculating cost for the remaining ones
                    foreach (ItemTile item in items) {
                        ThingDef thingDef = DefDatabase<ThingDef>.GetNamed(item.defName, false);
                        ThingDef stuffDef = (item.stuffDef != null)?DefDatabase<ThingDef>.GetNamed(item.stuffDef, false):null;

                        item.cost = ThingComponentsMarketCost(thingDef, stuffDef) * item.stackCount;
                        item.weight = thingDef.GetStatValueAbstract(StatDefOf.Mass, stuffDef) * item.stackCount;
                        if (item.weight == 0) {
                            item.weight = 0.5f * item.stackCount;
                        }

                        if (options.itemCostLimit > 0 && options.itemCostLimit < 1000) { //filter too expensive items. limit of 1000 means "no limit" actually
                            if (item.cost > options.itemCostLimit) {
                                itemsToRemove.Add(item);
                            }
                        }
                    }

                    foreach (ItemTile item in itemsToRemove) {
                        if (item.isWall || item.isDoor) {
                            RemoveWall(item.location.x, item.location.z);
                        }

                        items.Remove(item);
                    }
                }
            }
        }

        private void Deteriorate() {
            //remove everything according do integrity maps
            //since we have to maintain information about rooms integrity we have to update rooms map accordingly to all changes.
            
            for (int x = minX; x < maxX; x++) {
                for (int z = minZ; z < maxZ; z++) {
                    bool hadWall = false;
                    bool retainedWall = false;
                    List<ItemTile> items = itemsMap[x, z];

                    if (!Rand.Chance(terrainIntegrity[x, z])) {
                        terrainMap[x, z] = null;
                    }

                    if (terrainMap[x, z] == null) {
                        roofMap[x, z] = false; //no terrain - no roof. just is case. to be sure there won't be hanging roof islands floating in the air.
                    }

                    float itemsChance = itemsIntegrity[x, z] * (1.0f - deteriorationDegree);
                    List<ItemTile> newItems = new List<ItemTile>();
                    if (itemsChance > 0 && itemsChance < 1) {
                        roofMap[x, z] = Rand.Chance(itemsChance * 0.3f); //roof will most likely collapse everywhere

                        foreach (ItemTile item in items) {
                            if (options.shouldKeepDefencesAndPower && item.defName.ToLower().Contains("conduit")) { //do not deteriorate conduits if preparing
                                newItems.Add(item);
                                continue;
                            }
                            
                            if (item.isWall) hadWall = true;
                            if (Rand.Chance(itemsChance)) {
                                if (item.isWall) retainedWall = true;
                                newItems.Add(item);
                            }
                        }
                    }
                    if (itemsChance < 1) {
                        itemsMap[x, z] = newItems; //will be empty if chance is 0
                        if (itemsChance <= 0) RemoveWall(x, z);
                    }

                    if (hadWall && !retainedWall) RemoveWall(x, z);
                }
            }
        }

        private void RaidAndScavenge() {
            //remove the most precious things. smash some other things.
            //word is spread, so each next raid is more destructive than the previous ones
            //to make calculations a bit easier we're going to calculate value per cell, not per item.

            Debug.active = false;
            
            List<Tile> tilesByCost = new List<Tile>();

            for (int x = minX; x < maxX; x++) {
                for (int z = minZ; z < maxZ; z++) {
                    if (terrainMap[x, z] != null) {
                        tilesByCost.Add(terrainMap[x, z]);
                    }

                    foreach (ItemTile item in itemsMap[x, z]) {
                        tilesByCost.Add(item);
                    }
                }
            }

            tilesByCost.Sort(delegate(Tile t1, Tile t2) {
                return (t1.cost / t1.weight).CompareTo(t2.cost / t2.weight);
            });

            Debug.Message("Enumerated {0} items", tilesByCost.Count());

            int raidsCount = (int)(elapsedTime * scavengersActivity);
            if (options.scavengingMultiplier > 0.9f && raidsCount == 0) {
                raidsCount = 1; //at least one raid for each ruins in case of normal scavenging activity
            }
            int ruinsArea = (maxX - minX) * (maxZ - minZ);
            float baseRaidCapacity = Math.Max(10, (ruinsArea / 5) * scavengersActivity);

            Debug.Message("Performing {0} raids. Base capacity: {1}", raidsCount, baseRaidCapacity);

            for (int i = 0; i < raidsCount; i ++) {
                float raidCapacity = baseRaidCapacity * (float)Math.Pow(1.2, i);
                bool shouldStop = false;
                Debug.Message("Performing raid {0} of capacity {1}", i, raidCapacity);

                while (tilesByCost.Count > 0 && raidCapacity > 0 && !shouldStop) {
                    Tile topTile = tilesByCost.Pop();
                    String msg = string.Format("Inspecting tile \"{0}\" of cost {1} and weight {2}. ", topTile.defName, topTile.cost, topTile.weight);

                    if (topTile.cost < 15) {
                        shouldStop = true; //nothing to do here, everything valueable has already gone
                        msg += "Too cheap, stopping.";
                    } else {
                        if (Rand.Chance(0.999f)) { //there is still chance that even the most expensive thing will be left after raid. "Big momma said ya shouldn't touch that golden chair, it's cursed")
                            raidCapacity -= topTile.weight; //not counting weight for now.
                            if (topTile is TerrainTile) {
                                terrainMap[topTile.location.x, topTile.location.z] = null;
                                msg += "Terrain removed.";
                            } else if (topTile is ItemTile) {
                                ItemTile itemTile = topTile as ItemTile;
                                itemsMap[topTile.location.x, topTile.location.z].Remove((ItemTile)topTile);
                                if (itemTile.isDoor) { //if door is removed it should be replaced with another door, raiders are very polite and always replace expensive doors with cheaper ones.
                                    if (Rand.Chance(0.8f)) { //ok, not always.
                                        ItemTile replacementTile = ItemTile.DefaultDoorItemTile(itemTile.location);
                                        itemsMap[topTile.location.x, topTile.location.z].Add(replacementTile);
                                        msg += "Added " + replacementTile.defName + ", original ";
                                    } else {
                                        RemoveWall(itemTile.location.x, itemTile.location.z);
                                    }
                                }  else if (itemTile.isWall) { //if something like a wall removed (vent or aircon) you usually want to cover the hole to keep wall integrity
                                    ItemTile replacementTile = ItemTile.CollapsedWallItemTile(itemTile.location);
                                    itemsMap[topTile.location.x, topTile.location.z].Add(replacementTile);
                                    msg += "Added " + replacementTile.defName + ", original ";
                                }
                                msg += "Tile removed.";
                            }
                        }
                    }
                    Debug.Message(msg);
                }
            }

            //Check that there are no "hanging doors" left
            bool HasWallsIn(List<ItemTile> list) {
                foreach (ItemTile tile in list) {
                    if (tile.isWall) return true;
                }
                return false;
            }

            for (int z = minZ + 1; z < maxZ - 1; z++) {
                for (int x = minX + 1; x < maxX - 1; x++) {
                    ItemTile tileToRemove = null;
                    foreach (ItemTile tile in itemsMap[x, z]) {
                        if (tile.isDoor) { //check if a particular door tile has both two vertically adjacent walls (or similar) or two horizintally adjacent walls
                            if (!(HasWallsIn(itemsMap[x - 1, z]) && HasWallsIn(itemsMap[x + 1, z])) &&
                                !(HasWallsIn(itemsMap[x, z - 1]) && HasWallsIn(itemsMap[x, z + 1]))) {
                                tileToRemove = tile;
                                break;
                            }
                        }
                    }
                    if (tileToRemove != null) {
                        itemsMap[x, z].Remove(tileToRemove);
                        RemoveWall(x, z);
                    }
                }
            }

            Debug.active = true;

        }

        private void TransferBlueprint() {
            //Planting blueprint

            totalCost = 0;
            Faction faction = (Rand.Value > 0.35) ? Find.FactionManager.RandomEnemyFaction() : Find.FactionManager.OfAncients;
            
            //Debug.Message("Setting ruins faction to {0}", faction.Name);

            for (int z = minZ; z < maxZ; z++) {
                for (int x = minX; x < maxX; x++) {

                    //{loc.x, loc.z} should be mapped from blueprint's {floorcenter.x, floorcenter.z}
                    IntVec3 mapLocation = new IntVec3(x - minX + mapOriginX, 0, z - minZ + mapOriginZ);
                    if (!mapLocation.InBounds(map)) continue;
                    

                    //Check if thepoint is in allowed bounds of the map
                    if (!mapLocation.InBounds(map) || mapLocation.InNoBuildEdgeArea(map)) {
                        continue; //ignore invalid cells
                    }

                    //Construct terrain if some specific terrain stored in the blueprint
                    if (terrainMap[x, z] != null) {
                        TerrainDef blueprintTerrain = TerrainDef.Named(terrainMap[x, z].defName);
                        map.terrainGrid.SetTerrain(mapLocation, blueprintTerrain);
                        totalCost += terrainMap[x, z].cost;
                    }

                    /*if (roofMap[x, z] == true) {
                        map.roofGrid.SetRoof(mapLocation, RoofDefOf.RoofConstructed);
                    }*/ //no roof yet


                    //Add items
                    if (itemsMap[x, z] != null && itemsMap[x, z].Count > 0 && cellUsed[mapLocation.x, mapLocation.z] == false) {

                        bool cellIsAlreadyCleared = false;
                        
                        foreach (ItemTile itemTile in itemsMap[x, z]) {

                            ThingDef thingDef = DefDatabase<ThingDef>.GetNamed(itemTile.defName, false); //here thingDef is definitely not null because it was checked earlier

                            ThingDef stuffDef = null; //but stuff can still be null, or can be missing, so we have to check and use default just in case.
                            if (itemTile.stuffDef != null && thingDef.MadeFromStuff) { //some mods may alter thing and add stuff parameter to it. this will result in a bug on a vanilla, so need to double-check here
                                stuffDef = DefDatabase<ThingDef>.GetNamed(itemTile.stuffDef, false);
                            }

                            if (stuffDef == null) {
                                stuffDef = GenStuff.DefaultStuffFor(thingDef);
                            }

                            if (!cellIsAlreadyCleared) { //first item to be spawned should also clear place for itself. we can't do it beforehand because we don't know it it will be able and get a chance to be spawned.
                                bool forceCleaning = (wallMap[x, z] > 1) && Rand.Chance(0.9f);
                                    
                                if (!ClearCell(mapLocation, map, forceCleaning)) {
                                    break; //if cell was not cleared successfully -> break things placement cycle and move on to the next item
                                } else {
                                    cellIsAlreadyCleared = true;
                                }
                            }

                            if ((wallMap[x, z] > 1 || wallMap[x, z] == -1) && !map.roofGrid.Roofed(mapLocation)) {
                                map.roofGrid.SetRoof(mapLocation, RoofDefOf.RoofConstructed);
                            }

                            Thing thing = ThingMaker.MakeThing(thingDef, stuffDef);

                            if (thing != null) {
                                GenSpawn.Spawn(thing, mapLocation, map, new Rot4(itemTile.rot));
                                if (thingDef.CanHaveFaction) {
                                    thing.SetFaction(faction);
                                }

                                CompQuality q = thing.TryGetComp<CompQuality>();
                                if (q != null) {
                                    byte category = (byte)Math.Abs(Math.Round(Rand.Gaussian(0, 2)));
                                    if (category > 6) category = 6; 
                                    q.SetQuality((QualityCategory)category, ArtGenerationContext.Outsider);
                                }

                                CompBreakdownable b = thing.TryGetComp<CompBreakdownable>();
                                if (b != null) {
                                    if (Rand.Chance(0.8f)) {
                                        b.DoBreakdown();
                                    }
                                }


                                if (itemTile.stackCount > 1) {
                                    thing.stackCount = Rand.Range(1, Math.Min(thingDef.stackLimit, itemTile.stackCount));


                                    //Spoil things that can be spoiled. You shouldn't find a fresh meat an the old ruins.
                                    CompRottable rottable = thing.TryGetComp<CompRottable>();
                                    if (rottable != null) {
                                        //if deterioration degree is > 0.5 you definitely won't find any food.
                                        //anyway, there is a chance that you also won't get any food even if deterioriation is relatively low. animalr, raiders, you know.
                                        if (canHaveFood) {
                                            rottable.RotProgress = (Rand.Value * 0.5f + deteriorationDegree) * (rottable.PropsRot.TicksToRotStart);
                                        } else {
                                            rottable.RotProgress = rottable.PropsRot.TicksToRotStart + 1;
                                        }
                                    }
                                }

                                totalCost += itemTile.cost;

                                thing.HitPoints = Rand.Range(1, thing.def.BaseMaxHitPoints);
                                if (thing.def.EverHaulable) {
                                    thing.SetForbidden(true, false);
                                    if (map.terrainGrid.TerrainAt(mapLocation).IsWater) {
                                        thing.HitPoints /= Rand.Range(10, 100); //things in marsh or river are really in bad condition
                                    }
                                }
                            }
                        }
                    }
                }
            }
            
            Debug.Message("Transferred blueprint of total cost of approximately {0}", totalCost);
        }

        private void AddFilthAndRubble() {
            //spice up the area with some high quality dirt and trash

            for (int z = minZ; z < maxZ; z++) {
                for (int x = minX; x < maxX; x++) {

                    if (terrainIntegrity[x, z] < 0.1f) {
                        continue; //skip generating filth on empty
                    }

                    IntVec3 mapLocation = new IntVec3(x - minX + mapOriginX, 0, z - minZ + mapOriginZ);
                    if (!mapLocation.InBounds(map)) continue;


                    ThingDef[] filthDef = { ThingDefOf.Filth_Dirt, ThingDefOf.Filth_Trash, ThingDefOf.Filth_Ash };
                    FilthMaker.MakeFilth(mapLocation, map, filthDef[0], Rand.Range(0, 3));

                    while (Rand.Value > 0.7) {
                        FilthMaker.MakeFilth(mapLocation, map, filthDef[Rand.Range(0, 2)], Rand.Range(1, 5));
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

        private void AddSpecials() {
            //corpses, blood trails, mines and traps, bugs and bees
            //Pretty low chance to have someone's remainings
            for (int z = minZ; z < maxZ; z++) {
                for (int x = minX; x < maxX; x++) {


                    IntVec3 mapLocation = new IntVec3(x - minX + mapOriginX, 0, z - minZ + mapOriginZ);
                    if (!mapLocation.InBounds(map)) continue;

                    if (Rand.Value < options.decorationChance) {

                        bool canPlace = true;
                        List<Thing> things = map.thingGrid.ThingsListAt(mapLocation);
                        foreach (Thing t in things) {
                            if (t.def.fillPercent > 0.5) canPlace = false;
                        }
                        if (!canPlace) continue;

                        int timeOfDeath = Find.TickManager.TicksGame - (int)(Rand.Value * 100000000);
                        PawnGenerationRequest request = new PawnGenerationRequest(PawnKindDefOf.WildMan, null, PawnGenerationContext.NonPlayer, -1, false, false, false, false, true, false, 20f, false, true, true, false, false, false, false, null, null, null, null, null, null, null, null);
                        Pawn dweller = PawnGenerator.GeneratePawn(request);
                        GenSpawn.Spawn(dweller, mapLocation, map);
                        dweller.Kill(null);
                        CompRottable rottable = dweller.Corpse.TryGetComp<CompRottable>();
                        rottable.RotProgress = rottable.PropsRot.TicksToDessicated;
                        dweller.Corpse.timeOfDeath = timeOfDeath + (int)(Rand.Value * 100000);
                    } else if (Rand.Value < options.trapChance) {
                        ThingDef trapDef = ThingDefOf.TrapSpike;
                        Thing thing = ThingMaker.MakeThing(trapDef, ThingDefOf.WoodLog);
                        thing.SetFaction(Find.FactionManager.RandomEnemyFaction());
                        GenSpawn.Spawn(thing, mapLocation, map);
                    }

                }
            }


            //enemies
            if (Rand.Chance(options.hostileChance)) {
                CellRect rect = new CellRect(mapOriginX, mapOriginZ, maxX - minX, maxZ - minZ);
                if (!CellFinder.TryFindRandomCellInsideWith(rect, (IntVec3 x) => x.Standable(map) && wallMap[x.x - rect.minX + minX, x.z - rect.minZ + minZ] > 1, out IntVec3 testCell)) {
                    return; //interrupt if there are no closed cells available
                }

                PawnKindDef pawnKindDef = null;

                if (Rand.Chance(0.7f) && !options.shouldAddSignificantResistance) { //no animals in "significant resistance" scenario. Surely animals are not a significant resistance in sane amounts
                    //animals
                    pawnKindDef = map.Biome.AllWildAnimals.RandomElementByWeight((PawnKindDef def) => (def.RaceProps.foodType == FoodTypeFlags.CarnivoreAnimal || def.RaceProps.foodType == FoodTypeFlags.OmnivoreAnimal) ? 1 : 0);
                } else {
                    //mechanoids' kinds are selected for each unit
                }

                float powerMax = rect.Area / 30.0f;
                float powerThreshold = (Math.Abs(Rand.Gaussian(0.5f, 1)) * powerMax) + 1;
                

                Debug.Message("Gathering troops power of {0} (max was {1})", powerThreshold, powerMax);

                float cumulativePower = 0;

                Faction faction = Faction.OfAncientsHostile;
                
                Lord lord = LordMaker.MakeNewLord(lordJob: new LordJob_DefendPoint(rect.CenterCell), faction: faction, map: map, startingPawns: null);
                int tile = map.Tile;

                while (cumulativePower <= powerThreshold) { 

                    PawnKindDef currentPawnKindDef = pawnKindDef;
                    if (currentPawnKindDef == null) {
                        currentPawnKindDef = (from kind in DefDatabase<PawnKindDef>.AllDefsListForReading
                                              where kind.RaceProps.IsMechanoid
                                              select kind).RandomElementByWeight((PawnKindDef kind) => 1f / kind.combatPower);
                    }

                    PawnGenerationRequest request =
                        new PawnGenerationRequest(currentPawnKindDef, faction, PawnGenerationContext.NonPlayer, tile, true, false, false, //allowDead is last
                        false, true, false, 1f,
                        false, true, true, false,
                        false, false, false,
                        null, null, null, null,
                        null, null, null, null);

                    if (CellFinder.TryFindRandomCellInsideWith(rect, (IntVec3 x) => x.Standable(map) && wallMap[x.x - rect.minX + minX, x.z - rect.minZ + minZ] > 1, out IntVec3 cell)) {
                        Pawn pawn = PawnGenerator.GeneratePawn(request);

                        FilthMaker.MakeFilth(cell, map, ThingDefOf.Filth_Blood, 5);
                        GenSpawn.Spawn(pawn, cell, map, WipeMode.Vanish);

                        lord.AddPawn(pawn);
                        cumulativePower += pawn.kindDef.combatPower;

                        Debug.Message("Adding combat power for {0}, total is {1}", currentPawnKindDef.defName, cumulativePower);
                    } else {
                        break; //no more suitable cells
                    }
                }
            }
        }

        private void UpdateUsedCells() {
            for (int z = minZ; z < maxZ; z++) {
                for (int x = minX; x < maxX; x++) {
                    IntVec3 mapLocation = new IntVec3(x - minX + mapOriginX, 0, z - minZ + mapOriginZ);
                    if (!mapLocation.InBounds(map)) continue;

                    if (wallMap[x, z] != 1) cellUsed[mapLocation.x, mapLocation.z] = true; //mark walls and inner areas as used to prevent overlapping
                }
            }
        }

        private void RestoreDefencesAndPower() {
            foreach (var thing in map.spawnedThings) {
                if (thing.TryGetComp<CompPowerPlant>() != null || thing.TryGetComp<CompPowerBattery>() != null || (thing.def.building != null && thing.def.building.IsTurret)) {
                    CompBreakdownable bdcomp = thing.TryGetComp<CompBreakdownable>();
                    if (bdcomp != null) {
                        bdcomp.Notify_Repaired();
                    }
                }
            }
        }



        //Deterioration degree is unconditional modifier of destruction applied to the ruins bluepring. Degree of 0.5 means that in average each 2nd block in "central" part will be destroyed.
        //Scavenge threshold is an item price threshold after which the item or terrain is most likely scavenged.
        public void ScatterRuinsAt(IntVec3 loc, Map map, ScatterOptions options) {

            DateTime start = DateTime.Now;

            targetPoint = loc;
            this.map = map;
            this.options = options;


            referenceRadius = Rand.Range((int)(options.referenceRadiusAverage * 0.8f), (int)(options.referenceRadiusAverage * 1.2f));
            scavengersActivity = Rand.Value * options.scavengingMultiplier + (options.scavengingMultiplier) / 3;
            elapsedTime = (Rand.Value * options.scavengingMultiplier) * 3 + ((options.scavengingMultiplier > 0.95) ? 3 : 0);
            referenceRadiusJitter = referenceRadius / 10;

            deteriorationDegree = options.deteriorationMultiplier;

            Debug.Message("Scattering ruins at ({0}, {1}) of radius {2}. scavengers activity: {3}, age: {4}", loc.x, loc.z, referenceRadius, scavengersActivity, elapsedTime);
            //cut and deteriorate:
            // since the original blueprint can be pretty big, you usually don't want to replicate it as is. You need to cut a small piece and make a smooth transition

            //Debug.Message("Loading snapshot...");
            if (!LoadRandomXMLSnapshot()) {
                return; //no valid files to generate ruins.
            }

            //Debug.Message("Finding rooms...");
            if (options.shouldCutBlueprint) {
                FindRoomsAndConstructIntegrityMaps();
            } else {
                UntouchedIntegrityMapConstructor();
            }
            

            //Debug.Message("Processing items...");
            ProcessItems();
            //Debug.Message("Deteriorating...");
            Deteriorate();
            //Debug.Message("Scavenging...");
            RaidAndScavenge();
            //Debug.Message("Transferring blueprint...");
            TransferBlueprint();
            //Debug.Message("Adding filth and rubble...");
            AddFilthAndRubble();
            //Debug.Message("Adding something special...");
            AddSpecials();
            //Debug.Message("Ready");
            UpdateUsedCells();

            if (options.shouldKeepDefencesAndPower) {
                RestoreDefencesAndPower();
            }

            TimeSpan span = DateTime.Now - start;
            totalWorkTime += (int)span.TotalMilliseconds;
            Debug.Message("Added ruins for {0} seconds, total: {1} msec", span.TotalSeconds, totalWorkTime);

        }
    }
}
