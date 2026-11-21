using Microsoft.Xna.Framework;

namespace VibeOpenGothic.Physics;

/// <summary>
/// Engine-space ray hit. Distances and positions are in centimetres.
/// </summary>
public readonly record struct PhysicsHit(
	Vector3 Position,
	Vector3 Normal,
	float Distance,
	CollisionLayer Layer);

/// <summary>
/// Conservative broad-phase overlap result. <see cref="PhysicsWorld.OverlapBox"/>
/// does not run contact generation, so this means the collidable's bounding box
/// overlaps the query box.
/// </summary>
public readonly record struct PhysicsOverlap(CollisionLayer Layer);

/// <summary>
/// Engine-facing collision API. Implementations work in centimetres at the
/// boundary and convert to metres internally.
/// </summary>
public interface IPhysicsWorld
{
	void Step(float elapsedSeconds);

	bool Raycast(
		Vector3 from,
		Vector3 to,
		out PhysicsHit hit,
		CollisionLayer mask);

	int OverlapBox(
		Vector3 center,
		Vector3 halfExtents,
		CollisionLayer mask,
		List<PhysicsOverlap>? results = null);
}
