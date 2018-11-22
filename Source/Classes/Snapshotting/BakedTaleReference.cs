using RimWorld;
using Verse;

namespace RealRuins {
    public class BakedTaleReference : TaleReference {
        public string bakedTale; //to allow access in original tale reference object's patched ExposeData method
        
        public BakedTaleReference() {
            bakedTale = "DeterioratedArtDescription".Translate();
        }

        public BakedTaleReference(string taleDescription) {
            //Debug.Message("Created baked tale reference");
            bakedTale = taleDescription;
        }



        public new void ExposeData()
        {
            Scribe_Values.Look(ref bakedTale, "bakedTale", "DeterioratedArtDescription".Translate(), false);
        }
        
 
    }
}