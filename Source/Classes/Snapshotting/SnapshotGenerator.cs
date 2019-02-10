using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Security;
using System.Xml;

using Verse;
using RimWorld;
using System.Reflection;

namespace RealRuins {

    class SnapshotGenerator {
        private Map map;

        int maxHPItemsCount = 0;
        int itemsCount = 0;
        int terrainCount = 0;


        public SnapshotGenerator(Map map) {
            this.map = map;
        }

        public bool CanGenerate() {
            if (map.areaManager.Home.ActiveCells.Count() < 300) return false;
            return true;
        }

        //Since RimWorlds save system is based on a singleton which does not support subclassing in a sane way, I can't use it for saving pawns.
        //Actually, I can use it for saving, but can't for loading. So I will use combination of native and custom way of saving pawn info
        //(to get rid of tons of unnecessary information like pawn records or pawn work schedule), and fully customized loading routines.
        private string EncodePawn(Pawn pawn) {
            StringBuilder builder = new StringBuilder();
            XmlWriterSettings settings = new XmlWriterSettings();
            settings.OmitXmlDeclaration = true;
            settings.ConformanceLevel = ConformanceLevel.Fragment;
            XmlWriter writer = XmlWriter.Create(builder, settings);

            //Pawn and it's kind
            Debug.Message("Writing pawn {0}", pawn.ToString());

            writer.WriteStartElement("item");
            writer.WriteAttributeString("def", "Pawn");
            writer.WriteElementString("kind", pawn.kindDef.defName);

            //Name - triple or single
            writer.WriteStartElement("name");
            if (pawn.Name is NameSingle) {
                writer.WriteAttributeString("first", ((NameSingle)pawn.Name).Name);
            } else if (pawn.Name is NameTriple) {
                writer.WriteAttributeString("first", ((NameTriple)pawn.Name).First);
                writer.WriteAttributeString("last", ((NameTriple)pawn.Name).Last);
                writer.WriteAttributeString("nick", ((NameTriple)pawn.Name).Nick);
            } else if (pawn.Name != null && pawn.Name.ToStringFull != null) {
                writer.WriteAttributeString("first", pawn.Name.ToStringFull);
            } else {
                writer.WriteAttributeString("first", "Unknown");
            }
            writer.WriteEndElement();

            Debug.Message("name ok");

            //gender
            writer.WriteElementString("gender", pawn.gender.ToString());

            //age
            writer.WriteElementString("biologicalAge", pawn.ageTracker.AgeBiologicalTicks.ToString());
            writer.WriteElementString("chronologicalAge", pawn.ageTracker.AgeBiologicalTicks.ToString());

            //story and appearance
            if (pawn.story != null) {
                string storyXml = Scribe.saver.DebugOutputFor(pawn.story);
                writer.WriteRaw(storyXml ?? "");
            }

            Debug.Message("gender age story ok");

            //apparel
            if (pawn.apparel != null) {
                if (pawn.apparel.WornApparelCount > 0) {
                    Debug.Message("starting apparel");
                    writer.WriteStartElement("apparel");
                    foreach (Apparel apparel in pawn.apparel.WornApparel) {
                        Debug.Message("Trying {0}", apparel);
                        string appDef = apparel.def?.defName;
                        string stuffDef = apparel.Stuff?.defName;
                        if (appDef != null) {
                            writer.WriteStartElement("item");
                            writer.WriteAttributeString("def", appDef);
                            if (stuffDef != null) {
                                writer.WriteAttributeString("stuffDef", stuffDef);
                            }
                            writer.WriteEndElement();
                        }
                    }
                    writer.WriteEndElement();
                }
            }
            Debug.Message("Finished apparel");

            //health
            if (pawn.health != null) {
                string healthXml = Scribe.saver.DebugOutputFor(pawn.health);
                writer.WriteRaw(healthXml ?? "");
            }

            //skills
            if (pawn.skills != null) {
                string skillsXml = Scribe.saver.DebugOutputFor(pawn.skills);
                writer.WriteRaw(skillsXml ?? "");
            }
            Debug.Message("health skills ok");

            writer.WriteEndElement();

            writer.Flush();
            return builder.ToString();

        }

        private string EncodeCorpse(Corpse corpse) {
            return "<item def=\"Corpse\" timeOfDeath=\"" + corpse.timeOfDeath.ToString() + "\">" + EncodePawn(corpse.InnerPawn) + "</item>";
        }

        private string EncodeThing(Thing thing) {

            if (thing is Corpse) {
                return EncodeCorpse((Corpse)thing);
            } else if (thing is Pawn) {
                return EncodePawn((Pawn)thing);
            }

            //Use only buildings and items. Ignoring pawns, trees, filth and so on
            if (thing.def.category == ThingCategory.Building || thing.def.category == ThingCategory.Item) {
                if (thing.def.building != null && thing.def.building.isNaturalRock) return null; //ignoring natural rocks too

                StringBuilder itemBuilder = new StringBuilder();

                if (thing.HitPoints > thing.MaxHitPoints * 0.9f) {
                    maxHPItemsCount++;
                }
                itemsCount++;

                itemBuilder.AppendFormat("\t\t<item def=\"{0}\"", thing.def.defName);

                if (thing.Stuff != null) {
                    itemBuilder.AppendFormat(" stuffDef=\"{0}\"", thing.Stuff.defName);
                }

                if (thing.stackCount > 1) {
                    itemBuilder.AppendFormat(" stackCount=\"{0}\"", thing.stackCount);
                }

                if (thing.Rotation != null) {
                    itemBuilder.AppendFormat(" rot=\"{0}\"", thing.Rotation.AsByte);
                }

                CompArt a = thing.TryGetComp<CompArt>();
                if (a != null && a.Active) {
                    itemBuilder.AppendFormat(" artAuthor=\"{0}\" artTitle=\"{1}\" artDescription=\"{2}\"", Uri.EscapeDataString(a.AuthorName), Uri.EscapeDataString(a.Title), Uri.EscapeDataString(a.GenerateImageDescription())); //not always a wall, but should act as a wall)
                }

                if (thing.def.passability == Traversability.Impassable || thing.def.fillPercent > 0.99) {
                    itemBuilder.Append(" actsAsWall=\"1\""); //not always a wall, but should act as a wall
                }

                if (thing.def.IsDoor) {
                    itemBuilder.Append(" isDoor=\"1\"");
                }

                if (thing is IThingHolder) {
                    ThingOwner innerThingsOwner = ((IThingHolder)thing).GetDirectlyHeldThings();
                    itemBuilder.Append(">\n");
                    foreach (Thing t in innerThingsOwner) {
                        itemBuilder.Append(EncodeThing(t));
                    }
                    itemBuilder.Append("</item>");
                } else {
                    itemBuilder.Append(" />\n");
                }

                return itemBuilder.ToString();
            } else {
                return null;
            }
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
            fileBuilder.AppendFormat("<snapshot version=\"{3}\" width=\"{0}\" height=\"{1}\" biomeDef=\"{2}\" inGameYear=\"{4}\">\n", width, height, map.Biome.defName, typeof(RealRuins).Assembly.GetName().Version, Find.TickManager.StartingYear + Find.TickManager.TicksGame / 3600000);
            fileBuilder.AppendFormat("<world seed=\"{0}\" tile=\"{1}\" gameId=\"{2}\"/>\n", Find.World.info.seedString, map.Parent.Tile, Math.Abs(Find.World.info.persistentRandomValue));

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
                        terrainCount++;
                    }



                    if (roof != null) {
                        if (!roof.isNatural) {
                            roofBuilder.AppendFormat("\t\t<roof />\n");
                        }
                    }

                    if (things.Count > 0) {
                        foreach (Thing thing in things) {
                            if (thing.Position.Equals(cellVec)) {//store multicell object only if we're looking at it's origin point.
                                string encoded = EncodeThing(thing);
                                if (encoded != null) {
                                    itemsBuilder.Append(encoded);
                                }
                            }
                        }
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
            
            float density = ((float) (itemsCount + terrainCount)) / ((xmax - xmin) * (zmax - zmin));
            if (density < 0.01) {
                //too low density of user generated things.
                Debug.Message("Too low ruins density: {0}. Ignoring.", density);
                return null;
            }
            if (maxHPItemsCount < itemsCount * 0.25f) {
                //items with maxhp less than 25% means VERY worn base. It's very likely this base is just a bunch of claimed ruins, and we want to prevent recycling ruins as is
                Debug.Message("Too low ruins average HP. Ignoring.");
                return null;
            }

            fileBuilder.Append("</snapshot>");

            string tmpPath = System.IO.Path.GetTempFileName();
            System.IO.File.WriteAllText(tmpPath, fileBuilder.ToString());

            return tmpPath;
        }
    }
}
