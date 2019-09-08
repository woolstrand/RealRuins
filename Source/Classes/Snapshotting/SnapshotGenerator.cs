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
        private void EncodePawn(Pawn pawn, XmlWriter writer) {

            //Pawn and it's kind
            //Debug.Message("Writing pawn {0}", pawn.ToString());

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

            //Debug.Message("name ok");

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

            //Debug.Message("gender age story ok");

            //apparel
            if (pawn.apparel != null) {
                if (pawn.apparel.WornApparelCount > 0) {
                    Debug.Log(Debug.BlueprintGen, "starting apparel");
                    writer.WriteStartElement("apparel");
                    foreach (Apparel apparel in pawn.apparel.WornApparel) {
                        Debug.Log(Debug.BlueprintGen, "Trying {0}", apparel);
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
            //Debug.Message("Finished apparel");

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
            //Debug.Message("health skills ok");

            writer.WriteEndElement();

            writer.Flush();
        }

        private void EncodeCorpse(Corpse corpse, XmlWriter writer) {
            writer.WriteStartElement("item");
            writer.WriteAttributeString("def", "corpse");
            writer.WriteAttributeString("timeOfDeath", corpse.timeOfDeath.ToString());
            EncodePawn(corpse.InnerPawn, writer);
            writer.WriteEndElement();
        }

        private void EncodeThing(Thing thing, XmlWriter writer) {

            if (thing is Corpse) {
                EncodeCorpse((Corpse)thing, writer);
                return;
            } else if (thing is Pawn) {
                EncodePawn((Pawn)thing, writer);
                return;
            }

            //Use only buildings and items. Ignoring pawns, trees, filth and so on
            if (thing.def.category == ThingCategory.Building || thing.def.category == ThingCategory.Item) {
                if (thing.def.building != null && thing.def.building.isNaturalRock) return; //ignoring natural rocks too

                Type CompTextClass = Type.GetType("SaM.CompText, Signs_and_Memorials");


                if (thing.HitPoints > thing.MaxHitPoints * 0.9f) {
                    maxHPItemsCount++;
                }
                itemsCount++;

                writer.WriteStartElement("item");
                writer.WriteAttributeString("def", thing.def.defName);

                if (thing.Stuff != null) {
                    writer.WriteAttributeString("stuffDef", thing.Stuff.defName);
                }

                if (thing.stackCount > 1) {
                    writer.WriteAttributeString("stackCount", thing.stackCount.ToString());
                }

                if (thing.Rotation != null) {
                    writer.WriteAttributeString("rot", thing.Rotation.AsByte.ToString());
                }

                CompArt a = thing.TryGetComp<CompArt>();
                if (a != null && a.Active) {
                    writer.WriteAttributeString("artAuthor", a.AuthorName);
                    writer.WriteAttributeString("artTitle", a.Title);
                    writer.WriteAttributeString("artDescription", a.GenerateImageDescription());
                }

                ThingWithComps thingWithComps = thing as ThingWithComps;
                if (thingWithComps != null && CompTextClass != null) {
                    Object textComp = null;
                    for (int i = 0; i < thingWithComps.AllComps.Count; i++) {
                        var val = thingWithComps.AllComps[i];
                        if (val.GetType() == CompTextClass) {
                            textComp = val;
                        }
                    }
                    if (textComp != null) {
                        string text = (string)(textComp?.GetType()?.GetField("text")?.GetValue(textComp));
                        if (text != null) {
                            writer.WriteAttributeString("text", text);
                        }
                    }
                }

                if (thing.def.passability == Traversability.Impassable || thing.def.fillPercent > 0.99) {
                    writer.WriteAttributeString("actsAsWall", "1");
                }

                if (thing.def.IsDoor) {
                    writer.WriteAttributeString("isDoor", "1");
                }

                if (thing is IThingHolder) {
                    ThingOwner innerThingsOwner = ((IThingHolder)thing).GetDirectlyHeldThings();
                    foreach (Thing t in innerThingsOwner) {
                        EncodeThing(t, writer);
                    }
                }
                writer.WriteEndElement();
            }
        }

    public string Generate() {

            StringBuilder builder = new StringBuilder();
            XmlWriterSettings settings = new XmlWriterSettings();
            settings.OmitXmlDeclaration = true;
            settings.ConformanceLevel = ConformanceLevel.Fragment;
            XmlWriter writer = XmlWriter.Create(builder, settings);

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

            
            writer.WriteStartElement("snapshot");
            writer.WriteAttributeString("version", typeof(RealRuins).Assembly.GetName().Version.ToString());
            writer.WriteAttributeString("x", originX.ToString());
            writer.WriteAttributeString("z", originZ.ToString());
            writer.WriteAttributeString("width", width.ToString());
            writer.WriteAttributeString("height", height.ToString());
            writer.WriteAttributeString("biomeDef", map.Biome.defName);
            writer.WriteAttributeString("mapSize", map.Size.x.ToString()); //hope maps are square all the time
            writer.WriteAttributeString("inGameYear", (Find.TickManager.StartingYear + Find.TickManager.TicksGame / 3600000).ToString());

            writer.WriteStartElement("world");
            writer.WriteAttributeString("seed", Find.World.info.seedString);
            writer.WriteAttributeString("tile", map.Parent.Tile.ToString());
            writer.WriteAttributeString("gameId", Math.Abs(Find.World.info.persistentRandomValue).ToString());
            writer.WriteAttributeString("percentage", Find.World.info.planetCoverage.ToString());
            writer.WriteEndElement();

            for (int z = rect.minZ; z < rect.maxZ; z++) {
                for (int x = rect.minX; x < rect.maxX; x++) {



                    IntVec3 cellVec = new IntVec3(x, 0, z);
                    List<Thing> things = map.thingGrid.ThingsListAt(cellVec);
                    TerrainDef terrain = map.terrainGrid.TerrainAt(cellVec);
                    RoofDef roof = map.roofGrid.RoofAt(cellVec);

                    if (roof == null && terrain.BuildableByPlayer == false && things.Count == 0) continue; //skip node creation if there is nothing to fill the node with

                    writer.WriteStartElement("cell");
                    writer.WriteAttributeString("x", (x - xmin).ToString());
                    writer.WriteAttributeString("z", (z - zmin).ToString());


                    if (terrain.BuildableByPlayer) {
                        writer.WriteStartElement("terrain");
                        writer.WriteAttributeString("def", terrain.defName);
                        writer.WriteEndElement();
                        terrainCount++;
                    }



                    if (roof != null) {
                        writer.WriteElementString("roof", "");
                    }

                    if (things.Count > 0) {
                        foreach (Thing thing in things) {
                            if (!(thing is Pawn) && thing.Position.Equals(cellVec)) {//ignore alive pawns, store multicell object only if we're looking at it's origin point.
                                EncodeThing(thing, writer);
                            }
                        }
                    }

                    writer.WriteEndElement();
                }
            }

            writer.WriteEndElement();
            
            float density = ((float) (itemsCount + terrainCount)) / ((xmax - xmin) * (zmax - zmin));
            if (density < 0.01) {
                //too low density of user generated things.
                Debug.Log(Debug.BlueprintGen, "Too low ruins density: {0}. Ignoring.", density);
                return null;
            }
            if (maxHPItemsCount < itemsCount * 0.25f) {
                //items count with maxhp less than 25% means VERY worn base. It's very likely this base is just a bunch of claimed ruins, and we want to prevent recycling ruins as is
                Debug.Log(Debug.BlueprintGen, "Too low ruins average HP. Ignoring.");
                return null;
            }

            

            string tmpPath = Path.GetTempFileName();
            File.WriteAllText(tmpPath, builder.ToString());
            Debug.Log(Debug.BlueprintGen, "Capture finished successfully and passed all checks. ({0})", tmpPath);

            return tmpPath;
        }
    }
}
