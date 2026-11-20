using Microsoft.Xna.Framework;
using VibeOpenGothic.Assets.Materials;
using VibeOpenGothic.Assets.Meshes;
using VibeOpenGothic.Assets.Models;

namespace VibeOpenGothic.World;

/// <summary>
/// CPU-side converted ZEN world. GPU buffers and textures are created by the
/// rendering layer from this asset.
/// </summary>
public sealed class WorldAsset
{
	public WorldAsset(
		string name,
		MeshData mesh,
		MaterialSet materials,
		IReadOnlyList<StaticVobInstance> staticVobs,
		IReadOnlyDictionary<string, StaticMeshSource> staticMeshes,
		IReadOnlyList<ModelVobInstance> modelVobs,
		IReadOnlyDictionary<string, GothicModelAsset> models,
		IReadOnlyList<WorldWaypoint> waypoints,
		IReadOnlyList<string> missingTextures,
		Vector3? startPosition,
		Vector3 startDirection,
		BoundingBox bounds,
		WorldLoadStats stats)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		ArgumentNullException.ThrowIfNull(mesh);
		ArgumentNullException.ThrowIfNull(materials);
		ArgumentNullException.ThrowIfNull(staticVobs);
		ArgumentNullException.ThrowIfNull(staticMeshes);
		ArgumentNullException.ThrowIfNull(modelVobs);
		ArgumentNullException.ThrowIfNull(models);
		ArgumentNullException.ThrowIfNull(waypoints);
		ArgumentNullException.ThrowIfNull(missingTextures);

		Name = name;
		Mesh = mesh;
		Materials = materials;
		StaticVobs = staticVobs;
		StaticMeshes = staticMeshes;
		ModelVobs = modelVobs;
		Models = models;
		Waypoints = waypoints;
		MissingTextures = missingTextures;
		StartPosition = startPosition;
		StartDirection = startDirection;
		Bounds = bounds;
		Stats = stats;
	}

	public string Name { get; }

	public MeshData Mesh { get; }

	public MaterialSet Materials { get; }

	public IReadOnlyList<StaticVobInstance> StaticVobs { get; }

	public IReadOnlyDictionary<string, StaticMeshSource> StaticMeshes { get; }

	public IReadOnlyList<ModelVobInstance> ModelVobs { get; }

	public IReadOnlyDictionary<string, GothicModelAsset> Models { get; }

	public IReadOnlyList<WorldWaypoint> Waypoints { get; }

	public IReadOnlyList<string> MissingTextures { get; }

	public Vector3? StartPosition { get; }

	public Vector3 StartDirection { get; }

	public BoundingBox Bounds { get; }

	public WorldLoadStats Stats { get; }
}
