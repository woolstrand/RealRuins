using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using RimWorld;
using RimWorld.Planet;

namespace RealRuins {
    // Only the gizmo is customized. FormCaravanComp.CanFormOrReformCaravanNow is not virtual and must stay vanilla:
    // * in instant mode vanilla already blocks reforming while hostiles are around (CanReformNow also requires a free colonist);
    // * in the other modes the reform button below is available under fire, because DialogFormCaravan_Patch turns it into
    //   regular caravan forming. Making the property return true there would also turn off ExitMapGrid (it is used only when
    //   the caravan can't be reformed), so pawns could not leave the map by walking off the edge during a fight.
    class FormCaravanFromRuinsComp: FormCaravanComp {
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
