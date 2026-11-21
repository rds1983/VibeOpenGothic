using Microsoft.Xna.Framework;
using VibeOpenGothic.Assets.Meshes;
using VibeOpenGothic.Physics;

namespace VibeOpenGothic.World;

/// <summary>
/// Converts the converted visual world asset into the renderer-independent
/// static collision description used by <see cref="PhysicsWorld"/>.
/// </summary>
public static class WorldPhysicsBuilder
{
	private const string WorldMeshKey = "__world";

	public static PhysicsWorld Build(
		WorldAsset world,
		Action<string>? progress = null)
	{
		ArgumentNullException.ThrowIfNull(world);

		var description = new PhysicsWorldDescription();
		AddMesh(description, WorldMeshKey, world.Mesh, CollisionLayer.World);
		description.AddInstance(WorldMeshKey, Matrix.Identity, CollisionLayer.World);

		var usedMeshes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var instance in world.StaticVobs)
		{
			if (IsCollidable(instance))
			{
				usedMeshes.Add(instance.MeshKey);
			}
		}

		foreach (var key in usedMeshes.OrderBy(
			key => key,
			StringComparer.OrdinalIgnoreCase))
		{
			if (world.StaticMeshes.TryGetValue(key, out var source))
			{
				AddMesh(description, key, source.Mesh, CollisionLayer.StaticProp);
			}
		}

		var instanceCount = 0;
		foreach (var instance in world.StaticVobs)
		{
			if (!IsCollidable(instance))
			{
				continue;
			}

			description.AddInstance(
				instance.MeshKey,
				instance.Transform,
				CollisionLayer.StaticProp);
			instanceCount++;
		}

		progress?.Invoke(
			$"Collision input: {usedMeshes.Count} static meshes, " +
			$"{instanceCount} static instances");
		return new PhysicsWorld(description, progress);
	}

	private static bool IsCollidable(StaticVobInstance instance)
	{
		return instance.CdStatic || instance.CdDynamic;
	}

	private static void AddMesh(
		PhysicsWorldDescription description,
		string key,
		MeshData mesh,
		CollisionLayer layer)
	{
		var positions = new Vector3[mesh.VertexCount];
		var source = mesh.Vertices.Span;
		for (var index = 0; index < positions.Length; index++)
		{
			positions[index] = source[index].Position;
		}

		description.AddMesh(
			key,
			positions,
			mesh.Indices.ToArray(),
			layer);
	}
}
