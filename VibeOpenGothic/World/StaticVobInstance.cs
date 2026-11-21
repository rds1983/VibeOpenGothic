using Microsoft.Xna.Framework;

namespace VibeOpenGothic.World;

public sealed record StaticVobInstance(
	string MeshKey,
	Matrix Transform,
	bool CdStatic,
	bool CdDynamic);
