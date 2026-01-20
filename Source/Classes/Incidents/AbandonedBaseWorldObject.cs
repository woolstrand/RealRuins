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
        public override bool GravShipCanLandOn => true;
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

        public override IEnumerable<FloatMenuOption> GetTransportersFloatMenuOptions(IEnumerable<IThingHolder> pods, Action<PlanetTile, TransportersArrivalAction> launchAction) { 
            foreach (FloatMenuOption transportPodsFloatMenuOption in base.GetTransportersFloatMenuOptions(pods, launchAction)) {
                yield return transportPodsFloatMenuOption;
            }
            foreach (FloatMenuOption floatMenuOption in TransportPodsArrivalAction_VisitRuins.GetFloatMenuOptions(launchAction, pods, this)) {
                yield return floatMenuOption;
            }
        }

        public override IEnumerable<Gizmo> GetGizmos() {
            foreach (Gizmo gizmo in base.GetGizmos()) {
                yield return gizmo;
            }
            
            // Debug button to test empty signal triggering
            if (Prefs.DevMode) {
                var comp = this.GetComponent<RuinedBaseComp>();
                if (comp != null) {
                    Command_Action debugEmptySignal = new Command_Action {
                        defaultLabel = "[DEBUG] Send Empty Signal",
                        defaultDesc = "Test if sending an empty signal triggers 'are leaving' message",
                        action = delegate {
                            Messages.Message("Sending empty signal for testing...", MessageTypeDefOf.NeutralEvent);
                            Find.SignalManager.SendSignal(new Signal(""));
                        }
                    };
                    yield return debugEmptySignal;
                    
                    Command_Action debugNullSignal = new Command_Action {
                        defaultLabel = "[DEBUG] Send Null Signal",
                        defaultDesc = "Test if sending a null signal triggers 'are leaving' message",
                        action = delegate {
                            Messages.Message("Sending null signal for testing...", MessageTypeDefOf.NeutralEvent);
                            Find.SignalManager.SendSignal(new Signal(null));
                        }
                    };
                    yield return debugNullSignal;
                    
                    Command_Action debugShowSignals = new Command_Action {
                        defaultLabel = "[DEBUG] Show Signal Info",
                        defaultDesc = string.Format("successSignal: '{0}', expireSignal: '{1}'", 
                            comp.successSignal ?? "NULL", 
                            comp.expireSignal ?? "NULL"),
                        action = delegate {
                            Messages.Message(string.Format("successSignal: '{0}' (null/empty: {1})\nexpireSignal: '{2}' (null/empty: {3})", 
                                comp.successSignal ?? "NULL",
                                comp.successSignal.NullOrEmpty(),
                                comp.expireSignal ?? "NULL",
                                comp.expireSignal.NullOrEmpty()), MessageTypeDefOf.NeutralEvent);
                        }
                    };
                    yield return debugShowSignals;
                }
            }
        }

        public override bool ShouldRemoveMapNow(out bool alsoRemoveWorldObject) {
            bool shouldRemove = !Map.mapPawns.AnyPawnBlockingMapRemoval;
            alsoRemoveWorldObject = shouldRemove;
            if (shouldRemove) {
                var comp = this.GetComponent<RuinedBaseComp>();
                if (comp != null) {
                    var signalTag = comp.successSignal;
                    // Only send signal if it's not null or empty to avoid triggering unintended Lord jobs
                    // Signals are global and can match Lords from other quests/raids, causing "are leaving" messages
                    if (!signalTag.NullOrEmpty()) {
                        Debug.Log("Quest", "Sending success signal: {0}", signalTag);
                        Find.SignalManager.SendSignal(new Signal(signalTag));
                    }
                }
            }
            return shouldRemove;
        }

        public override string GetInspectString() {
            return base.GetInspectString();
        }
    }
}
