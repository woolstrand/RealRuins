using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Verse;

namespace RealRuins {
    class ScavengingProcessor {

        private List<Tile> tilesByCost = new List<Tile>();
        private ScatterOptions options;
        private Blueprint blueprint;
        float totalCost = 0;

        //Scavengers take things out of containers (bookcases, minified things, sarcophagi) instead of carrying the whole container away:
        //contents are separate candidates, and a container is judged by its own value only (OwnCost).
        private readonly List<ItemTile> contents = new List<ItemTile>();
        private readonly HashSet<ItemTile> removedItems = new HashSet<ItemTile>();

        int totalRemovedTiles = 0;
        int totalRemovedTerrains = 0;
        int totalReplacedTiles = 0;

        //wealthAllowance: what this blueprint may still be worth after scavenging (its share of the map's ruins wealth), -1 for no limit.
        //options.costCap is the other limit (abandoned base, ruined POI); the lower one applies.
        public void RaidAndScavenge(Blueprint blueprint, ScatterOptions options, float wealthAllowance = -1) {
            //remove the most precious things. smash some other things.
            //word is spread, so each next raid is more destructive than the previous ones
            //to make calculations a bit easier we're going to calculate value per cell, not per item.

            this.options = options;
            this.blueprint = blueprint;

            Debug.active = false;

            float scavengersActivity = Rand.Value * options.scavengingMultiplier + (options.scavengingMultiplier) / 3; //slight variation for scavengers activity for this particular blueprint
            float elapsedTime = -blueprint.dateShift;

            int processedTiles = 0;
            int processedTerrains = 0;

            for (int x = 0; x < blueprint.width; x++) {
                for (int z = 0; z < blueprint.height; z++) {
                    if (blueprint.terrainMap[x, z] != null) {
                        tilesByCost.Add(blueprint.terrainMap[x, z]);
                        totalCost += blueprint.terrainMap[x, z].cost;
                        processedTerrains++;
                    }

                    foreach (ItemTile item in blueprint.itemsMap[x, z]) {
                        tilesByCost.Add(item);
                        totalCost += item.cost; //contents included
                        processedTiles++;
                        AddContents(item);
                    }
                }
            }

            tilesByCost.Sort(delegate (Tile t1, Tile t2) {
                return (OwnCost(t1) / t1.weight).CompareTo(OwnCost(t2) / t2.weight);
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

            Debug.Log(Debug.BlueprintTransfer, "Performing {0} raids. Base capacity: {1}", raidsCount, baseRaidCapacity);

            for (int i = 0; i < raidsCount; i++) {
                float raidCapacity = baseRaidCapacity * (float)Math.Pow(1.1, i);
                bool shouldStop = false;
                Debug.Log(Debug.BlueprintTransfer, "Performing raid {0} of capacity {1}", i, raidCapacity);

                while (tilesByCost.Count > 0 && raidCapacity > 0 && !shouldStop) {
                    Tile topTile = tilesByCost.Pop();
                    if (topTile is ItemTile poppedItem && IsGone(poppedItem)) continue; //its container was taken earlier
                    String msg = string.Format("Inspecting tile \"{0}\" of cost {1} and weight {2}. ", topTile.defName, OwnCost(topTile), topTile.weight);

                    if (OwnCost(topTile) / topTile.weight < 7) {
                        shouldStop = true; //nothing to do here, everything valueable has already gone
                        msg += "Too cheap, stopping.";
                    } else {
                        if (Rand.Chance(0.999f)) { //there is still chance that even the most expensive thing will be left after raid. ("Big momma said ya shouldn't touch that golden chair, it's cursed")
                            raidCapacity -= topTile.weight; 
                            msg += RemoveTile(topTile, replaceWalls: true);
                        }
                    }
                    Debug.Log(Debug.BlueprintTransfer, msg);
                    if (shouldStop) break;
                }
                if (shouldStop) break;
            }

            //the cost cap (abandoned base, ruined POI) counts everything, the map's wealth limit may leave out basic structure
            if (options.costCap > 0) CutTo(options.costCap, countAll: true);
            if (wealthAllowance >= 0) CutTo(wealthAllowance, countAll: false);

            //Check that there are no "hanging doors" left (also after a cut that dismantled walls)
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
                        totalCost -= tileToRemove.cost;
                        blueprint.RemoveWall(x, z);
                    }
                }
            }
            Debug.active = true;
            //Debug.Message("Scavenging completed. Terrain removed: {0}/{1}, Tiles removed: {2}/{3}, tiles replaced: {4}.", totalRemovedTerrains, processedTerrains, totalRemovedTiles, processedTiles, totalReplacedTiles);
        }

        //Takes a tile away: contents out of their container, terrain removed, items removed. Doors and wall-like things are either
        //replaced with cheap ones to keep the walls closed (replaceWalls), or dismantled, leaving a hole. Returns a log message.
        private string RemoveTile(Tile tile, bool replaceWalls) {
            if (tile is ItemTile contentTile && contentTile.container != null) {
                contentTile.TakeOutOfContainer();
                removedItems.Add(contentTile);
                totalCost -= contentTile.cost;
                totalRemovedTiles++;
                return "Taken out of a container.";
            }
            if (tile is TerrainTile) {
                blueprint.terrainMap[tile.location.x, tile.location.z] = null;
                totalRemovedTerrains++;
                totalCost -= tile.cost;
                return "Terrain removed.";
            }

            ItemTile itemTile = tile as ItemTile;
            totalCost -= itemTile.cost;
            blueprint.itemsMap[tile.location.x, tile.location.z].Remove(itemTile);
            removedItems.Add(itemTile);

            ItemTile replacementTile = null;
            if (replaceWalls && itemTile.isDoor) { //if door is removed it should be replaced with another door, raiders are very polite and always replace expensive doors with cheaper ones.
                if (Rand.Chance(0.8f)) replacementTile = ItemTile.DefaultDoorItemTile(itemTile.location); //ok, not always.
            } else if (replaceWalls && itemTile.isWall) { //if something like a wall removed (vent or aircon) you usually want to cover the hole to keep wall integrity
                replacementTile = ItemTile.WallReplacementItemTile(itemTile.location);
            }

            if (replacementTile != null) {
                blueprint.itemsMap[tile.location.x, tile.location.z].Add(replacementTile);
                totalCost += replacementTile.cost;
                totalReplacedTiles++;
                return "Replaced with " + replacementTile.defName + ".";
            }
            if (itemTile.isWall || itemTile.isDoor) blueprint.RemoveWall(itemTile.location.x, itemTile.location.z);
            totalRemovedTiles++;
            return "Tile removed.";
        }

        //Brings the blueprint down to the target cost (map ruins wealth allowance, or the cost cap of an abandoned base / ruined POI):
        //the most valuable things go first. Unlike raids it ignores weight, so a few expensive things go and the ruin keeps its look.
        //Walls and doors are replaced in regular ruins (a golden room in old ruins looks implausible, a hole breaks the ruin),
        //and dismantled where options.cutDismantlesWalls is set (a half-dismantled golden room fits a scavenged base).
        private void CutTo(float target, bool countAll) {
            countAll |= !RealRuins_ModSettings.ruinsWealthIgnoreStructure;
            //candidates come from the blueprint, not from tilesByCost: raids pop tiles they don't take (the "too cheap per kg" one ending a raid),
            //and heavy expensive things like ship beams are exactly those
            var candidates = new List<Tile>(contents);
            for (int x = 0; x < blueprint.width; x++) {
                for (int z = 0; z < blueprint.height; z++) {
                    if (blueprint.terrainMap[x, z] != null) candidates.Add(blueprint.terrainMap[x, z]);
                    candidates.AddRange(blueprint.itemsMap[x, z]);
                }
            }

            candidates.RemoveAll(tile => tile is ItemTile item && IsGone(item));
            if (!countAll) candidates.RemoveAll(tile => !tile.CountsForWealthLimit);
            //what the target is compared with: everything (totalCost), or only what counts for the wealth limit
            float countedCost = countAll ? totalCost : candidates.Sum(tile => tile.OwnCost);
            if (countedCost <= target) return;
            Debug.Log(Debug.BlueprintTransfer, "Cutting cost of {0} (total {1}) to {2}", countedCost, totalCost, target);

            bool replaceWalls = !options.cutDismantlesWalls;
            float replacementWallCost = ItemTile.WallReplacementItemTile().cost;
            int removed = 0;
            foreach (Tile tile in candidates.OrderByDescending(OwnCost).ToList()) {
                if (countedCost <= target) break;
                if (OwnCost(tile) <= 0) break;
                if (tile is ItemTile item && IsGone(item)) continue;
                //replacing a wall with one worth as much (granite with granite, or cheaper) gains nothing
                if (replaceWalls && tile is ItemTile wall && wall.isWall && !wall.isDoor && wall.OwnCost <= replacementWallCost) continue;
                if (countAll) {
                    RemoveTile(tile, replaceWalls);
                    countedCost = totalCost; //replacements count too
                } else {
                    countedCost -= tile.cost; //with whatever it still holds; replacements are basic structure and don't count
                    RemoveTile(tile, replaceWalls);
                }
                removed++;
            }
            Debug.Log(Debug.BlueprintTransfer, "Removed {0} tiles, resulting cost is {1} (total {2})", removed, countedCost, totalCost);
        }

        //Adds a container's contents as separate candidates. Things with no value (corpses, pawns, missing defs) are left inside.
        private void AddContents(ItemTile container) {
            if (container.innerItems == null) return;
            foreach (ItemTile inner in container.innerItems) {
                if (inner.cost <= 0) continue;
                contents.Add(inner);
                tilesByCost.Add(inner);
                AddContents(inner);
            }
        }

        private static float OwnCost(Tile tile) => tile.OwnCost;

        //Removed itself, or its container (at any level) was carried away whole
        private bool IsGone(ItemTile item) {
            for (ItemTile current = item; current != null; current = current.container) {
                if (removedItems.Contains(current)) return true;
            }
            return false;
        }
    }

}
