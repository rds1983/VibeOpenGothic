using Microsoft.Xna.Framework;

namespace VibeOpenGothic.Physics;

/// <summary>
/// Static triangle mesh in engine space (centimetres). PhysicsWorld converts
/// positions to metres when it builds the Bepu mesh.
/// </summary>
public sealed record PhysicsMesh(
	string Key,
	Vector3[] Vertices,
	int[] Indices,
	CollisionLayer Layer);

/// <summary>
/// One static placement of a <see cref="PhysicsMesh"/> in engine space.
/// </summary>
public sealed record PhysicsMeshInstance(
	string MeshKey,
	Matrix Transform,
	CollisionLayer Layer);

/// <summary>
/// Renderer-independent description of the static collision world.
/// </summary>
public sealed class PhysicsWorldDescription
{
	private readonly List<PhysicsMesh> _meshes = new();
	private readonly List<PhysicsMeshInstance> _instances = new();

	public IReadOnlyList<PhysicsMesh> Meshes => _meshes;

	public IReadOnlyList<PhysicsMeshInstance> Instances => _instances;

	public void AddMesh(
		string key,
		Vector3[] vertices,
		int[] indices,
		CollisionLayer layer)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(key);
		ArgumentNullException.ThrowIfNull(vertices);
		ArgumentNullException.ThrowIfNull(indices);
		if (indices.Length % 3 != 0)
		{
			throw new ArgumentException(
				"Triangle indices must be a multiple of three.",
				nameof(indices));
		}

		_meshes.Add(new PhysicsMesh(key, vertices, indices, layer));
	}

	public void AddInstance(
		string meshKey,
		Matrix transform,
		CollisionLayer layer)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(meshKey);
		_instances.Add(new PhysicsMeshInstance(meshKey, transform, layer));
	}
}
