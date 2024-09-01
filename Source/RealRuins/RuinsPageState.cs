namespace RealRuins;

internal enum RuinsPageState
{
	Idle,
	LoadingHeader,
	LoadedHeader,
	LoadingBlueprints,
	LoadedBlueprints,
	ProcessingBlueprints,
	Completed
}
