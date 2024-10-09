using RimWorld;
using Verse;

namespace RealRuins;

internal static class ThingDefExtension
{
	public static float ThingComponentsMarketCost(this BuildableDef buildable, ThingDef stuffDef = null)
	{
		float num = 0f;
		if (buildable == null)
		{
			return 0f;
		}
		if (buildable.costList != null)
		{
			foreach (ThingDefCountClass cost in buildable.costList)
			{
				num += (float)cost.count * cost.thingDef.ThingComponentsMarketCost();
			}
		}
		if (buildable.costStuffCount > 0)
		{
			if (stuffDef == null)
			{
				stuffDef = GenStuff.DefaultStuffFor(buildable);
			}
			if (stuffDef != null)
			{
				num += (float)buildable.costStuffCount * stuffDef.BaseMarketValue * (1f / stuffDef.VolumePerUnit);
			}
		}
		if (num == 0f && buildable is ThingDef && ((ThingDef)buildable).recipeMaker == null)
		{
			return ((ThingDef)buildable).BaseMarketValue;
		}
		return num;
	}

	public static float ThingWeight(this ThingDef thingDef, ThingDef stuffDef)
	{
		if (thingDef == null)
		{
			return 0f;
		}
		float statValueAbstract;
		try
		{
			statValueAbstract = thingDef.GetStatValueAbstract(StatDefOf.Mass, stuffDef);
			if (statValueAbstract != 0f && statValueAbstract != 1f)
			{
				return statValueAbstract;
			}
		}
		catch
		{
		}
		if (thingDef.costList == null)
		{
			return 1f;
		}
		statValueAbstract = 0f;
		foreach (ThingDefCountClass cost in thingDef.costList)
		{
			statValueAbstract += (float)cost.count * cost.thingDef.ThingWeight(null);
		}
		return (statValueAbstract != 0f) ? statValueAbstract : 1f;
	}
}
