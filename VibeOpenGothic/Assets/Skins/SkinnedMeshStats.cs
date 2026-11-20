namespace VibeOpenGothic.Assets.Skins;

public sealed record SkinnedMeshStats(
	int SourcePolygonCount,
	int SourceTriangleCount,
	int DegenerateTriangleCount,
	int InvalidPolygonCount,
	int VertexCount,
	int IndexCount,
	int SubMeshCount,
	SkinBakeDiagnostics Skin);

public sealed record SkinnedMeshResult(
	SkinnedMeshData Mesh,
	SkinnedMeshStats Stats);
