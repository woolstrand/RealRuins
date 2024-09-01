using System;
using System.Collections.Generic;
using System.Text;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace RealRuins;

[StaticConstructorOnStartup]
internal class RealRuinsPOIWorldObject : MapParent
{
	private Material cachedMat;

	private float wealthOnEnter = 1f;

	private Faction originalFaction;

	public override Texture2D ExpandingIcon => ContentFinder<Texture2D>.Get("poi-" + GetComponent<RealRuinsPOIComp>().poiType);

	public override Color ExpandingIconColor => (base.Faction == null) ? Color.white : base.Faction.Color;

	public override string Label => ("RealRuins.CaptionPOI" + GetComponent<RealRuinsPOIComp>().poiType).Translate();

	private string expandedIconTexturePath
	{
		get
		{
			switch ((POIType)GetComponent<RealRuinsPOIComp>().poiType)
			{
			case POIType.Ruins:
				return "World/WorldObjects/TribalSettlement";
			case POIType.City:
			case POIType.MilitaryBaseLarge:
			case POIType.Stronghold:
				return "World/WorldObjects/DefaultSettlement";
			case POIType.Factory:
			case POIType.Research:
			case POIType.PowerPlant:
			case POIType.Storage:
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

	public override Material Material
	{
		get
		{
			if (cachedMat == null)
			{
				Color expandingIconColor = ExpandingIconColor;
				bool flag = false;
				cachedMat = MaterialPool.MatFrom(expandedIconTexturePath, ShaderDatabase.WorldOverlayTransparentLit, expandingIconColor, WorldMaterials.DynamicObjectRenderQueue);
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
		foreach (FloatMenuOption floatMenuOption2 in CaravanArrivalAction_VisitRealRuinsPOI.GetFloatMenuOptions(caravan, this))
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
		foreach (FloatMenuOption floatMenuOption in TransportPodsArrivalAction_VisitRuinsPOI.GetFloatMenuOptions(representative, pods, this))
		{
			yield return floatMenuOption;
		}
	}

	public override void ExposeData()
	{
		base.ExposeData();
		Scribe_Values.Look(ref wealthOnEnter, "wealthOnEnter", 0f);
		Scribe_References.Look(ref originalFaction, "originalFaction");
	}

	public override IEnumerable<Gizmo> GetGizmos()
	{
		foreach (Gizmo gizmo in base.GetGizmos())
		{
			yield return gizmo;
		}
		if (base.HasMap && Find.WorldSelector.SingleSelectedObject == this)
		{
			yield return SettleInExistingMapUtility.SettleCommand(base.Map, requiresNoEnemies: true);
		}
	}

	public override void PostMapGenerate()
	{
		base.PostMapGenerate();
		wealthOnEnter = CurrentMapWealth();
		originalFaction = base.Faction;
		Debug.Log("POI", "Started with cost of  {0}", wealthOnEnter);
	}

	public override void Tick()
	{
		base.Tick();
		if (base.HasMap && base.Faction != Faction.OfPlayer && !GenHostility.AnyHostileActiveThreatToPlayer(base.Map))
		{
			originalFaction = base.Faction;
			SetFaction(Faction.OfPlayer);
			Debug.Log("Setting player faction, cached mat set to nil");
			cachedMat = null;
		}
	}

	public override bool ShouldRemoveMapNow(out bool alsoRemoveWorldObject)
	{
		if (!base.Map.mapPawns.AnyPawnBlockingMapRemoval)
		{
			alsoRemoveWorldObject = false;
			EnterCooldownComp component = GetComponent<EnterCooldownComp>();
			RealRuinsPOIComp component2 = GetComponent<RealRuinsPOIComp>();
			float num = 1f;
			if (component2 != null)
			{
				if (component2.poiType == 10 || originalFaction == null)
				{
					alsoRemoveWorldObject = true;
					return true;
				}
				num = component2.approximateSnapshotCost;
			}
			float num2 = CurrentMapWealth();
			float num3 = wealthOnEnter - num2;
			float num4 = num3 / num;
			float num5 = Math.Max(4f, num3 / 2000f);
			if (component != null)
			{
				component.Props.durationDays = num5;
			}
			Debug.Log("POI", "Leaving POI map. Initial cost: {0} (bp cost: {4}), now: {1}. Difference = {2}, ratio: {3}", wealthOnEnter, num2, num3, num4, num);
			if ((double)num4 < 0.1)
			{
				SetFaction(originalFaction);
				Debug.Log("POI", "Low damage. Restoring owner, activating cooldown for {0} days", num5);
			}
			else if ((double)num4 < 0.3)
			{
				if (Rand.Chance(0.3f))
				{
					Debug.Log("POI", "Moderate damage. Abandoning, activating cooldown for {0} days", num5);
				}
				else
				{
					SetFaction(originalFaction);
					Debug.Log("POI", "Moderate damage. Restoring owner, activating cooldown for {0} days", num5);
				}
			}
			else
			{
				Debug.Log("POI", "Significant damage, destroying");
				alsoRemoveWorldObject = true;
			}
			cachedMat = null;
			Draw();
			return true;
		}
		alsoRemoveWorldObject = false;
		return false;
	}

	private float CurrentMapWealth()
	{
		float num = 0f;
		foreach (Thing allThing in base.Map.listerThings.AllThings)
		{
			num += allThing.def.ThingComponentsMarketCost(allThing.Stuff) * (float)allThing.stackCount;
		}
		return num;
	}

	public override string GetInspectString()
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append(base.GetInspectString());
		if (stringBuilder.Length > 0)
		{
			stringBuilder.AppendLine();
		}
		RealRuinsPOIComp component = GetComponent<RealRuinsPOIComp>();
		if (component != null)
		{
			if (base.Faction != null)
			{
				stringBuilder.AppendLine(("RealRuins.DescPOI" + component.poiType).Translate());
				if (component.poiType != 10)
				{
					int[] array = new int[5] { 0, 10000, 100000, 1000000, 10000000 };
					string text = null;
					if (component.approximateSnapshotCost > (float)array[array.Length - 1])
					{
						text = ("RealRuins.RuinsWealth." + (array.Length - 1)).Translate();
					}
					for (int i = 0; i < array.Length - 1; i++)
					{
						if (component.approximateSnapshotCost > (float)array[i] && component.approximateSnapshotCost <= (float)array[i + 1])
						{
							text = ("RealRuins.RuinsWealth." + i).Translate();
						}
					}
					if (text != null)
					{
						stringBuilder.Append("RealRuins.RuinsWealth".Translate());
						stringBuilder.AppendLine(text);
					}
				}
			}
			else if (component.poiType != 10)
			{
				stringBuilder.AppendLine(string.Format("RealRuins.POINowRuined".Translate(), Label.ToLower()));
			}
			else
			{
				stringBuilder.AppendLine(string.Format("RealRuins.POIUselessRuins".Translate(), "something"));
			}
		}
		return stringBuilder.ToString().TrimEndNewlines();
	}
}
