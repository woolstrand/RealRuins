using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Verse;
using RimWorld;

namespace RealRuins {
    class DeteriorationProcessor {
        float[,] terrainIntegrity; //integrity of floor tiles
        float[,] itemsIntegrity; //base integrity of walls, roofs and items

        Blueprint blueprint;
        ScatterOptions options;


        //Constructs default circular integrity map if something goes wrong with the room based one creation.
        private void ConstructFallbackIntegrityMaps() {

            IntVec3 center = new IntVec3(blueprint.width / 2, 0, blueprint.height / 2);
            int radius = Math.Min(blueprint.width / 2, blueprint.height / 2);
            if (radius > 8) radius -= 4; //margins for smooth deterioration increase

            for (int x = 0; x < blueprint.width; x++) {
                for (int z = 0; z < blueprint.height; z++) {
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

            terrainIntegrity.Blur(10);
            itemsIntegrity.Blur(7);
        }

        private void ConstructRoomBasedIntegrityMap() {


            if (blueprint.roomsCount == 1) { 
                //no new rooms were added => blueprint does not have regular rooms or rooms were formed with use of some missing components or materials
                //fallback plan: construct old-fashioned circular deterioration map
                ConstructFallbackIntegrityMaps();
                return;
            } else {

                IntVec3 center = new IntVec3(blueprint.width / 2, 0, blueprint.height / 2);
                int radius = Math.Min(center.x, center.z);
                List<int> allRooms = new List<int>();
                List<int> openRooms = new List<int>();
                for (int x = 0; x < blueprint.width; x++) {
                    for (int z = 0; z < blueprint.height; z++) {
                        int roomIndex = blueprint.wallMap[x, z];
                        int sqrDistance = (x - center.x) * (x - center.x) + (z - center.z) * (z - center.z);
                        if (sqrDistance < (radius - 1) * (radius - 1)) {
                            if (!allRooms.Contains(roomIndex)) allRooms.Add(roomIndex);
                        } else if (sqrDistance < (radius * radius)) { //intersecting edge => room is opened to the wild
                            if (!allRooms.Contains(roomIndex)) allRooms.Add(roomIndex);
                            if (!openRooms.Contains(roomIndex)) openRooms.Add(roomIndex);
                            blueprint.MarkRoomAsOpenedAt(x, z);
                        }
                    }
                }

                //if all rooms are intersecting circle outline do fallback plan (circular deterioration chance map)
                if (allRooms.Count == openRooms.Count) {
                    ConstructFallbackIntegrityMaps();
                    return;
                } else {
                    //otherwise create the following deterioration map: 
                    List<int> closedRooms = allRooms.ListFullCopy();
                    foreach (int room in openRooms) {
                        closedRooms.Remove(room);
                    }

                    // - points in non-intersecting rooms: no destruction. 
                    for (int x = 0; x < blueprint.width; x++) {
                        for (int z = 0; z < blueprint.height; z++) {
                            int roomIndex = blueprint.wallMap[x, z];
                            if (closedRooms.Contains(roomIndex)) {
                                terrainIntegrity[x, z] = 20.0f; //terrain integrity is used later to calculate boundary based on it.
                                itemsIntegrity[x, z] = 1.0f; //items integrity is just usual map
                            }
                        }
                    }

                    //Debug.Message("Expanding core by one cell");

                    // - then add one pixel width expansion to cover adjacent wall.
                    for (int x = 1; x < blueprint.width - 1; x++) {
                        for (int z = 1; z < blueprint.height - 1; z++) {
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
                    for (int x = 0; x < blueprint.width; x++) {
                        for (int z = 0; z < blueprint.height; z++) {
                            if (terrainIntegrity[x, z] > 1) {
                                terrainIntegrity[x, z] = 1;
                            }
                        }
                    }

                    //Debug.Message("Blurring");
                    terrainIntegrity.Blur(7);
                    itemsIntegrity.Blur(4);
                    //At this step we have integrity maps, so we can proceed further and simulate deterioration and scavenging
                    //Debug.Message("Finished");
                }
            }
        }

        void ConstructUntouchedIntegrityMap() {
            for (int x = 0; x < blueprint.width; x++) {
                for (int z = 0; z < blueprint.height; z++) {
                    terrainIntegrity[x, z] = 1;
                    itemsIntegrity[x, z] = 1;
                }
            }
        }

        void Deteriorate() {
            for (int x = 0; x < blueprint.width; x++) {
                for (int z = 0; z < blueprint.height; z++) {
                    bool hadWall = false;
                    bool retainedWall = false;
                    List<ItemTile> items = blueprint.itemsMap[x, z];

                    if (!Rand.Chance(terrainIntegrity[x, z])) {
                        blueprint.terrainMap[x, z] = null;
                    }

                    if (blueprint.terrainMap[x, z] == null) {
                        blueprint.roofMap[x, z] = false; //no terrain - no roof. just is case. to be sure there won't be hanging roof islands floating in the air.
                    }

                    float itemsChance = itemsIntegrity[x, z] * (1.0f - options.deteriorationMultiplier);
                    List<ItemTile> newItems = new List<ItemTile>();
                    if (itemsChance > 0 && itemsChance < 1) {
                        blueprint.roofMap[x, z] = Rand.Chance(itemsChance * 0.3f); //roof will most likely collapse everywhere

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
                        blueprint.itemsMap[x, z] = newItems; //will be empty if chance is 0
                        if (itemsChance <= 0) blueprint.RemoveWall(x, z);
                    }

                    if (hadWall && !retainedWall) blueprint.RemoveWall(x, z);
                }
            }
        }


        public static void Process(Blueprint source, ScatterOptions options) {
            
            if (options.enableDeterioration) {
                DeteriorationProcessor dp = new DeteriorationProcessor();
                dp.options = options;
                dp.blueprint = source;
                dp.itemsIntegrity = new float[source.width, source.height];
                dp.terrainIntegrity = new float[source.width, source.height];
                dp.ConstructRoomBasedIntegrityMap();

                Debug.PrintNormalizedFloatMap(dp.terrainIntegrity);
                Debug.PrintNormalizedFloatMap(dp.itemsIntegrity);

                dp.Deteriorate();
            }
        }
    }
}
