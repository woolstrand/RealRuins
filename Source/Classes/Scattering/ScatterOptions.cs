using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RealRuins {
    class ScatterOptions {
        float densityMultiplier;
        int referenceRadiusAverage;
        float deteriorationMultiplier;
        float scavengingMultiplier;

        int itemCostLimit;
        bool canSpawnItems;
        bool wallsDoorsOnly;

        float decorationChance; //probability PER CELL
        float trapChance; //probability PER CELL
        float hostileChance; //probability PER CHUNK
    }
}
