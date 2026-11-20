using Microsoft.Xna.Framework;

namespace VibeOpenGothic.World;

public sealed record WorldWaypoint(
	string Name,
	Vector3 Position,
	Vector3 Direction,
	bool FreePoint,
	bool UnderWater,
	int WaterDepth);
