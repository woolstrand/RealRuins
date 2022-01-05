using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Verse;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;

namespace RealRuins {
    [StaticConstructorOnStartup]
    class AbandonedBaseWorldObject: MapParent {
        public override Texture2D ExpandingIcon => ContentFinder<Texture2D>.Get("ruinedbase");
        public override Color ExpandingIconColor => Color.white;
        private Material cachedMat;

        private bool hasStartedCountdown = false;

        public override Material Material {
            get {
                if (cachedMat == null) {
                    cachedMat = MaterialPool.MatFrom(color: Faction?.Color ?? Color.white, texPath: "World/WorldObjects/Sites/GenericSite", shader: ShaderDatabase.WorldOverlayTransparentLit, renderQueue: WorldMaterials.WorldObjectRenderQueue);
                }
                return cachedMat;
            }
        }

        public AbandonedBaseWorldObject() {
        }

        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Caravan caravan) {
            foreach (FloatMenuOption floatMenuOption in base.GetFloatMenuOptions(caravan)) {
                yield return floatMenuOption;
            }
            foreach (FloatMenuOption floatMenuOption in CaravanArrivalAction_VisitAbandonedBase.GetFloatMenuOptions(caravan, this)) {
                yield return floatMenuOption;
            }
        }

        public override IEnumerable<FloatMenuOption> GetTransportPodsFloatMenuOptions(IEnumerable<IThingHolder> pods, CompLaunchable representative) {
            foreach (FloatMenuOption transportPodsFloatMenuOption in base.GetTransportPodsFloatMenuOptions(pods, representative)) {
                yield return transportPodsFloatMenuOption;
            }
            foreach (FloatMenuOption floatMenuOption in TransportPodsArrivalAction_VisitRuins.GetFloatMenuOptions(representative, pods, this)) {
                yield return floatMenuOption;
            }
            //foreach ()
        }

        public override IEnumerable<Gizmo> GetGizmos() {
            foreach (Gizmo gizmo in base.GetGizmos()) {
                yield return gizmo;
            }
        }

        public override void Tick() {
            base.Tick();
            if (HasMap && !hasStartedCountdown) {
                GetComponent<TimedForcedExit>().StartForceExitAndRemoveMapCountdown(15 * 60000);
                hasStartedCountdown = true;
            }
        }

        public override bool ShouldRemoveMapNow(out bool alsoRemoveWorldObject) {
            bool shouldRemove = !Map.mapPawns.AnyPawnBlockingMapRemoval;
            alsoRemoveWorldObject = shouldRemove;
            return shouldRemove;
        }

        public override string GetInspectString() {
            return base.GetInspectString();
        }
    }
}
