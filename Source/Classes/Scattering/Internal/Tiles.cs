using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;


using RimWorld;
using Verse;

namespace RealRuins {
    //stores information about terrain in the blueprint
    class Tile {
        public string defName;
        public float cost = 0.0f; //populated later
        public float weight = 1.0f;
        public IntVec3 location;
    }

    class TerrainTile : Tile {
        public TerrainTile(XmlNode node) {
            defName = node.Attributes["def"].Value;
        }
    }

    //stores information about item in the blueprint
    class ItemTile : Tile {
        public string stuffDef;
        public int stackCount;
        public long corpseDeathTime;
        public int rot;
        public bool isWall = false; //is a wall or something as tough and dense as a wall. actually this flag determines if this item can be replaced with a wall if it's impossible to use the original.
        public bool isDoor = false;

        public ItemArt art = null;
        public string attachedText = null;

        public List<ItemTile> innerItems;

        public string itemXml;

        static public ItemTile WallReplacementItemTile() {
            return WallReplacementItemTile(new IntVec3(0, 0, 0));
        }

        public static ItemTile WallReplacementItemTile(IntVec3 location) {
            ItemTile tile = new ItemTile {
                defName = ThingDefOf.Wall.defName,
                stuffDef = ThingDefOf.BlocksGranite.defName,
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

        public override string ToString() {
            string ofWhat = "";
            string cnt = "";
            if (stuffDef != null) ofWhat = " of " + stuffDef;
            if (stackCount > 1) cnt = " (" + stackCount + " pcs)";
            string result = "Tile \"" + defName + "\"" + ofWhat + cnt + ", $" + cost + ", " + weight + "kg";
            if (isWall) result += ", wall";
            if (isDoor) result += ", door";
            return result;
        }

        public ItemTile() {
        }

        public ItemTile(XmlNode node) {
            defName = node.Attributes["def"].Value;
            itemXml = node.OuterXml;

            XmlAttribute stuffDefAttribute = node.Attributes["stuffDef"];
            if (stuffDefAttribute != null) {
                stuffDef = stuffDefAttribute.Value;
            }

            corpseDeathTime = long.Parse(node.Attributes["timeOfDeath"]?.Value ?? "0");

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

            XmlAttribute textAttribute = node.Attributes["text"];
            if (textAttribute != null) {
                attachedText = textAttribute.Value;
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

            XmlAttribute artTitleAttribute = node.Attributes["artTitle"];
            if (artTitleAttribute != null) {
                if (art == null) art = new ItemArt();
                art.title = Uri.UnescapeDataString(artTitleAttribute.Value);
            }

            XmlAttribute artAuthorAttribute = node.Attributes["artAuthor"];
            if (artAuthorAttribute != null) {
                if (art == null) art = new ItemArt();
                art.author = Uri.UnescapeDataString(artAuthorAttribute.Value);
            }

            XmlAttribute artDescriptionAttribute = node.Attributes["artDescription"];
            if (artDescriptionAttribute != null) {
                if (art == null) art = new ItemArt();
                art.text = Uri.UnescapeDataString(artDescriptionAttribute.Value);
            }

            if (node.HasChildNodes) {
                List<ItemTile> innerItems = new List<ItemTile>();
                foreach (XmlNode childNode in node.ChildNodes) {
                    if (childNode.Name.Equals("item")) {
                        ItemTile innerTile = new ItemTile(childNode);
                        innerItems.Add(innerTile);
                    }
                }

                if (innerItems.Count > 0) {
                    this.innerItems = innerItems;
                }
            }
        }
    }
}
