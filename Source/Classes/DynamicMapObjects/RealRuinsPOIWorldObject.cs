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
        private float wealthOnEnter = 1;

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

        public override void ExposeData() {
            base.ExposeData();
            Scribe_Values.Look(ref wealthOnEnter, "wealthOnEnter");
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
            Debug.Message("Started with cost of {0}", wealthOnEnter);
        }

        public override void Tick() {
            base.Tick();
            if (HasMap && Faction != Faction.OfPlayer) {
                if (!GenHostility.AnyHostileActiveThreatToPlayer(Map)) {
                    //show letter about enemies defeated
                    SetFaction(Faction.OfPlayer);
                }
            }
        }

        public override bool ShouldRemoveMapNow(out bool alsoRemoveWorldObject) {
            bool shouldRemove = !Map.mapPawns.AnyPawnBlockingMapRemoval;
            if (shouldRemove) {
                EnterCooldownComp cooldownComp = GetComponent<EnterCooldownComp>();
                RealRuinsPOIComp poiComp = GetComponent<RealRuinsPOIComp>();
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

                Debug.Message("on enter {0}, now {1}, snapshot: {4}, diff {2}, ratio {3},", wealthOnEnter, mapWealth, difference, ratio, blueprintCost);

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
    }
}
