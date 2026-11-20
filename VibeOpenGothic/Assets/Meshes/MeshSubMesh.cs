using Microsoft.Xna.Framework;

namespace VibeOpenGothic.Assets.Meshes;

public sealed record MeshSubMesh(
	int IndexOffset,
	int IndexCount,
	int MaterialIndex,
	int LightMapIndex,
	BoundingBox Bounds);
