using VibeOpenGothic.Assets.Materials;
using VibeOpenGothic.Assets.Meshes;

namespace VibeOpenGothic.World;

/// <summary>CPU-side static visual mesh, shared by every VOB instance that uses it.</summary>
public sealed record StaticMeshSource(
	string Key,
	MeshData Mesh,
	MaterialSet Materials);
