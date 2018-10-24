using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;

using RimWorld;
using Verse;

namespace RealRuins
{

    class GenStep_ScatterRealRuins : GenStep_Scatterer {
        public override int SeedPart {
            get {
                return 74293945;
            }
        }


        protected override void ScatterAt(IntVec3 loc, Map map, int count = 1) {
            int floorRadius = Rand.Range(4, 12);
            new RuinsScatterer().ScatterRuinsAt(loc, map, floorRadius, Rand.Range(0, 3), Rand.Range(floorRadius, floorRadius * 2), Rand.Range(0,3), (Rand.Value * 0.8f) + 0.2f, Rand.Range(30, 300));
        }

        protected override bool CanScatterAt(IntVec3 loc, Map map) {
            return true;
        }
    }

    class GenStep_ScatterLargeRealRuins : GenStep_Scatterer {
        public override int SeedPart {
            get {
                return 74293946;
            }
        }


        protected override void ScatterAt(IntVec3 loc, Map map, int count = 1) {
            int floorRadius = Rand.Range(16, 32);
            new RuinsScatterer().ScatterRuinsAt(loc, map, floorRadius, Rand.Range(0, 5), Rand.Range(floorRadius, floorRadius * 2), Rand.Range(0, 3), (Rand.Value * 0.3f) + 0.7f, Rand.Range(10, 50));
        }

        protected override bool CanScatterAt(IntVec3 loc, Map map) {
            return true;
        }
    }
}
