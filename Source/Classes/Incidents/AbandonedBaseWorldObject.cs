using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace RealRuins;

[StaticConstructorOnStartup]
internal class AbandonedBaseWorldObject : MapParent
{
	private Material cachedMat;

	private bool hasStartedCountdown = false;

	public override Texture2D ExpandingIcon => ContentFinder<Texture2D>.Get("ruinedbase");

	public override Color ExpandingIconColor => Color.white;

	public override Material Material
	{
		get
		{
			if (cachedMat == null)
			{
				cachedMat = MaterialPool.MatFrom("World/WorldObjects/Sites/GenericSite", color: base.Faction?.Color ?? Color.white, shader: ShaderDatabase.WorldOverlayTransparentLit, renderQueue: WorldMaterials.WorldObjectRenderQueue);
			}
			return cachedMat;
		}
	}

	public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Caravan caravan)
	{
		foreach (FloatMenuOption floatMenuOption in base.GetFloatMenuOptions(caravan))
		{
			yield return floatMenuOption;
		}
		foreach (FloatMenuOption floatMenuOption2 in CaravanArrivalAction_VisitAbandonedBase.GetFloatMenuOptions(caravan, this))
		{
			yield return floatMenuOption2;
		}
	}

	public override IEnumerable<FloatMenuOption> GetTransportPodsFloatMenuOptions(IEnumerable<IThingHolder> pods, CompLaunchable representative)
	{
		foreach (FloatMenuOption transportPodsFloatMenuOption in base.GetTransportPodsFloatMenuOptions(pods, representative))
		{
			yield return transportPodsFloatMenuOption;
		}
		foreach (FloatMenuOption floatMenuOption in TransportPodsArrivalAction_VisitRuins.GetFloatMenuOptions(representative, pods, this))
		{
			yield return floatMenuOption;
		}
	}

	public override IEnumerable<Gizmo> GetGizmos()
	{
		foreach (Gizmo gizmo in base.GetGizmos())
		{
			yield return gizmo;
		}
	}

	public override bool ShouldRemoveMapNow(out bool alsoRemoveWorldObject)
	{
		bool flag = (alsoRemoveWorldObject = !base.Map.mapPawns.AnyPawnBlockingMapRemoval);
		if (flag)
		{
			RuinedBaseComp component = GetComponent<RuinedBaseComp>();
			if (component != null)
			{
				string successSignal = component.successSignal;
				Find.SignalManager.SendSignal(new Signal(successSignal));
			}
		}
		return flag;
	}

	public override string GetInspectString()
	{
		return base.GetInspectString();
	}
}
