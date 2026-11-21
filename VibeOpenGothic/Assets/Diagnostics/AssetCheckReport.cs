namespace VibeOpenGothic.Assets.Diagnostics;

public sealed record AssetIssue(
	string Kind,
	string Asset,
	string Message);

public sealed record AssetCheckReport(
	string InstallRoot,
	string GameVersion,
	double Milliseconds,
	IReadOnlyDictionary<string, int> FileCounts,
	int ModelScripts,
	int ModelHierarchies,
	int ModelMeshes,
	int Animations,
	int Aliases,
	int MissingHierarchies,
	int MissingModelMeshes,
	int MissingAnimations,
	int MissingTextures,
	int MissingVobAssets,
	int MissingScripts,
	int StaticMeshes,
	int MorphMeshes,
	int Textures,
	int Worlds,
	int ScriptFiles,
	int Attachments,
	int Triangles,
	int Vertices,
	int DegenerateTriangles,
	int InvalidPolygons,
	int UnsupportedFeatures,
	int Failures,
	int Warnings,
	IReadOnlyList<AssetIssue> Issues);
