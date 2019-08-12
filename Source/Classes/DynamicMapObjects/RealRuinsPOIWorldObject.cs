using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using RimWorld.Planet;
using UnityEngine;
using RimWorld;
using Verse;

namespace RealRuins {
    class RealRuinsPOIWorldObject : MapParent {

        public override Texture2D ExpandingIcon => ContentFinder<Texture2D>.Get("poi-" + GetComponent<RealRuinsPOIComp>().poiType);
        public override Color ExpandingIconColor => Faction?.Color ?? Color.white;
        private Material cachedMat;

        public override Material Material {
            get {
                if (cachedMat == null) {
                    cachedMat = MaterialPool.MatFrom(color: Faction?.Color ?? Color.white, texPath: "World/WorldObjects/Sites/GenericSite", shader: ShaderDatabase.WorldOverlayTransparentLit, renderQueue: WorldMaterials.WorldObjectRenderQueue);
                }
                return cachedMat;
            }
        }

        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Caravan caravan) {
            foreach (FloatMenuOption floatMenuOption in base.GetFloatMenuOptions(caravan)) {
                yield return floatMenuOption;
            }
            foreach (FloatMenuOption floatMenuOption in CaravanArrivalAction_VisitRealRuinsPOI.GetFloatMenuOptions(caravan, this)) {
                yield return floatMenuOption;
            }
        }


    }
}
