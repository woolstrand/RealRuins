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
    class TerrainTile
    {
        public string defName;

        public TerrainTile(XmlNode node)
        {
            defName = node.Attributes["def"].Value;
        }
    }

    //stores information about item in the blueprint
    class ItemTile
    {
        public string defName;
        public string stuffDef;
        public int stackCount;
        public int rot;

        public int cost; //populated later

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


        int blueprintWidth;
        int blueprintHeight;

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
                        terrainMap[x, z] = new TerrainTile(cellElement);
                    } else if (cellElement.Name.Equals("item")) {
                        ItemTile tile = new ItemTile(cellElement);
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

                int minX = center.x - radius; int maxX = center.x + radius;
                int minZ = center.z - radius; int maxZ = center.z + radius;

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

                    float[,] delta = new float[blueprintWidth, blueprintHeight]; //delta integrity for making blur
                    // - then blur the map to create gradient deterioration around the intact area.
                    for (int steps = 0; steps < 10; steps++) { //terrain map
                        for (int x = minX + 1; x < maxX - 1; x++) {
                            for (int z = minZ + 1; z < maxZ - 1; z++) {
                                delta[x, z] = (terrainIntegrity[x - 1, z - 1] + terrainIntegrity[x, z - 1] + terrainIntegrity[x + 1, z - 1] +
                                    terrainIntegrity[x - 1, z] + terrainIntegrity[x, z] + terrainIntegrity[x + 1, z] +
                                    terrainIntegrity[x - 1, z + 1] + terrainIntegrity[x, z + 1] + terrainIntegrity[x + 1, z + 1]) / 9.0f;
                            }
                        }
                        for (int x = minX + 1; x < maxX - 1; x++) {
                            for (int z = minZ + 1; z < maxZ - 1; z++) {
                                if (terrainIntegrity[x, z] < 1) {
                                    terrainIntegrity[x, z] = delta[x, z] * (0.9f + Rand.Value * 0.1f);
                                }
                            }
                        }
                    }

                    for (int steps = 0; steps < 4; steps++) {//items map
                        for (int x = minX + 1; x < maxX - 1; x++) {
                            for (int z = minZ + 1; z < maxZ - 1; z++) {
                                delta[x, z] = (itemsIntegrity[x - 1, z - 1] + itemsIntegrity[x, z - 1] + itemsIntegrity[x + 1, z - 1] +
                                    itemsIntegrity[x - 1, z] + itemsIntegrity[x, z] + itemsIntegrity[x + 1, z] +
                                    itemsIntegrity[x - 1, z + 1] + itemsIntegrity[x, z + 1] + itemsIntegrity[x + 1, z + 1]) / 9.0f;
                            }
                        }
                        for (int x = minX + 1; x < maxX - 1; x++) {
                            for (int z = minZ + 1; z < maxZ - 1; z++) {
                                if (itemsIntegrity[x, z] < 1) {
                                    itemsIntegrity[x, z] = delta[x, z] * (0.7f + Rand.Value * 0.3f);
                                }
                            }
                        }
                    }
                    //At this step we have integrity maps, so we can proceed further and simulate deterioration and scavenging
                }
            }
        }

        private void RemoveUnplaceableItems() {
            //Each item should be checked if it can be placed or not. This should help preventing situations when simulated scavenging removes things which anyway won't be placed.
        }

        private void Deteriorate() {
            //remove everything according do integrity maps
        }

        private void RaidAndScavenge() {
            //remove the most precious things. smash some other things.
        }

        private void AddFilthAndRubble() {
            //spice up the area with some high quality dirt and trash
        }

        private void AddSpecials() {
            //corpses, blood trails, mines and traps, bugs and bees
        }



        //Deterioration degree is unconditional modifier of destruction applied to the ruins bluepring. Degree of 0.5 means that in average each 2nd block in "central" part will be destroyed.
        //Scavenge threshold is an item price threshold after which the item or terrain is most likely scavenged.
        public void ScatterRuinsAt(IntVec3 loc, Map map, int wallRadius = 6, int wallRadiusJitter = 2, int floorRadius = 7, int floorRadiusJitter = 2, float deteriorationDegree = 0.5f, float scavengeThreshold = 50.0f)
        {

            Debug.Message("Scattering ruins at ({0}, {1})", loc.x, loc.z);
            DateTime start = DateTime.Now;


            //cut and deteriorate:
            // since the original blueprint can be pretty big, you usually don't want to replicate it as is. You need to cut a small piece and make a smooth transition
            // center is a center of translated area inside the blueprint
            // floor cutoff radius is a radius at which floors becomes cut off completely. This is effective application radius as well
            // wall cutoff radius is a radius at which walls become cut off almost completely. It is smaller than floor cutoff radius.

            //now let's just fill deterioration mask with some circles, ald later will do freeform deterioration patterns
            // base integrity = 0.0 means item will never be spawned. 1.0 means item will always be spawned

            IntVec3 floorCenter = SelectRandomCenterInBounds(blueprintWidth, blueprintHeight, floorRadius, floorRadiusJitter);
            IntVec3 wallCenter = new IntVec3(floorCenter.x + Rand.Range(-(floorRadius - wallRadius), floorRadius - wallRadius), 0,
                                             floorCenter.z + Rand.Range(-(floorRadius - wallRadius), floorRadius - wallRadius));

            for (int z = Math.Max(floorCenter.z - floorRadius - floorRadiusJitter, 0); z < Math.Min(floorCenter.z + floorRadius + floorRadiusJitter, blueprintHeight); z++) {
                for (int x = Math.Max(floorCenter.x - floorRadius - floorRadiusJitter, 0); x < Math.Min(floorCenter.x + floorRadius + floorRadiusJitter, blueprintWidth); x++) {
                    if (!PointInsideCircle(x, z, floorCenter, floorRadius + floorRadiusJitter)) {
                        terrainIntegrity[x, z] = 0.0f; //outside all bounds => destroyed completely
                        itemsIntegrity[x, z] = 0.0f;
                        continue;
                    }

                    if (PointInsideCircle(x, z, wallCenter, wallRadius - wallRadiusJitter)) {
                        terrainIntegrity[x, z] = 1.0f;//Rand.Value / 20.0f + 0.95f; //inside walls => almost untouched
                        itemsIntegrity[x, z] = 1.0f;// Rand.Value / 20.0f + 0.95f;
                        continue;
                    }

                    double terrainDistance = Distance(x, z, floorCenter); //trying to make some gradient
                    double itemsDistance = Distance(x, z, wallCenter);

                    terrainIntegrity[x, z] = (floorRadius + floorRadiusJitter - (float)terrainDistance) / (floorRadius + floorRadiusJitter - (wallRadius - wallRadiusJitter));
                    if (terrainIntegrity[x, z] < 0) terrainIntegrity[x, z] = 0;
                    if (terrainIntegrity[x, z] > 1) terrainIntegrity[x, z] = 1;

                    itemsIntegrity[x, z] = (floorRadius + floorRadiusJitter - (float)itemsDistance) / (floorRadius + floorRadiusJitter - (wallRadius - wallRadiusJitter));
                    if (itemsIntegrity[x, z] < 0) itemsIntegrity[x, z] = 0;
                    if (itemsIntegrity[x, z] > 1) itemsIntegrity[x, z] = 1;
                }
            }

            Faction faction = (Rand.Value > 0.5) ? Find.FactionManager.OfAncientsHostile : Find.FactionManager.OfAncients;
            Debug.Message("Setting faction to {0}", faction.def.defName);

            //Planting blueprint
            for (int z = 0; z < blueprintHeight; z++) {
                for (int x = 0; x < blueprintWidth; x++) {

                    //{loc.x, loc.z} should be mapped from blueprint's {floorcenter.x, floorcenter.z}
                    IntVec3 mapLocation = new IntVec3(x - floorCenter.x + loc.x, 0, z - floorCenter.z + loc.z);
                    bool didAlterCell = false;

                    //Check if thepoint is in allowed bounds of the map
                    if (!mapLocation.InBounds(map) || mapLocation.InNoBuildEdgeArea(map)) {
                        continue; //ignore invalid cells
                    }

                    //Construct terrain if some specific terrain stored in the blueprint
                    if (terrainMap[x, z] != null) {
                        TerrainDef blueprintTerrain = TerrainDef.Named(terrainMap[x, z].defName);
                        

                        //check if terrain from blueprint can be built on top of existing terrain
                        if (blueprintTerrain != null && map.terrainGrid.TerrainAt(mapLocation).affordances.Contains(blueprintTerrain.terrainAffordanceNeeded)) {

                            float scavengeMultiplier = (float)Math.Max(1.0, scavengeThreshold / ThingComponentsMarketCost(blueprintTerrain));
                            float resultingIntegrity = terrainIntegrity[x, z] * scavengeMultiplier * (1.0f - deteriorationDegree);
                            float rv = Rand.Value;
                                                     
                            if (rv < resultingIntegrity) {
                                map.terrainGrid.SetTerrain(mapLocation, blueprintTerrain);
                                didAlterCell = true;
                            }
                        }
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



                            if (thingDef != null) {
                                //check if can ever be built on top of existing terrain
                                if (thingDef.terrainAffordanceNeeded != null && !map.terrainGrid.TerrainAt(mapLocation).affordances.Contains(thingDef.terrainAffordanceNeeded)) {
                                    continue;
                                }

                                //scavengers more likely will get things that are lighter and more expensive.
                                float massModifier = 1.0f;
                                if (thingDef.alwaysHaulable) {
                                    float mass = thingDef.GetStatValueAbstract(StatDefOf.Mass, stuffDef);
                                    massModifier = Math.Min(0.8f, (mass / 5.0f * itemTile.stackCount)); 
                                }

                                float cost = ThingComponentsMarketCost(thingDef, stuffDef) * itemTile.stackCount;
                                float scavengeModifier = Math.Min(0.95f, scavengeThreshold / cost);
                                float spawnThreshold = itemsIntegrity[x, z] * scavengeModifier * (1.0f - deteriorationDegree);


                                float spawnChance = Rand.Value;
                                if (spawnChance > spawnThreshold * 1.5) {
                                    continue; //item deteriorated/scavenged completely
                                } else { 
                                    //otherwise there is a chance that player will get some leftovers
                                    if (!cellIsAlreadyCleared) { //first item to be spawned should also clear place for itself. we can't do it beforehand because we don't know it it will be able and get a chance to be spawned.
                                        if (!ClearCell(mapLocation, map)) {
                                            break; //if cell was not cleared successfully -> break things placement cycle and move on to the next item
                                        } else {
                                            cellIsAlreadyCleared = true;
                                        }
                                    }

                                    if (!thingDef.BuildableByPlayer) {
                                        if (spawnChance > spawnThreshold) {
                                            continue; //no leftovers for items, only for buildings
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

                                        if (spawnChance > spawnThreshold) {
                                            thing.Destroy(DestroyMode.KillFinalize);
                                        } else {
                                        }

                                        didAlterCell = true;
                                    }
                                }
                            }
                        }
                    }

                    //Add some generic filth to floor. TODO: add optional rotten bodies, blood trails, vomit, whatever
                    if (didAlterCell && map.terrainGrid.TerrainAt(mapLocation).acceptFilth) {

                        ThingDef[] filthDef = { ThingDefOf.Filth_Dirt, ThingDefOf.Filth_Trash, ThingDefOf.Filth_Ash };
                        FilthMaker.MakeFilth(mapLocation, map, filthDef[0], Rand.Range(0, 3));

                        while (Rand.Value > 0.7) {
                            FilthMaker.MakeFilth(mapLocation, map, filthDef[Rand.Range(0, 2)], Rand.Range(1, 5));
                        }
                    }

                    //Pretty low chance to have someone's remainings
                    if (Rand.Value < 0.0001) {
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

            TimeSpan span = DateTime.Now - start;
            totalWorkTime += (int)span.TotalMilliseconds;
            Debug.Message("Added ruins for {0} seconds, total: {1} msec", span.TotalSeconds, totalWorkTime);

        }
    }
}
