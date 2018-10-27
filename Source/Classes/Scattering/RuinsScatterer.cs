using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using System.Xml;

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

            XmlAttribute rotAttribute = node.Attributes["rot"];
            if (rotAttribute != null) {
                rot = int.Parse(s: rotAttribute.Value);
            } else {
                rot = 0;
            }

        }

    }

    class RuinsScatterer {

        static private int totalWorkTime = 0;

        // Clear the cell from other destroyable objects
        private bool ClearCell(IntVec3 location, Map map) {
            List<Thing> items = map.thingGrid.ThingsListAt(location);
            if (!CanClearCell(location, map)) return false;
            for (int index = items.Count - 1; index >= 0; index--) {
                items[index].Destroy(DestroyMode.Vanish);
            }
            return true;
        }

        // Clear the cell from other destroyable objects
        private bool CanClearCell(IntVec3 location, Map map) {
            List<Thing> items = map.thingGrid.ThingsListAt(location);
            foreach (Thing item in items) {
                if (!item.def.destroyable) {
                    return false;
                }
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

        object SL(object o) {
            return (o != null) ? o : "<NULL>";
        }

        //Calculates cost of item made of stuff, or default cost if stuff is null
        //Golden wall is a [Wall] made of [Gold], golden tile is a [GoldenTile] made of default material
        private float ThingComponentsMarketCost(BuildableDef buildable, ThingDef stuffDef = null) {
            float num = 0f;

            if (buildable.costList != null) {
                foreach (ThingDefCountClass cost in buildable.costList) {
                    num += (float)cost.count * ThingComponentsMarketCost(cost.thingDef);
                }
            }

            if (buildable.costStuffCount > 0) {
                if (stuffDef == null) {
                    stuffDef = GenStuff.DefaultStuffFor(buildable);
                }
                num += (float)buildable.costStuffCount * stuffDef.BaseMarketValue * (1.0f / stuffDef.VolumePerUnit);
            }

            if (num == 0) {
                if (buildable is ThingDef) {
                    if (((ThingDef)buildable).recipeMaker == null) {
                        return ((ThingDef)buildable).BaseMarketValue; //on some reason base market value is calculated wrong for, say, golden walls
                    }
                }
            }
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


        private void LoadRandomXMLSnapshot() {
            //Create the XmlDocument.  
            XmlDocument snapshot = new XmlDocument();

            snapshot.Load(SnapshotStoreManager.Instance.RandomSnapshotFilename());

            XmlNodeList elemList = snapshot.GetElementsByTagName("cell");
            blueprintWidth = int.Parse(snapshot.FirstChild.Attributes["width"].Value);
            blueprintHeight = int.Parse(snapshot.FirstChild.Attributes["height"].Value);

            terrainMap = new TerrainTile[blueprintWidth, blueprintHeight];
            roofMap = new bool[blueprintWidth, blueprintHeight];
            itemsMap = new List<ItemTile>[blueprintWidth, blueprintHeight];

            wallMap = new int[blueprintWidth, blueprintHeight];


            //base deterioration chance mask. will be used in future to create freeform deterioration which is much more fun than just circular
            //deterioration change also depends on material and cost, but is always based on base chance
            terrainIntegrity = new float[blueprintWidth, blueprintHeight]; //integrity of floor tiles
            itemsIntegrity = new float[blueprintWidth, blueprintHeight]; //base integrity of walls, roofs and items


            //should food ever be spawned for this ruins
            canHaveFood = Rand.Chance((1.0f - deteriorationDegree) / 4);

            foreach (XmlNode cellNode in elemList) {
                int x = int.Parse(cellNode.Attributes["x"].Value);
                int z = int.Parse(cellNode.Attributes["z"].Value);
                itemsMap[x, z] = new List<ItemTile>();

                foreach (XmlNode cellElement in cellNode.ChildNodes) {
                    if (cellElement.Name.Equals("terrain")) {
                        TerrainTile terrain = new TerrainTile(cellElement);
                        terrain.location = new IntVec3(x, 0, z);
                        terrainMap[x, z] = terrain;
                    } else if (cellElement.Name.Equals("item")) {
                        ItemTile tile = new ItemTile(cellElement);
                        tile.location = new IntVec3(x, 0, z);
                        itemsMap[x, z].Add(tile);

                        ThingDef thingDef = DefDatabase<ThingDef>.GetNamed(tile.defName, false);
                        if (thingDef != null && thingDef.fillPercent == 1.0f) {
                            wallMap[x, z] = -1; //place wall
                        }
                    } else if (cellElement.Name.Equals("roof")) {
                        roofMap[x, z] = true;
                    }
                }
            }
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

            //For each unmarked point we can interpret our map as a tree with root at current point and branches going to four directions. For this tree (with removed duplicate nodes) we can implement BFS travrsing.
            for (int z = 0; z < blueprintHeight; z++) {
                for (int x = 0; x < blueprintWidth; x++) {
                    if (wallMap[x, z] == 0) {
                        TraverseCells(new List<IntVec3>() { new IntVec3(x, 0, z) });
                        currentRoomIndex += 1;
                    }
                }
            }

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

                int selectedRoomIndex = 0;
                if (suitableRoomIndices.Count == 0) {
                    //1x. no suitable rooms: fallback plan
                    FallbackIntegrityMapConstructor(new IntVec3(0, 0, 0), referenceRadius);
                    return;
                } else {
                    selectedRoomIndex = suitableRoomIndices[Rand.Range(0, suitableRoomIndices.Count)];
                }

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


                //3. draw a random circle around that room
                int radius = Rand.Range(referenceRadius - referenceRadiusJitter, referenceRadius + referenceRadiusJitter);
                if (radius * 2 > blueprintWidth) { radius = blueprintWidth / 2 - 1; }
                if (radius * 2 > blueprintHeight) { radius = blueprintHeight / 2 - 1; }
                if (radius > center.x) { center.x = radius; } //shift central point if it's too close to blueprint edge
                if (radius > center.y) { center.y = radius; }
                if (center.x + radius > blueprintWidth) { center.x = blueprintWidth - radius; }
                if (center.y + radius > blueprintHeight) { center.y = blueprintHeight - radius; }

                minX = center.x - radius; maxX = center.x + radius;
                minZ = center.z - radius; maxZ = center.z + radius;

                mapOriginX = targetPoint.x - (maxX - minX) / 2;
                mapOriginZ = targetPoint.z - (maxZ - minZ) / 2;

                if (mapOriginX < 10) mapOriginX = 10;
                if (mapOriginZ < 10) mapOriginZ = 10;

                if (mapOriginX + (maxX - minX) > map.info.Size.x) mapOriginX = map.info.Size.x - 10 - (maxX - minX);
                if (mapOriginZ + (maxZ - minZ) > map.info.Size.z) mapOriginZ = map.info.Size.z - 10 - (maxZ - minZ);


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
                        } else if (sqrDistance < (radius * radius)) { //edge
                            if (!allRooms.Contains(roomIndex)) allRooms.Add(roomIndex);
                            if (!openRooms.Contains(roomIndex)) openRooms.Add(roomIndex);
                        }
                    }
                }

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

                    // normalize terrain core values which were used for border generation
                    for (int x = minX; x < maxX; x++) {
                        for (int z = minZ; z < maxZ; z++) {
                            if (terrainIntegrity[x, z] > 1) {
                                terrainIntegrity[x, z] = 1;
                            }
                        }
                    }

                    BlurIntegrityMap(terrainIntegrity, 7);
                    BlurIntegrityMap(itemsIntegrity, 4);
                    //At this step we have integrity maps, so we can proceed further and simulate deterioration and scavenging
                }
            }
        }

        private void ProcessItems() {
            //Each item should be checked if it can be placed or not. This should help preventing situations when simulated scavenging removes things which anyway won't be placed.
            //For each placed item it's cost should be calculated
            for (int x = minX; x < maxX; x++) {
                for (int z = minZ; z < maxZ; z++) {
                    //Log.Clear();

                    if (itemsMap[x, z] == null) { itemsMap[x, z] = new List<ItemTile>(); }//to make thngs easier add empty list to every cell

                    List<ItemTile> items = itemsMap[x, z];
                    TerrainTile terrain = terrainMap[x, z];
                    TerrainDef terrainDef = null;

                    if (terrain != null) {
                        terrainDef = DefDatabase<TerrainDef>.GetNamed(terrain.defName, false);
                        if (terrainDef != null) {
                            terrain.cost = ThingComponentsMarketCost(terrainDef);
                        } else {
                            terrain.cost = 0.0f;
                        }
                        terrain.weight = 5.0f;
                    }

                    TerrainDef existingTerrain = map.terrainGrid.TerrainAt(new IntVec3(x - minX + mapOriginX, 0, z - minZ + mapOriginZ));

                    List<ItemTile> itemsToRemove = new List<ItemTile>();
                    foreach (ItemTile item in items) {
                        ThingDef thingDef = DefDatabase<ThingDef>.GetNamed(item.defName, false);

                        if (thingDef != null && thingDef.terrainAffordanceNeeded != null) {
                            if (terrainDef != null && terrainDef.terrainAffordanceNeeded != null && existingTerrain.affordances.Contains(terrainDef.terrainAffordanceNeeded)) {
                                if (!terrainDef.affordances.Contains(thingDef.terrainAffordanceNeeded)) { //if new terrain can be placed over existing terrain, checking if an item can be placed over a new terrain
                                    itemsToRemove.Add(item);
                                }
                            } else {
                                if (!existingTerrain.affordances.Contains(thingDef.terrainAffordanceNeeded)) {//otherwise checking if the item can be placed over the existing terrain.
                                    itemsToRemove.Add(item);
                                }
                            }
                        }
                    }

                    foreach (ItemTile item in itemsToRemove) {
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
                    }
                }
            }
        }

        private void Deteriorate() {
            //remove everything according do integrity maps
            for (int x = minX; x < maxX; x++) {
                for (int z = minZ; z < maxZ; z++) {
                    List<ItemTile> items = itemsMap[x, z];

                    if (!Rand.Chance(terrainIntegrity[x, z])) {
                        terrainMap[x, z] = null;
                    }

                    float itemsChance = itemsIntegrity[x, z];
                    List<ItemTile> newItems = new List<ItemTile>();
                    if (itemsChance > 0 && itemsChance < 1) {
                        foreach (ItemTile item in items) {
                            if (Rand.Chance(itemsChance)) {
                                newItems.Add(item);
                            }
                        }
                    }
                    if (itemsChance < 1) {
                        itemsMap[x, z] = newItems; //will be empty if chance is 0
                    }
                }
            }
        }

        private void RaidAndScavenge() {
            //remove the most precious things. smash some other things.
            //word is spread, so each next raid is more destructive than the previous ones
            //to make calculations a bit easier we're going to calculate value per cell, not per item.

            //Log.Clear();
            
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

                    if (topTile.cost < 25) {
                        shouldStop = true; //nothing to do here, everything valueable has already gone
                        msg += "Too cheap, stopping.";
                    } else {
                        if (Rand.Chance(0.999f)) { //there is still chance that even the most expensive thing will be left after raid. "Big momma said ya shouldn't touch that golden chair, it's cursed")
                            raidCapacity -= topTile.weight; //not counting weight for now.
                            if (topTile is TerrainTile) {
                                terrainMap[topTile.location.x, topTile.location.z] = null;
                                msg += "Terrain removed.";
                            } else if (topTile is ItemTile) {
                                itemsMap[topTile.location.x, topTile.location.z].Remove((ItemTile)topTile);
                                msg += "Tile removed.";
                            }
                        }
                    }
                    Debug.Message(msg);
                }
            }
        }

        private void TransferBlueprint() {
            //Planting blueprint

            Faction faction = (Rand.Value > 0.5) ? Find.FactionManager.OfAncientsHostile : Find.FactionManager.OfAncients;
            faction = Find.FactionManager.RandomEnemyFaction();
            Debug.Message("Setting faction to {0}", faction.def.defName);

            for (int z = minZ; z < maxZ; z++) {
                for (int x = minX; x < maxX; x++) {

                    //{loc.x, loc.z} should be mapped from blueprint's {floorcenter.x, floorcenter.z}
                    IntVec3 mapLocation = new IntVec3(x - minX + mapOriginX, 0, z - minZ + mapOriginZ);

                    //Check if thepoint is in allowed bounds of the map
                    if (!mapLocation.InBounds(map) || mapLocation.InNoBuildEdgeArea(map)) {
                        continue; //ignore invalid cells
                    }

                    //Construct terrain if some specific terrain stored in the blueprint
                    if (terrainMap[x, z] != null) {
                        TerrainDef blueprintTerrain = TerrainDef.Named(terrainMap[x, z].defName);
                        map.terrainGrid.SetTerrain(mapLocation, blueprintTerrain);
                    }

                    //Add items
                    if (itemsMap[x, z] != null && itemsMap[x, z].Count > 0) {

                        bool cellIsAlreadyCleared = false;
                        foreach (ItemTile itemTile in itemsMap[x, z]) {

                            ThingDef thingDef = DefDatabase<ThingDef>.GetNamed(itemTile.defName, false);
                            ThingDef stuffDef = null;
                            if (itemTile.stuffDef != null) {
                                stuffDef = DefDatabase<ThingDef>.GetNamed(itemTile.stuffDef, false);
                            }

                            if (!cellIsAlreadyCleared) { //first item to be spawned should also clear place for itself. we can't do it beforehand because we don't know it it will be able and get a chance to be spawned.
                                if (!ClearCell(mapLocation, map)) {
                                    break; //if cell was not cleared successfully -> break things placement cycle and move on to the next item
                                } else {
                                    cellIsAlreadyCleared = true;
                                }
                            }

                            Thing thing = ThingMaker.MakeThing(thingDef, stuffDef);


                            if (thing != null) {
                                GenSpawn.Spawn(thing, mapLocation, map, new Rot4(itemTile.rot));
                                if (itemTile.stackCount > 1) {
                                    thing.stackCount = Rand.Range(1, itemTile.stackCount);

                                    if (thingDef.CanHaveFaction) {
                                        thing.SetFactionDirect(faction);
                                    }

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

                                thing.HitPoints = Rand.Range(1, thing.def.BaseMaxHitPoints);
                                if (thing.def.EverHaulable) {
                                    thing.SetForbidden(true, false);
                                }
                            }
                        }
                    }
                }
            }
        }

        private void AddFilthAndRubble() {
            //spice up the area with some high quality dirt and trash

            for (int z = minZ; z < maxZ; z++) {
                for (int x = minX; x < maxX; x++) {

                    IntVec3 mapLocation = new IntVec3(x - minX + mapOriginX, 0, z - minZ + mapOriginZ);

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
                    if (Rand.Value < 0.0001) {

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
                    }

                }
            }
        }



        //Deterioration degree is unconditional modifier of destruction applied to the ruins bluepring. Degree of 0.5 means that in average each 2nd block in "central" part will be destroyed.
        //Scavenge threshold is an item price threshold after which the item or terrain is most likely scavenged.
        public void ScatterRuinsAt(IntVec3 loc, Map map, int referenceRadius, int radiusJitter, float deteriorationDegree, float scavengersActivity, float elapsedTime)
        {

            Debug.Message("Scattering ruins at ({0}, {1}) of radius {2}+-{3}. Deterioriation degree: {4}, scavengers activity: {5}, age: {6}", loc.x, loc.z, referenceRadius, radiusJitter, deteriorationDegree, scavengersActivity, elapsedTime);
            DateTime start = DateTime.Now;

            targetPoint = loc;
            this.map = map;

            this.referenceRadius = referenceRadius;
            this.scavengersActivity = scavengersActivity;
            this.elapsedTime = elapsedTime;
            referenceRadiusJitter = radiusJitter;

            this.deteriorationDegree = deteriorationDegree;
            //cut and deteriorate:
            // since the original blueprint can be pretty big, you usually don't want to replicate it as is. You need to cut a small piece and make a smooth transition

            Debug.Message("Loading snapshot...");
            LoadRandomXMLSnapshot();
            Debug.Message("Finding rooms...");
            FindRoomsAndConstructIntegrityMaps();
            Debug.Message("Processing items...");
            ProcessItems();
            Debug.Message("Deteriorating...");
            Deteriorate();
            Debug.Message("Scavenging...");
            RaidAndScavenge();
            Debug.Message("Transferring blueprint...");
            TransferBlueprint();
            Debug.Message("Adding filth and rubble...");
            AddFilthAndRubble();
            Debug.Message("Adding something special...");
            AddSpecials();
            Debug.Message("Ready");

            TimeSpan span = DateTime.Now - start;
            totalWorkTime += (int)span.TotalMilliseconds;
            Debug.Message("Added ruins for {0} seconds, total: {1} msec", span.TotalSeconds, totalWorkTime);

        }
    }
}
