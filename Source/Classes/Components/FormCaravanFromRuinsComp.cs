using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using RimWorld;
using RimWorld.Planet;

namespace RealRuins {
    class FormCaravanFromRuinsComp: FormCaravanComp {
        public new bool CanFormOrReformCaravanNow {
            get {
                MapParent mapParent = (MapParent)parent;
                if (!mapParent.HasMap) {
                    return false;
                }
                Debug.Log("Checking if caravan can be reformed, type {0}, reform {1}, has hostiles {2}", RealRuins_ModSettings.caravanReformType, Reform, GenHostility.AnyHostileActiveThreatToPlayer(mapParent.Map));
                if (RealRuins_ModSettings.caravanReformType == 1) { //in case of instant caravan reforming we need to check if there are enemies
                    if (Reform && (GenHostility.AnyHostileActiveThreatToPlayer(mapParent.Map) || mapParent.Map.mapPawns.FreeColonistsSpawnedCount == 0)) {
                        return false;
                    }
                }
                return true; //otherwise we can always TRY to reform caravan under enemy fire
            }
        }

        public override IEnumerable<Gizmo> GetGizmos() {
            MapParent mapParent = (MapParent)parent;
            if (!mapParent.HasMap) {
                yield break;
            } if (mapParent.Map.mapPawns.FreeColonistsSpawnedCount != 0) {
                Command_Action reformCaravan = new Command_Action {
                    defaultLabel = "CommandReformCaravan".Translate(),
                    defaultDesc = "CommandReformCaravanDesc".Translate(),
                    icon = FormCaravanCommand,
                    hotKey = KeyBindingDefOf.Misc2,
                    tutorTag = "ReformCaravan",
                    action = delegate
                    {
                        Find.WindowStack.Add(new Dialog_FormCaravan(mapParent.Map, reform: true)); //always show reform caravan
                    }
                };
                //Disable caravan reforming due to hostiles only in instant mode
                if (GenHostility.AnyHostileActiveThreatToPlayer(mapParent.Map) && RealRuins_ModSettings.caravanReformType == 1) {
                    reformCaravan.Disable("CommandReformCaravanFailHostilePawns".Translate());
                }
                yield return reformCaravan;
            }
        }
    }
}
