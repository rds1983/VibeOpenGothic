using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics.PackedVector;

namespace VibeOpenGothic.Assets.Skins;

public readonly record struct SkinBakedVertex(
	Vector3 Position,
	Vector4 Weights,
	Byte4 BoneIndices);
