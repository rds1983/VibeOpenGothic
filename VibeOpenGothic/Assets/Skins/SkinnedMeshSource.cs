using Microsoft.Xna.Framework;
using VibeOpenGothic.Assets.Meshes;

namespace VibeOpenGothic.Assets.Skins;

/// <summary>
/// A model mesh plus Gothic-space skinning data. Geometry supplies normals, UVs
/// and material submeshes; influences supply local positions per bone.
/// </summary>
public sealed record SkinnedMeshSource(
	MeshSource Geometry,
	IReadOnlyList<IReadOnlyList<SkinInfluence>> Influences,
	IReadOnlyList<SkinHierarchyNode> Hierarchy,
	Vector3 RootTranslation);
