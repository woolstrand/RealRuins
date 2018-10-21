using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;

using RimWorld;
using Verse;

namespace RealRuins
{

    class GenStep_ScatterRealRuins : GenStep_Scatterer
    {
        public override int SeedPart
        {
            get
            {
                return 74293945;
            }
        }


        protected override void ScatterAt(IntVec3 loc, Map map, int count = 1)
        {
            new RuinsScatterer().ScatterRuinsAt(loc, map);
        }

        protected override bool CanScatterAt(IntVec3 loc, Map map)
        {
            return true;
        }
    }
}
