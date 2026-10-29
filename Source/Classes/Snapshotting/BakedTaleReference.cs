using RimWorld;
using Verse;

namespace RealRuins {
    // TaleReference.ExposeData is not virtual, so bakedTale is saved by TaleReference_ExposeData_Patch in RealRuins.cs.
    public class BakedTaleReference : TaleReference {
        public TaggedString bakedTale; //to allow access in original tale reference object's patched ExposeData method
        
        public BakedTaleReference() {
            bakedTale = "DeterioratedArtDescription".Translate();
        }

        public BakedTaleReference(string taleDescription) {
            //Debug.Message("Created baked tale reference");
            bakedTale = new TaggedString(taleDescription);
        }

        public BakedTaleReference(TaggedString taleDescription) {
            //Debug.Message("Created baked tale reference");
            bakedTale = taleDescription;
        }
    }
}