using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace RealRuins;

public class RaidTrigger : Thing, IAttackTarget, ILoadReferenceable
{
	public Faction faction;

	public float value;

	private bool triggered;

	private int ticksLeft = 200;

	private int referenceTimeoutAfterTriggered = 200;

	public float TargetPriorityFactor => 0f;

	public Thing Thing => this;

	public LocalTargetInfo TargetCurrentlyAimingAt => null;

	public void SetTimeouts(int timeoutUntilAutoTrigger, int referenceTimeoutAfterTriggered = 200)
	{
		ticksLeft = timeoutUntilAutoTrigger;
		this.referenceTimeoutAfterTriggered = referenceTimeoutAfterTriggered;
	}

	public bool IsTriggered()
	{
		return triggered;
	}

	public int TicksLeft()
	{
		return ticksLeft;
	}

	public override void Tick()
	{
		if (Find.TickManager.TicksGame % 250 == 0)
		{
			TickRare();
		}
	}

	public override void TickRare()
	{
		if (!base.Spawned)
		{
			return;
		}
		ticksLeft--;
		if (!triggered)
		{
			if (ticksLeft < 0)
			{
				ticksLeft = (int)Math.Abs(Rand.Gaussian(0f, referenceTimeoutAfterTriggered));
				triggered = true;
				Debug.Log("Battle", "Auto triggered raid at {0}, {1} of value {2} after {3} long ticks (approximately max speed seconds)", base.Position.x, base.Position.z, value, ticksLeft);
			}
			List<Thing> searchSet = PawnsFinder.AllMaps_FreeColonistsSpawned.ToList().ConvertAll((Converter<Pawn, Thing>)((Pawn pawn) => pawn));
			Thing thing = GenClosest.ClosestThing_Global(base.Position, searchSet, 10f);
			if (thing != null)
			{
				ticksLeft = (int)Math.Abs(Rand.Gaussian(0f, referenceTimeoutAfterTriggered));
				triggered = true;
				Debug.Log("Battle", "Triggered raid at {0}, {1} of value {2} after {3} long ticks (approximately max speed seconds)", base.Position.x, base.Position.z, value, ticksLeft);
			}
		}
		else if (ticksLeft < 0)
		{
			IncidentDef raidEnemy = IncidentDefOf.RaidEnemy;
			IncidentParms parms = new IncidentParms
			{
				faction = faction,
				points = value,
				target = base.Map
			};
			raidEnemy.Worker.TryExecute(parms);
			Destroy();
		}
	}

	public override void ExposeData()
	{
		base.ExposeData();
		Scribe_References.Look(ref faction, "faction");
		Scribe_Values.Look(ref value, "value", 0f);
		Scribe_Values.Look(ref triggered, "triggered", defaultValue: false);
		Scribe_Values.Look(ref ticksLeft, "ticksLeft", 0);
	}

	public bool ThreatDisabled(IAttackTargetSearcher disabledFor)
	{
		return true;
	}
}
