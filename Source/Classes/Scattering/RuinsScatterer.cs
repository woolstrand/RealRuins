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

    class RuinsScatterer
    {

        // Clear the cell from other destroyable objects
        private bool ClearCell(IntVec3 location, Map map)
        {
            List<Thing> items = map.thingGrid.ThingsListAt(location);
            if (!CanClearCell(location, map)) return false;
            for (int index = items.Count - 1; index >= 0; index --) {
                items[index].Destroy(DestroyMode.Vanish);
            }
            return true;
        }

        // Clear the cell from other destroyable objects
        private bool CanClearCell(IntVec3 location, Map map)
        {
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

        object SL(object o)
        {
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

        private void Message(string format, params object[] args)
        {
            string message = string.Format(format, args);
            Log.Message(message, true);
        }

        //Deterioration degree is unconditional modifier of destruction applied to the ruins bluepring. Degree of 0.5 means that in average each 2nd block in "central" part will be destroyed.
        //Scavenge threshold is an item price threshold after which the item or terrain is most likely scavenged.
        public void ScatterRuinsAt(IntVec3 loc, Map map, int wallRadius = 6, int wallRadiusJitter = 2, int floorRadius = 7, int floorRadiusJitter = 2, float deteriorationDegree = 0.5f, float scavengeThreshold = 50.0f)
        {
            Message("Scattering ruins at ({0}, {1})", loc.x, loc.z);

            //Create the XmlDocument.  
            XmlDocument snapshot = new XmlDocument();

            Log.Message("Loading file...");
            snapshot.Load("C:\\temp\\jeluder.txt");

            XmlNodeList elemList = snapshot.GetElementsByTagName("cell");
            int blueprintWidth = int.Parse(snapshot.FirstChild.Attributes["width"].Value);
            int blueprintHeight = int.Parse(snapshot.FirstChild.Attributes["height"].Value);

            TerrainTile[,] terrainMap = new TerrainTile[blueprintWidth, blueprintHeight];
            bool[,] roofMap = new bool[blueprintWidth, blueprintHeight];
            List<ItemTile>[,] itemsMap = new List<ItemTile>[blueprintWidth, blueprintHeight];


            //base deterioration chance mask. will be used in future to create freeform deterioration which is much more fun than just circular
            //deterioration change also depends on material and cost, but is always based on base chance
            float[,] terrainIntegrity = new float[blueprintWidth, blueprintHeight]; //integrity of floor tiles
            float[,] itemsIntegrity = new float[blueprintWidth, blueprintHeight]; //base integrity of walls, roofs and items

            foreach (XmlNode cellNode in elemList) {
                int x = int.Parse(cellNode.Attributes["x"].Value);
                int z = int.Parse(cellNode.Attributes["z"].Value);
                itemsMap[x, z] = new List<ItemTile>();

                foreach (XmlNode cellElement in cellNode.ChildNodes) {
                    if (cellElement.Name.Equals("terrain")) {
                        terrainMap[x, z] = new TerrainTile(cellElement);
                    } else if (cellElement.Name.Equals("item")) {
                        itemsMap[x, z].Add(new ItemTile(cellElement));
                    } else if (cellElement.Name.Equals("roof")) {
                        roofMap[x, z] = true;
                    }
                }
            }

            Message("File read and prepared for inserting.");

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
                        Message("Processing items on tile {0}, {1}", mapLocation.x, mapLocation.z);
                        foreach (ItemTile itemTile in itemsMap[x, z]) {

                            ThingDef thingDef = ThingDef.Named(itemTile.defName);
                            ThingDef stuffDef = null;
                            if (itemTile.stuffDef != null) {
                                stuffDef = ThingDef.Named(itemTile.stuffDef);
                            }



                            //check if can be built on top of existing terrain
                            if (thingDef != null) {
                                if (thingDef.terrainAffordanceNeeded != null && !map.terrainGrid.TerrainAt(mapLocation).affordances.Contains(thingDef.terrainAffordanceNeeded)) {
                                    Message("Terrain can't fullfill requirements: {0}", thingDef.terrainAffordanceNeeded);
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

                                Message("Spawning {0} of {1} (stack of {2}). cost is {3}. chance is {4}", thingDef, (stuffDef!=null)?stuffDef.defName:"<NULL>", itemTile.stackCount, cost, spawnThreshold);

                                float spawnChance = Rand.Value;
                                if (spawnChance > spawnThreshold * 1.5) {
                                    Message("Did not spawn");
                                    continue; //item deteriorated/scavenged completely
                                } else { 
                                    //otherwise there is a chance that player will get some leftovers
                                    if (!cellIsAlreadyCleared) { //first item to be spawned should also clear place for itself. we can't do it beforehand because we don't know it it will be able and get a chance to be spawned.
                                        if (!ClearCell(mapLocation, map)) {
                                            Message("Could not clear cell");
                                            break; //if cell was not cleared successfully -> break things placement cycle and move on to the next item
                                        } else {
                                            cellIsAlreadyCleared = true;
                                        }
                                    }

                                    if (!thingDef.BuildableByPlayer) {
                                        if (spawnChance > spawnThreshold) {
                                            Message("Did not spawn");
                                            continue; //no leftovers for items, only for buildings
                                        }
                                    }

                                    Thing thing = ThingMaker.MakeThing(thingDef, stuffDef);


                                    if (thing != null) {
                                        GenSpawn.Spawn(thing, mapLocation, map, new Rot4(itemTile.rot));
                                        if (itemTile.stackCount > 1) {
                                            thing.stackCount = Rand.Range(1, itemTile.stackCount);

                                            //Spoil things that can be spoiled. You shouldn't find a fresh meat an the old ruins.
                                            CompRottable rottable = thing.TryGetComp<CompRottable>();
                                            if (rottable != null) {
                                                //if deterioration degree is > 0.5 you won't find any food.
                                                rottable.RotProgress = (Rand.Value * 0.5f + deteriorationDegree) * (rottable.PropsRot.TicksToRotStart);
                                            }
                                        }

                                        thing.HitPoints = Rand.Range(1, thing.def.BaseMaxHitPoints);
                                        if (thing.def.EverHaulable) {
                                            thing.SetForbidden(true, false);
                                        }

                                        if (spawnChance > spawnThreshold) {
                                            Message("Spawned and destroyed");
                                            thing.Destroy(DestroyMode.KillFinalize);
                                        } else {
                                            Message("Spawned!");
                                        }



                                        didAlterCell = true;
                                    }
                                }
                            }
                        }
                    }

                    //Add some generic filth to floor. TODO: add optional rotten bodies, blood trails, vomit, whatever
                    if (didAlterCell && map.terrainGrid.TerrainAt(mapLocation).acceptFilth) {

                        ThingDef[] filthDef = { ThingDefOf.Filth_Dirt, ThingDefOf.Filth_RubbleBuilding, ThingDefOf.Filth_RubbleRock, ThingDefOf.Filth_Trash, ThingDefOf.Filth_Ash };
                        while (Rand.Value > 0.5) {
                            FilthMaker.MakeFilth(mapLocation, map, filthDef[Rand.Range(0, 4)], Rand.Range(1, 5));
                        }
                    }

                    if (Rand.Value < 0.1) {
                        PawnGenerationRequest request = new PawnGenerationRequest(PawnKindDefOf.WildMan, null, PawnGenerationContext.NonPlayer, -1, false, false, false, false, true, false, 20f, false, true, true, false, false, false, false, null, null, null, null, null, null, null, null);
                        Pawn dweller = PawnGenerator.GeneratePawn(request);
                        GenSpawn.Spawn(dweller, mapLocation, map);
                        dweller.Kill(null);
                        CompRottable rottable = dweller.Corpse.TryGetComp<CompRottable>();
                        rottable.RotProgress = rottable.PropsRot.TicksToDessicated + Rand.Value * 1000000;
                        dweller.Corpse.timeOfDeath = -1304005;
                        Message("Time of death: {0}", dweller.Corpse.timeOfDeath);
                    }

                }
            }
        }
    }
}
