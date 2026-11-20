namespace VibeOpenGothic.World;

public sealed record WorldLoadStats(
	int RootObjectCount,
	int VobCount,
	int StaticVobCount,
	int UniqueStaticMeshCount,
	int ModelVobCount,
	int UniqueModelCount,
	int FailedModelCount,
	int SkippedVisualCount,
	int FailedVisualCount,
	int MissingTextureCount,
	int WaypointCount,
	double ParseMilliseconds,
	double MeshMilliseconds,
	double MaterialMilliseconds,
	double VobMilliseconds,
	double WaypointMilliseconds,
	double TotalMilliseconds);
