namespace RealRuins;

internal enum RuinedBaseState
{
	Inactive,
	WaitingForArrival,
	FightingWaves,
	WaitingForEnemiesToBeDefeated,
	WaitingTimeoutAfterEnemiesDefeat,
	WaitingToBeInformed,
	InformedWaitingForLeaving,
	ScavengedCompletely
}
