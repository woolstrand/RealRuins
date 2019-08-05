using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RealRuins {
    class RealRuinsPOIComp : WorldObjectComp {
        public string blueprintName = "";
        public string gameName = "";
        public int originX = 0;
        public int originZ = 0;

        public int poiType = 0;

        public override void Initialize(WorldObjectCompProperties props) {
            base.Initialize(props);
        }


        public override void PostExposeData() {
            base.PostExposeData();
            Scribe_Values.Look(ref blueprintName, "blueprintName", "");
            Scribe_Values.Look(ref gameName, "gameName", "");
            Scribe_Values.Look(ref originX, "originX", 0);
            Scribe_Values.Look(ref originZ, "originZ", 0);
            Scribe_Values.Look(ref poiType, "poiType", 0);
        }
    }


}
