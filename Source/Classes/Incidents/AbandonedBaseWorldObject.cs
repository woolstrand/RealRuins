using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Verse;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;

namespace RealRuins {
    class AbandonedBaseWorldObject: MapParent {
        public override Texture2D ExpandingIcon => ContentFinder<Texture2D>.Get("ruinedbase");
        public override Color ExpandingIconColor => Color.white;
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

        public override void Tick() {
            base.Tick();
            if (HasMap) {
                if (!GenHostility.AnyHostileActiveThreatToPlayer(Map)) {
                    if (Faction != Faction.OfPlayer) {
                        SetFaction(Faction.OfPlayer);
                    }
                } else {
                    if (Faction == Faction.OfPlayer) {
                        SetFaction(null); //reset faction to forbid fast caravan reform and add green border
                    }
                }
            }
        }

        public override bool ShouldRemoveMapNow(out bool alsoRemoveWorldObject) {
            bool shouldRemove = !Map.mapPawns.AnyPawnBlockingMapRemoval;
            alsoRemoveWorldObject = shouldRemove;
            return shouldRemove;
        }

        public override string GetInspectString() {
            StringBuilder builder = new StringBuilder();
            builder.Append(base.GetInspectString());
            if (builder.Length > 0) {
                builder.AppendLine();
            }
            builder.AppendLine("RealRuins.PristineRuinsWolrdObject".Translate());

            var comp = GetComponent<RuinedBaseComp>();
            if (comp != null) {
                builder.AppendLine(comp.CompInspectStringExtra());
            }

            return builder.ToString().TrimEndNewlines();
        }
    }
}
