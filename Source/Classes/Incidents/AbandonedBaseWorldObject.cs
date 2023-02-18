using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Verse;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using RimWorld.QuestGen;

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
        }

        public override IEnumerable<Gizmo> GetGizmos() {
            foreach (Gizmo gizmo in base.GetGizmos()) {
                yield return gizmo;
            }
        }

        public override bool ShouldRemoveMapNow(out bool alsoRemoveWorldObject) {
            bool shouldRemove = !Map.mapPawns.AnyPawnBlockingMapRemoval;
            alsoRemoveWorldObject = shouldRemove;
            if (shouldRemove) {
                var comp = this.GetComponent<RuinedBaseComp>();
                if (comp != null) {
                    var signalTag = comp.successSignal;
                    Find.SignalManager.SendSignal(new Signal(signalTag));
                }
            }
            return shouldRemove;
        }

        public override string GetInspectString() {
            return base.GetInspectString();
        }
    }
}
