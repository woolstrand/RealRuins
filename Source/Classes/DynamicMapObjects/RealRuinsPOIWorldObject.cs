using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using RimWorld.Planet;
using UnityEngine;
using RimWorld;
using Verse;

using SRTS;

namespace RealRuins {
    [StaticConstructorOnStartup]
    class RealRuinsPOIWorldObject : MapParent {

        public override Texture2D ExpandingIcon {
            get {
                return ContentFinder<Texture2D>.Get("poi-" + GetComponent<RealRuinsPOIComp>().poiType);
            }
        }

        public override Color ExpandingIconColor => this.Faction == null ? Color.white : this.Faction.Color;
        private Material cachedMat;
        private float wealthOnEnter = 1;
        private Faction originalFaction;

        public override string Label => ("RealRuins.CaptionPOI" + GetComponent<RealRuinsPOIComp>().poiType).Translate();

        private string expandedIconTexturePath {
            get {
                switch ((POIType)(GetComponent<RealRuinsPOIComp>().poiType)) {
                    case POIType.Ruins:
                        return "World/WorldObjects/TribalSettlement";
                    case POIType.City:
                    case POIType.Stronghold:
                    case POIType.MilitaryBaseLarge:
                        return "World/WorldObjects/DefaultSettlement";
                    case POIType.Research:
                    case POIType.Storage:
                    case POIType.Factory:
                    case POIType.PowerPlant:
                        return "World/WorldObjects/TribalSettlement";
                    case POIType.Camp:
                    case POIType.Communication:
                        return "World/WorldObjects/Sites/GenericSite";
                    case POIType.MilitaryBaseSmall:
                    case POIType.Outpost:
                        return "World/WorldObjects/Sites/Outpost";
                    default:
                        return "World/WorldObjects/TribalSettlement";
                }
            }
        }

        public override Material Material {
            get {
                if (cachedMat == null) {
                    var color = this.ExpandingIconColor;
                    if (color == null) { color = Color.white; }
                    cachedMat = MaterialPool.MatFrom(texPath: this.expandedIconTexturePath, shader: ShaderDatabase.WorldOverlayTransparentLit, color: color, renderQueue: WorldMaterials.DynamicObjectRenderQueue); ;
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
                alsoRemoveWorldObject = false;
                EnterCooldownComp cooldownComp = GetComponent<EnterCooldownComp>();
                RealRuinsPOIComp poiComp = GetComponent<RealRuinsPOIComp>();

                float blueprintCost = 1;
                if (poiComp != null) {
                    if (poiComp.poiType == (int)POIType.Ruins || originalFaction == null) {
                        alsoRemoveWorldObject = true;
                        return true;
                    } else {
                        blueprintCost = poiComp.approximateSnapshotCost;
                    }
                }

                float mapWealth = CurrentMapWealth();
                float difference = wealthOnEnter - mapWealth;

                //Ratio is what part of original cost was destroyed or stolen.
                float ratio = difference / blueprintCost;
                float cooldownDuration = Math.Max(4, difference / 2000);
                if (cooldownComp != null) {
                    cooldownComp.Props.durationDays = cooldownDuration;
                }

                Debug.Log(Debug.POI, "Leaving POI map. Initial cost: {0} (bp cost: {4}), now: {1}. Difference = {2}, ratio: {3}", wealthOnEnter, mapWealth, difference, ratio, blueprintCost);

                if (ratio < 0.1) {
                    //less than 10% stolen: site reclaimed
                    SetFaction(originalFaction);
                    Debug.Log(Debug.POI, "Low damage. Restoring owner, activating cooldown for {0} days", cooldownDuration);
                } else if (ratio < 0.3) {
                    //if 10-30% was destroyed, then there is a chance that ruins won't be reclaimed by their previous owners
                    if (Rand.Chance(0.3f)) {
                        // 30% of abandoning POI
                        Debug.Log(Debug.POI, "Moderate damage. Abandoning, activating cooldown for {0} days", cooldownDuration);
                    } else {
                        //
                        SetFaction(originalFaction);
                        Debug.Log(Debug.POI, "Moderate damage. Restoring owner, activating cooldown for {0} days", cooldownDuration);
                    }
                } else {
                    Debug.Log(Debug.POI, "Significant damage, destroying");
                    alsoRemoveWorldObject = true;
                }

                cachedMat = null; //reset cached icon to recolor it
                Draw();

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
                if (Faction != null) {
                    builder.AppendLine(("RealRuins.DescPOI" + comp.poiType).Translate());

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
                } else {
                    if ((POIType)comp.poiType != POIType.Ruins) {
                        builder.AppendLine(String.Format("RealRuins.POINowRuined".Translate(), Label.ToLower()));
                    } else {
                        builder.AppendLine(String.Format("RealRuins.POIUselessRuins".Translate(), "something"));
                    }
                }
            }
            
            return builder.ToString().TrimEndNewlines();
        }
    }
}
