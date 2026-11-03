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
        private readonly Dictionary<ItemTile, ItemTile> containerOf = new Dictionary<ItemTile, ItemTile>(); //contents -> the item holding them
        private readonly HashSet<ItemTile> removedItems = new HashSet<ItemTile>();

        int totalRemovedTiles = 0;
        int totalRemovedTerrains = 0;
        int totalReplacedTiles = 0;

        //wealthAllowance: what this blueprint may still be worth after scavenging (its share of the map's ruins wealth), -1 for no limit
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
                            msg += RemoveTile(topTile);
                        }
                    }
                    Debug.Log(Debug.BlueprintTransfer, msg);
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
                        totalCost -= tileToRemove.cost;
                        blueprint.RemoveWall(x, z);
                    }
                }
            }
            Debug.active = true;
            //Debug.Message("Scavenging completed. Terrain removed: {0}/{1}, Tiles removed: {2}/{3}, tiles replaced: {4}.", totalRemovedTerrains, processedTerrains, totalRemovedTiles, processedTiles, totalReplacedTiles);

            if (wealthAllowance >= 0 && totalCost > wealthAllowance) { CutToAllowance(wealthAllowance); }
            if (options.costCap > 0) { LimitCostToCap(); }
        }

        //Takes a tile away the way scavengers do: contents out of their container, terrain removed, doors and wall-like things
        //replaced with cheap ones to keep the walls closed. Returns a log message.
        private string RemoveTile(Tile tile) {
            if (tile is ItemTile contentTile && containerOf.ContainsKey(contentTile)) {
                TakeOut(contentTile);
                totalRemovedTiles++;
                return "Taken out of a container.";
            }
            if (tile is TerrainTile) {
                blueprint.terrainMap[tile.location.x, tile.location.z] = null;
                totalRemovedTerrains++;
                totalCost -= tile.cost;
                return "Terrain removed.";
            }

            string msg = "";
            ItemTile itemTile = tile as ItemTile;
            totalCost -= itemTile.cost;
            blueprint.itemsMap[tile.location.x, tile.location.z].Remove(itemTile);
            removedItems.Add(itemTile);
            if (itemTile.isDoor) { //if door is removed it should be replaced with another door, raiders are very polite and always replace expensive doors with cheaper ones.
                if (Rand.Chance(0.8f)) { //ok, not always.
                    ItemTile replacementTile = ItemTile.DefaultDoorItemTile(itemTile.location);
                    blueprint.itemsMap[tile.location.x, tile.location.z].Add(replacementTile);
                    msg += "Added " + replacementTile.defName + ", original ";
                    totalReplacedTiles++;
                } else {
                    totalRemovedTiles++;
                    blueprint.RemoveWall(itemTile.location.x, itemTile.location.z);
                }
            } else if (itemTile.isWall) { //if something like a wall removed (vent or aircon) you usually want to cover the hole to keep wall integrity
                ItemTile replacementTile = ItemTile.WallReplacementItemTile(itemTile.location);
                blueprint.itemsMap[tile.location.x, tile.location.z].Add(replacementTile);
                msg += "Added " + replacementTile.defName + ", original ";
                totalReplacedTiles++;
            } else {
                totalRemovedTiles++;
            }
            return msg + "Tile removed.";
        }

        //The map's ruins wealth limit: this blueprint got more than its share, so the most valuable things go first until it fits.
        //Unlike raids it ignores weight: taking a few expensive things keeps the ruin looking as it was.
        private void CutToAllowance(float allowance) {
            Debug.Log(Debug.BlueprintTransfer, "Cutting cost of {0} to the map wealth allowance of {1}", totalCost, allowance);
            //candidates come from the blueprint, not from tilesByCost: raids pop tiles they don't take (the "too cheap per kg" one ending a raid),
            //and heavy expensive things like ship beams are exactly those
            var candidates = new List<Tile>(containerOf.Keys);
            for (int x = 0; x < blueprint.width; x++) {
                for (int z = 0; z < blueprint.height; z++) {
                    if (blueprint.terrainMap[x, z] != null) candidates.Add(blueprint.terrainMap[x, z]);
                    candidates.AddRange(blueprint.itemsMap[x, z]);
                }
            }

            int removed = 0;
            foreach (Tile tile in candidates.OrderByDescending(OwnCost).ToList()) {
                if (totalCost <= allowance) break;
                if (OwnCost(tile) <= 0) break;
                if (tile is ItemTile item && IsGone(item)) continue;
                RemoveTile(tile);
                removed++;
            }
            Debug.Log(Debug.BlueprintTransfer, "Removed {0} tiles, resulting cost is {1}", removed, totalCost);
        }

        private void LimitCostToCap() {
            Debug.Log(Debug.BlueprintTransfer, "Capping current cost of {0} to target cost of {1}", totalCost, options.costCap);

            var initialCount = tilesByCost.Count;

            tilesByCost.RemoveAll(tile => tile is ItemTile item && IsGone(item));
            var filteredItems = tilesByCost.Where(item => OwnCost(item) > 20).ToList();
            var filteredCost = 0f;
            filteredItems.ForEach(tile => filteredCost += OwnCost(tile));

            if (filteredItems.Count == 0 || totalCost - filteredCost > options.costCap) {
                filteredItems = tilesByCost;
            }

            Debug.Log(Debug.BlueprintTransfer, "TilesByCost: {0} items, filteredTiles: {1} items of cost {2}", tilesByCost.Count, filteredItems.Count, filteredCost);

            while (filteredItems.Count > 0 && totalCost > options.costCap) {
                var tile = filteredItems.RandomElement();
                filteredItems.Remove(tile);
                if (tile is ItemTile maybeContent) {
                    if (IsGone(maybeContent)) continue;
                    if (containerOf.ContainsKey(maybeContent)) {
                        TakeOut(maybeContent);
                        continue;
                    }
                }
                totalCost -= tile.cost;
                if (tile is ItemTile) {
                    var itemTile = tile as ItemTile;
                    if (itemTile.isWall && !itemTile.isDoor) {
                        itemTile.defName = "Wall";
                        itemTile.stuffDef = "Wood";
                    } else if (itemTile.isDoor) {
                        itemTile.defName = "Door";
                        itemTile.stuffDef = "Wood";
                    } else { 
                        blueprint.itemsMap[tile.location.x, tile.location.z].Remove(tile as ItemTile);
                        removedItems.Add(itemTile);
                    }
                }
                if (tile is TerrainTile) {
                    blueprint.terrainMap[tile.location.x, tile.location.z] = null;
                }
            }

            Debug.Log(Debug.BlueprintTransfer, "Done. Resulting cost is {0}. Items left: {1}/{2}", totalCost, tilesByCost.Count, initialCount);
        }

        //Adds a container's contents as separate candidates. Things with no value (corpses, pawns, missing defs) are left inside.
        private void AddContents(ItemTile container) {
            if (container.innerItems == null) return;
            foreach (ItemTile inner in container.innerItems) {
                if (inner.cost <= 0) continue;
                containerOf[inner] = container;
                tilesByCost.Add(inner);
                AddContents(inner);
            }
        }

        //Value of the tile itself, without the contents it still holds
        private static float OwnCost(Tile tile) {
            return tile is ItemTile item ? item.OwnCost : tile.cost;
        }

        //Removed itself, or its container (at any level) was carried away whole
        private bool IsGone(ItemTile item) {
            for (ItemTile current = item; current != null; current = containerOf.TryGetValue(current, out ItemTile container) ? container : null) {
                if (removedItems.Contains(current)) return true;
            }
            return false;
        }

        //Takes a thing out of its container; every container it was in becomes cheaper by its value.
        //A minified thing left empty is not spawned at all (BlueprintTransferUtility), as there is nothing left in the crate.
        private void TakeOut(ItemTile item) {
            ItemTile container = containerOf[item];
            container.innerItems.Remove(item);
            removedItems.Add(item);
            for (ItemTile current = container; current != null; current = containerOf.TryGetValue(current, out ItemTile up) ? up : null) {
                current.cost -= item.cost;
            }
            totalCost -= item.cost;
        }
    }

}
