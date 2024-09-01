using System.Xml;

namespace RealRuins;

internal class TerrainTile : Tile
{
	public TerrainTile(XmlNode node)
	{
		defName = node.Attributes["def"].Value;
	}
}
