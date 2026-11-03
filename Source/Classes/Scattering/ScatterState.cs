using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Verse;

namespace RealRuins {
    // Runtime data of a single scattering run. Created fresh for every run, never persisted.
    class ScatterState {
        public int[,] roomMap; // Room traversal map, in blueprint coordinates.
        public float uncoveredCost = 0; // Cost not yet covered by defenders or raid triggers.
        public float transferredCost = 0; // Cost of what was actually transferred to the map.
        public CellRect placementRect; // Actual map rect the blueprint was placed into.
        public RuinsWealthReport wealthReport; // Debug wealth measurement of the transfer, only when the wealth log is on.
    }
}
