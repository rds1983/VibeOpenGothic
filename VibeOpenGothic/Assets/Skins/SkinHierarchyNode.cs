using Microsoft.Xna.Framework;

namespace VibeOpenGothic.Assets.Skins;

public readonly record struct SkinHierarchyNode(
	string Name,
	int ParentIndex,
	Matrix LocalTransform);
