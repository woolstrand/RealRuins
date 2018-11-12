using RimWorld;
using System.Collections.Generic;
using Verse;

namespace RealRuins {
    public class TrippingTrigger : Thing {
        public override void Tick()
        {
            if (base.Spawned) {
                List<Thing> thingList = base.Position.GetThingList(base.Map);
                for (int i = 0; i < thingList.Count; i++) {
                    Pawn pawn = thingList[i] as Pawn;
                    if (pawn != null) {
                        //float change = pawn.
                        DamageInfo dinfo = new DamageInfo(DamageDefOf.Blunt, Rand.Value * 9f + 1f, 0.1f, -1f, this, null, null, DamageInfo.SourceCategory.Collapse, null);

                        if (Rand.Chance(0.7f)) {
                            pawn.TakeDamage(dinfo);
                        }

                        this.Destroy(DestroyMode.Vanish);
                    }
                }
            }
        }
    }
}