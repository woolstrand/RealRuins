using RimWorld.Planet;
using Verse;

namespace RealRuins {
    public class RuinedBaseComp : WorldObjectComp {

        public string blueprintFileName;

        public override void PostExposeData() {
            base.PostExposeData();
            Scribe_Values.Look(ref blueprintFileName, "blueprintFileName", "");
        }
    }
}