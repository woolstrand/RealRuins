using RimWorld;
using Verse;

namespace RealRuins;

internal class ThoughtWorker_ScavengingRuins : ThoughtWorker
{
	public const float MaxForUpset = 10000f;

	public const float MinForMedium = 25000f;

	public const float MinForHigh = 100000f;

	public const float MinForVeryHigh = 500000f;

	public const float MinForExtreme = 1000000f;

	protected override ThoughtState CurrentStateInternal(Pawn p)
	{
		RuinedBaseComp ruinedBaseComp = (p.Map?.Parent)?.GetComponent<RuinedBaseComp>();
		if (ruinedBaseComp == null)
		{
			return ThoughtState.Inactive;
		}
		if (!ruinedBaseComp.isActive)
		{
			return ThoughtState.Inactive;
		}
		if ((float)ruinedBaseComp.currentCapCost < 10000f)
		{
			return ThoughtState.ActiveAtStage(0);
		}
		if ((float)ruinedBaseComp.currentCapCost > 1000000f)
		{
			return ThoughtState.ActiveAtStage(4);
		}
		if ((float)ruinedBaseComp.currentCapCost > 500000f)
		{
			return ThoughtState.ActiveAtStage(3);
		}
		if ((float)ruinedBaseComp.currentCapCost > 100000f)
		{
			return ThoughtState.ActiveAtStage(2);
		}
		if ((float)ruinedBaseComp.currentCapCost > 25000f)
		{
			return ThoughtState.ActiveAtStage(1);
		}
		return ThoughtState.Inactive;
	}
}
