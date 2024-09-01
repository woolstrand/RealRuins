using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RealRuins;

public class TrippingTrigger : Thing
{
	public override void Tick()
	{
		if (!base.Spawned)
		{
			return;
		}
		List<Thing> thingList = base.Position.GetThingList(base.Map);
		for (int i = 0; i < thingList.Count; i++)
		{
			if (thingList[i] is Pawn pawn)
			{
				DamageInfo dinfo = new DamageInfo(DamageDefOf.Blunt, Rand.Value * 9f + 1f, 0.1f, -1f, this, null, null, DamageInfo.SourceCategory.Collapse);
				if (Rand.Chance(0.7f))
				{
					pawn.TakeDamage(dinfo);
				}
				Destroy();
			}
		}
	}
}
