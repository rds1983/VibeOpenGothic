using Microsoft.Xna.Framework;

namespace VibeOpenGothic.Assets.Meshes;

public sealed record MeshSourceFeature(
	Vector3 Normal,
	Vector2 TexCoord);

public sealed record MeshSourcePolygon(
	int MaterialIndex,
	int LightMapIndex,
	IReadOnlyList<int> PositionIndices,
	IReadOnlyList<int> FeatureIndices);

/// <summary>
/// CPU-side, Gothic-space mesh input. Positions and normals are left-handed;
/// <see cref="MeshBuilder"/> applies <see cref="Core.CoordinateSystem"/>.
/// </summary>
public sealed record MeshSource(
	MeshSourceKind Kind,
	IReadOnlyList<Vector3> Positions,
	IReadOnlyList<MeshSourceFeature> Features,
	IReadOnlyList<MeshSourcePolygon> Polygons);
