using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Verse;
using RimWorld;
using RimWorld.Planet;

namespace RealRuins {
    class ThoughtWorker_ScavengingRuins : ThoughtWorker {
        public const float MaxForUpset = 10000;
        public const float MinForMedium = 25000;
        public const float MinForHigh = 100000;
        public const float MinForVeryHigh = 500000;
        public const float MinForExtreme = 1000000;

        protected override ThoughtState CurrentStateInternal(Pawn p) {

            RuinedBaseComp comp = (p.Map?.Parent as MapParent)?.GetComponent<RuinedBaseComp>();
            if (comp == null) return ThoughtState.Inactive;
            if (!comp.isActive) return ThoughtState.Inactive;

            if (comp.currentCapCost < MaxForUpset) return ThoughtState.ActiveAtStage(0);
            if (comp.currentCapCost > MinForExtreme) return ThoughtState.ActiveAtStage(4);
            if (comp.currentCapCost > MinForVeryHigh) return ThoughtState.ActiveAtStage(3);
            if (comp.currentCapCost > MinForHigh) return ThoughtState.ActiveAtStage(2);
            if (comp.currentCapCost > MinForMedium) return ThoughtState.ActiveAtStage(1);
            return ThoughtState.Inactive;
        }
    }
}
