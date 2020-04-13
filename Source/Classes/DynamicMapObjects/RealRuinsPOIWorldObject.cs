using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using RimWorld.Planet;
using UnityEngine;
using RimWorld;
using Verse;

namespace RealRuins {
    [StaticConstructorOnStartup]
    class RealRuinsPOIWorldObject : MapParent {

        public override Texture2D ExpandingIcon => ContentFinder<Texture2D>.Get("poi-" + GetComponent<RealRuinsPOIComp>().poiType);
        public override Color ExpandingIconColor => Faction?.Color ?? Color.white;
        private Material cachedMat;
        private float wealthOnEnter = 1;
        private Faction originalFaction;

        public override string Label => ("RealRuins.CaptionPOI" + GetComponent<RealRuinsPOIComp>().poiType).Translate();
        

    public override Material Material {
            get {
                if (cachedMat == null) {
                    var color = Color.white;
                    cachedMat = MaterialPool.MatFrom(color: color, texPath: "World/WorldObjects/Sites/GenericSite", shader: ShaderDatabase.WorldOverlayTransparentLit, renderQueue: WorldMaterials.DynamicObjectRenderQueue);
                }
                return cachedMat;
            }
        }

        public RealRuinsPOIWorldObject() {
        }

        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Caravan caravan) {
            foreach (FloatMenuOption floatMenuOption in base.GetFloatMenuOptions(caravan)) {
                yield return floatMenuOption;
            }
            foreach (FloatMenuOption floatMenuOption in CaravanArrivalAction_VisitRealRuinsPOI.GetFloatMenuOptions(caravan, this)) {
                yield return floatMenuOption;
            }
        }

        public override IEnumerable<FloatMenuOption> GetTransportPodsFloatMenuOptions(IEnumerable<IThingHolder> pods, CompLaunchable representative) {
            foreach (FloatMenuOption transportPodsFloatMenuOption in base.GetTransportPodsFloatMenuOptions(pods, representative)) {
                yield return transportPodsFloatMenuOption;
            }
            foreach (FloatMenuOption floatMenuOption in TransportPodsArrivalAction_VisitRuinsPOI.GetFloatMenuOptions(representative, pods, this)) {
                yield return floatMenuOption;
            }
        }

        public override void ExposeData() {
            base.ExposeData();
            Scribe_Values.Look(ref wealthOnEnter, "wealthOnEnter");
            Scribe_References.Look(ref originalFaction, "originalFaction");
        }

        public override IEnumerable<Gizmo> GetGizmos() {
            foreach (Gizmo gizmo in base.GetGizmos()) {
                yield return gizmo;
            }
            if (HasMap && Find.WorldSelector.SingleSelectedObject == this) {
                yield return SettleInExistingMapUtility.SettleCommand(Map, requiresNoEnemies: true);
            }
        }

        public override void PostMapGenerate() {
            base.PostMapGenerate();
            //we don't use wealth watcher because it can't be set up properly (counts only player's belongings and always includes pawns)
            wealthOnEnter = CurrentMapWealth();
            originalFaction = Faction;
            Debug.Log(Debug.POI, "Started with cost of  {0}", wealthOnEnter);
        }

        public override void Tick() {
            base.Tick();
            if (HasMap && Faction != Faction.OfPlayer) {
                if (!GenHostility.AnyHostileActiveThreatToPlayer(Map)) {
                    //show letter about enemies defeated
                    originalFaction = Faction;
                    SetFaction(Faction.OfPlayer);
                    Debug.Log("Setting player faction, cached mat set to nil");
                    cachedMat = null;
                }
            }
        }

        public override bool ShouldRemoveMapNow(out bool alsoRemoveWorldObject) {
            bool shouldRemove = !Map.mapPawns.AnyPawnBlockingMapRemoval;
            if (shouldRemove) {
                EnterCooldownComp cooldownComp = GetComponent<EnterCooldownComp>();
                RealRuinsPOIComp poiComp = GetComponent<RealRuinsPOIComp>();
                SetFaction(originalFaction);
                Debug.Log("Setting original faction ({0}), cached mat set to nil", originalFaction);
                cachedMat = null; //reset cached icon to recolor it
                Draw();

                float blueprintCost = 1;
                if (poiComp != null) {
                    if (poiComp.poiType == (int)POIType.Ruins) {
                        alsoRemoveWorldObject = true;
                        return true;
                    } else {
                        blueprintCost = poiComp.approximateSnapshotCost;
                    }
                }

                float mapWealth = CurrentMapWealth();
                float difference = wealthOnEnter - mapWealth;
                float ratio = difference / blueprintCost;
                if (cooldownComp != null) {
                    cooldownComp.Props.durationDays = Math.Max(4, difference / 2000);
                }

                Debug.Log(Debug.POI, "on enter {0}, now {1}, snapshot: {4}, diff {2}, ratio {3},", wealthOnEnter, mapWealth, difference, ratio, blueprintCost);

                alsoRemoveWorldObject = ratio > 0.5; //at least half worth of initial wealth is destroyed or stolen.
                return true;
            } else {
                alsoRemoveWorldObject = false;
            }
            return false;
        }

        private float CurrentMapWealth() {
            float totalCost = 0;
            foreach (Thing thing in Map.listerThings.AllThings) {
                totalCost += thing.def.ThingComponentsMarketCost(thing.Stuff) * thing.stackCount;
            }
            return totalCost;
        }

        public override string GetInspectString() {
            StringBuilder builder = new StringBuilder();
            builder.Append(base.GetInspectString());

            if (builder.Length > 0) {
                builder.AppendLine();
            }

            var comp = GetComponent<RealRuinsPOIComp>();
            if (comp != null) {
                builder.AppendLine(("RealRuins.DescPOI" + comp.poiType).Translate());
                if (Faction == null) {
                    if ((POIType)comp.poiType != POIType.Ruins) {
                        builder.AppendLine("RealRuins.POINowRuined".Translate());
                    }
                } else {
                    if ((POIType)comp.poiType != POIType.Ruins) {

                        int[] costThresholds = { 0, 10000, 100000, 1000000, 10000000 };
                        string wealthDesc = null;
                        if (comp.approximateSnapshotCost > costThresholds[costThresholds.Length - 1]) wealthDesc = ("RealRuins.RuinsWealth." + (costThresholds.Length - 1)).Translate();
                        for (int i = 0; i < costThresholds.Length - 1; i++) {
                            if (comp.approximateSnapshotCost > costThresholds[i] && comp.approximateSnapshotCost <= costThresholds[i + 1]) {
                                wealthDesc = ("RealRuins.RuinsWealth." + i.ToString()).Translate();
                            }
                        }
                        if (wealthDesc != null) {
                            builder.Append("RealRuins.RuinsWealth".Translate());
                            builder.AppendLine(wealthDesc);
                        }
                    }
                }
            }
            
            return builder.ToString().TrimEndNewlines();
        }
    }
}
