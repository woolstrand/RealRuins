using RimWorld.Planet;
using Verse;

namespace RealRuins {
    [StaticConstructorOnStartup]
    public class SmallRuinsWorldObject : MapParent {
        public override bool ShouldRemoveMapNow(out bool alsoRemoveWorldObject)
        {
            if (!base.Map.mapPawns.AnyPawnBlockingMapRemoval)
            {
                alsoRemoveWorldObject = true;
                return true;
            }
            alsoRemoveWorldObject = false;
            return false;
        }

        public override string GetInspectString() {
            return "RealRuins.SmallRuinsInspectString".Translate();
        }
    }
}