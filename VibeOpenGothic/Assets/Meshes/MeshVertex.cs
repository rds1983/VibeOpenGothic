using Microsoft.Xna.Framework;

namespace VibeOpenGothic.Assets.Meshes;

public readonly record struct MeshVertex(
	Vector3 Position,
	Vector3 Normal,
	Vector2 TexCoord);
