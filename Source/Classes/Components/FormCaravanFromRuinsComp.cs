using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RealRuins;

internal class FormCaravanFromRuinsComp : FormCaravanComp
{
	public new bool CanFormOrReformCaravanNow
	{
		get
		{
			MapParent mapParent = (MapParent)parent;
			if (!mapParent.HasMap)
			{
				return false;
			}
			Debug.Log("Checking if caravan can be reformed, type {0}, reform {1}, has hostiles {2}", RealRuins_ModSettings.caravanReformType, base.Reform, GenHostility.AnyHostileActiveThreatToPlayer(mapParent.Map));
			if (RealRuins_ModSettings.caravanReformType == 1 && base.Reform && (GenHostility.AnyHostileActiveThreatToPlayer(mapParent.Map) || mapParent.Map.mapPawns.FreeColonistsSpawnedCount == 0))
			{
				return false;
			}
			return true;
		}
	}

	public override IEnumerable<Gizmo> GetGizmos()
	{
		MapParent mapParent = (MapParent)parent;
		if (mapParent.HasMap && mapParent.Map.mapPawns.FreeColonistsSpawnedCount != 0)
		{
			Command_Action reformCaravan = new Command_Action
			{
				defaultLabel = "CommandReformCaravan".Translate(),
				defaultDesc = "CommandReformCaravanDesc".Translate(),
				icon = FormCaravanComp.FormCaravanCommand,
				hotKey = KeyBindingDefOf.Misc2,
				tutorTag = "ReformCaravan",
				action = delegate
				{
					Find.WindowStack.Add(new Dialog_FormCaravan(mapParent.Map, reform: true));
				}
			};
			if (GenHostility.AnyHostileActiveThreatToPlayer(mapParent.Map) && RealRuins_ModSettings.caravanReformType == 1)
			{
				reformCaravan.Disable("CommandReformCaravanFailHostilePawns".Translate());
			}
			yield return reformCaravan;
		}
	}
}
