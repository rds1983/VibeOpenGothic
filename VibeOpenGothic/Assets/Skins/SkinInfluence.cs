using Microsoft.Xna.Framework;

namespace VibeOpenGothic.Assets.Skins;

public readonly record struct SkinInfluence(
	int BoneIndex,
	float Weight,
	Vector3 LocalPosition);
