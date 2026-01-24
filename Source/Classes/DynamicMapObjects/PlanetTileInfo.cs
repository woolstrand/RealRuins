using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RealRuins
{
    class PlanetTileInfo
    {
        public string mapId;
        public int tile;
        public int tileLayer = 0;  // Default to 0 for backward compatibility
        public string biomeName;

        public int originX;
        public int originZ;
    }
}
