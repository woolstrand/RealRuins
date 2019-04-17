using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Verse;


namespace RealRuins {
    class CoverageMap {
        private bool[,] coverageMap;

        public static CoverageMap EmptyCoverageMap(Map map) {
            CoverageMap cm = new CoverageMap();
            
            cm.coverageMap = new bool[map.Size.x, map.Size.z];
            return cm;
        }

        public bool isMarked(int x, int z) {
            return coverageMap[x, z];
        }

        public void Mark(int x, int z) {
            coverageMap[x, z] = true;
        }

        public void DebugPrint() {
            Debug.PrintBoolMap(coverageMap);
        }
    }
}
