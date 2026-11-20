namespace VibeOpenGothic.Assets.Skins;

public sealed record SkinBakeDiagnostics(
	int VertexCount,
	int BoneCount,
	float MaxInconsistency,
	float MaxWeightedInconsistency,
	int InconsistentVertexCount,
	int RenormalizedWeightCount,
	int ZeroWeightCount,
	int OutOfRangeBoneCount,
	int TruncatedInfluenceCount);

public sealed record SkinBakeResult(
	IReadOnlyList<SkinBakedVertex> Vertices,
	SkinBakeDiagnostics Diagnostics);
