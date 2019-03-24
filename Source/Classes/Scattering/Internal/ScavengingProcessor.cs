using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Verse;

namespace RealRuins {
    class ScavengingProcessor {

        public static void RaidAndScavenge(Blueprint blueprint, ScatterOptions options) {
            //remove the most precious things. smash some other things.
            //word is spread, so each next raid is more destructive than the previous ones
            //to make calculations a bit easier we're going to calculate value per cell, not per item.

            Debug.active = false;

            float scavengersActivity = Rand.Value * options.scavengingMultiplier + (options.scavengingMultiplier) / 3; //slight variation for scavengers activity for this particular blueprint
            float elapsedTime = -blueprint.dateShift;


            List<Tile> tilesByCost = new List<Tile>();

            for (int x = 0; x < blueprint.width; x++) {
                for (int z = 0; z < blueprint.height; z++) {
                    if (blueprint.terrainMap[x, z] != null) {
                        tilesByCost.Add(blueprint.terrainMap[x, z]);
                    }

                    foreach (ItemTile item in blueprint.itemsMap[x, z]) {
                        tilesByCost.Add(item);
                    }
                }
            }

            tilesByCost.Sort(delegate (Tile t1, Tile t2) {
                return (t1.cost / t1.weight).CompareTo(t2.cost / t2.weight);
            });

            int ruinsArea = blueprint.width * blueprint.height;

            //Debug.Message("Scavenging blueprint of area {0}, age {1}, scavengers activity multiplier {2}", ruinsArea, elapsedTime, scavengersActivity);
            //Debug.Message("Enumerated {0} items", tilesByCost.Count());
            //Debug.PrintArray(tilesByCost.ToArray());

            int raidsCount = (int)(Math.Log(elapsedTime / 10 + 1) * scavengersActivity);
            if (raidsCount > 50) raidsCount = 50;
            if (options.scavengingMultiplier > 0.9f && raidsCount <= 0) {
                raidsCount = 1; //at least one raid for each ruins in case of normal scavenging activity
            }
            float baseRaidCapacity = ruinsArea / 10 * scavengersActivity;

            Debug.Message("Performing {0} raids. Base capacity: {1}", raidsCount, baseRaidCapacity);

            for (int i = 0; i < raidsCount; i++) {
                float raidCapacity = baseRaidCapacity * (float)Math.Pow(1.1, i);
                bool shouldStop = false;
                Debug.Message("Performing raid {0} of capacity {1}", i, raidCapacity);

                while (tilesByCost.Count > 0 && raidCapacity > 0 && !shouldStop) {
                    Tile topTile = tilesByCost.Pop();
                    String msg = string.Format("Inspecting tile \"{0}\" of cost {1} and weight {2}. ", topTile.defName, topTile.cost, topTile.weight);

                    if (topTile.cost / topTile.weight < 7) {
                        shouldStop = true; //nothing to do here, everything valueable has already gone
                        msg += "Too cheap, stopping.";
                    } else {
                        if (Rand.Chance(0.999f)) { //there is still chance that even the most expensive thing will be left after raid. ("Big momma said ya shouldn't touch that golden chair, it's cursed")
                            raidCapacity -= topTile.weight; 
                            if (topTile is TerrainTile) {
                                blueprint.terrainMap[topTile.location.x, topTile.location.z] = null;
                                msg += "Terrain removed.";
                            } else if (topTile is ItemTile) {
                                ItemTile itemTile = topTile as ItemTile;
                                blueprint.itemsMap[topTile.location.x, topTile.location.z].Remove((ItemTile)topTile);
                                if (itemTile.isDoor) { //if door is removed it should be replaced with another door, raiders are very polite and always replace expensive doors with cheaper ones.
                                    if (Rand.Chance(0.8f)) { //ok, not always.
                                        ItemTile replacementTile = ItemTile.DefaultDoorItemTile(itemTile.location);
                                        blueprint.itemsMap[topTile.location.x, topTile.location.z].Add(replacementTile);
                                        msg += "Added " + replacementTile.defName + ", original ";
                                    } else {
                                        blueprint.RemoveWall(itemTile.location.x, itemTile.location.z);
                                    }
                                } else if (itemTile.isWall) { //if something like a wall removed (vent or aircon) you usually want to cover the hole to keep wall integrity
                                    ItemTile replacementTile = ItemTile.WallReplacementItemTile(itemTile.location);
                                    blueprint.itemsMap[topTile.location.x, topTile.location.z].Add(replacementTile);
                                    msg += "Added " + replacementTile.defName + ", original ";
                                }
                                msg += "Tile removed.";
                            }
                        }
                    }
                    Debug.Message(msg);
                    if (shouldStop) break;
                }
                if (shouldStop) break;
            }

            //Check that there are no "hanging doors" left
            bool HasWallsIn(List<ItemTile> list) {
                foreach (ItemTile tile in list) {
                    if (tile.isWall) return true;
                }
                return false;
            }

            for (int z = 1; z < blueprint.height - 1; z++) {
                for (int x = 1; x < blueprint.width - 1; x++) {
                    ItemTile tileToRemove = null;
                    foreach (ItemTile tile in blueprint.itemsMap[x, z]) {
                        if (tile.isDoor) { //check if a particular door tile has both two vertically adjacent walls (or similar) or two horizintally adjacent walls
                            if (!(HasWallsIn(blueprint.itemsMap[x - 1, z]) && HasWallsIn(blueprint.itemsMap[x + 1, z])) &&
                                !(HasWallsIn(blueprint.itemsMap[x, z - 1]) && HasWallsIn(blueprint.itemsMap[x, z + 1]))) {
                                tileToRemove = tile;
                                break;
                            }
                        }
                    }
                    if (tileToRemove != null) {
                        blueprint.itemsMap[x, z].Remove(tileToRemove);
                        blueprint.RemoveWall(x, z);
                    }
                }
            }
            Debug.active = true;
        }
    }
}
