using System.Diagnostics;
using Microsoft.Xna.Framework;
using VibeOpenGothic.Assets.Materials;
using VibeOpenGothic.Assets.Meshes;
using VibeOpenGothic.Assets.Textures;
using VibeOpenGothic.Core;
using VibeOpenGothic.Data.Vfs;
using ZenKit;
using ZenKit.Vobs;

namespace VibeOpenGothic.World;

/// <summary>
/// Loads a ZEN world into CPU-side engine data: converted mesh and materials,
/// static VOB instances with composed transforms, waypoints and the start point.
/// GPU upload belongs to the rendering layer.
/// </summary>
public static class WorldLoader
{
	public static WorldAsset Load(
		IGameVfs vfs,
		string worldName,
		GameVersion gameVersion,
		Action<string>? progress = null)
	{
		ArgumentNullException.ThrowIfNull(vfs);
		ArgumentException.ThrowIfNullOrWhiteSpace(worldName);
		progress ??= _ => { };

		var total = Stopwatch.StartNew();
		var entry = vfs.Find(worldName) ??
			throw new FileNotFoundException($"World '{worldName}' was not found in the VFS.", worldName);

		progress($"Loading {entry.Path}");
		var worldRead = new Read(ReadBytes(vfs, entry.Path));
		var world = new ZenKit.World(worldRead, gameVersion);
		var parseMs = total.Elapsed.TotalMilliseconds;

		var mesh = world.Mesh ??
			throw new InvalidDataException($"World '{entry.Path}' has no mesh.");
		var textureCache = new TextureCache(
			new ZenKitTextureLoader(vfs),
			name => progress($"Missing texture: {name}"));

		progress("Converting world mesh...");
		var meshStart = Stopwatch.StartNew();
		var source = ZenKitMeshAdapter.FromMesh(mesh, includePortals: false, includeGhostOccluders: false);
		var meshResult = MeshBuilder.Build(source, entry.Path);
		var meshMs = meshStart.Elapsed.TotalMilliseconds;
		var materialStart = Stopwatch.StartNew();
		var materials = MaterialSetBuilder.FromMesh(mesh, textureCache);
		var materialMs = materialStart.Elapsed.TotalMilliseconds;

		progress(
			$"World mesh: {meshResult.Stats.SourceTriangleCount} triangles, " +
			$"{meshResult.Stats.VertexCount} vertices, {meshResult.Stats.SubMeshCount} submeshes, " +
			$"{materials.Materials.Count} materials in {meshMs:0} ms");

		var vobStart = Stopwatch.StartNew();
		var staticVobs = new List<StaticVobInstance>();
		var staticMeshes = new Dictionary<string, StaticMeshSource>(StringComparer.OrdinalIgnoreCase);
		var failedVisualKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var vobCount = 0;
		var skippedVisuals = 0;
		var failedVisuals = 0;
		Vector3? startPosition = null;
		var startDirection = -Vector3.UnitZ;
		var stack = new Stack<(IVirtualObject Vob, Matrix Parent)>();
		var roots = world.RootObjects;
		foreach (var root in roots)
		{
			stack.Push((root, Matrix.Identity));
		}

		while (stack.Count > 0)
		{
			var (vob, parent) = stack.Pop();
			vobCount++;

			var local = GothicVobTransform.ToEngineTransform(vob);
			var transform = GothicVobTransform.Compose(parent, local);

			if (startPosition is null &&
				(vob is StartPoint || vob.Type == VirtualObjectType.zCVobStartpoint))
			{
				startPosition = transform.Translation;
				startDirection = GothicVobTransform.StartDirection(vob);
			}

			var visual = vob.Visual;
			if (vob.ShowVisual && visual is not null)
			{
				if (visual.Type is VisualType.MultiResolutionMesh or VisualType.Mesh)
				{
					var key = visual.Type + "|" + visual.Name;
					if (!staticMeshes.ContainsKey(key) && !failedVisualKeys.Contains(key))
					{
						var loaded = TryLoadStaticMesh(vfs, textureCache, visual, key, progress);
						if (loaded is null)
						{
							failedVisuals++;
							failedVisualKeys.Add(key);
							if (failedVisualKeys.Count <= 20)
							{
								progress($"Missing static visual: {visual.Name}");
							}
						}
						else
						{
							staticMeshes[key] = loaded;
						}
					}

					if (staticMeshes.TryGetValue(key, out var staticMesh))
					{
						staticVobs.Add(new StaticVobInstance(staticMesh.Key, transform));
					}
				}
				else
				{
					skippedVisuals++;
				}
			}
			else
			{
				skippedVisuals++;
			}

			var children = vob.Children;
			foreach (var child in children)
			{
				stack.Push((child, transform));
			}
		}

		var vobMs = vobStart.Elapsed.TotalMilliseconds;
		progress(
			$"VOBs: {vobCount} objects, {staticVobs.Count} static instances, " +
			$"{staticMeshes.Count} unique static meshes in {vobMs:0} ms");

		var waypointStart = Stopwatch.StartNew();
		var waypoints = ConvertWaypoints(world);
		var waypointMs = waypointStart.Elapsed.TotalMilliseconds;

		if (startPosition is null && waypoints.Count > 0)
		{
			var fallback = waypoints.FirstOrDefault(point => !point.FreePoint) ?? waypoints[0];
			startPosition = fallback.Position;
			startDirection = fallback.Direction;
		}

		if (startPosition is null)
		{
			startPosition = (meshResult.Mesh.Bounds.Min + meshResult.Mesh.Bounds.Max) * 0.5f;
			startDirection = -Vector3.UnitZ;
		}

		total.Stop();
		var stats = new WorldLoadStats(
			roots.Count,
			vobCount,
			staticVobs.Count,
			staticMeshes.Count,
			skippedVisuals,
			failedVisuals,
			textureCache.MissingTextures.Count,
			waypoints.Count,
			parseMs,
			meshMs,
			materialMs,
			vobMs,
			waypointMs,
			total.Elapsed.TotalMilliseconds);

		progress(
			$"Waypoints: {waypoints.Count} in {waypointMs:0} ms; " +
			$"world loaded in {total.Elapsed.TotalMilliseconds:0} ms");

		GC.KeepAlive(mesh);
		GC.KeepAlive(world);
		GC.KeepAlive(worldRead);

		return new WorldAsset(
			entry.Path,
			meshResult.Mesh,
			materials,
			staticVobs,
			staticMeshes,
			waypoints,
			textureCache.MissingTextures.ToArray(),
			startPosition,
			startDirection,
			meshResult.Mesh.Bounds,
			stats);
	}

	private static StaticMeshSource? TryLoadStaticMesh(
		IGameVfs vfs,
		TextureCache textures,
		IVisual visual,
		string key,
		Action<string> progress)
	{
		if (string.IsNullOrWhiteSpace(visual.Name))
		{
			return null;
		}

		var extension = visual.Type == VisualType.Mesh ? "MSH" : "MRM";
		var name = Path.ChangeExtension(visual.Name, extension);
		var entry = vfs.Find(name);
		if (entry is null)
		{
			return null;
		}

		try
		{
			var read = new Read(ReadBytes(vfs, entry.Path));
			MeshSource source;
			MaterialSet materials;
			if (visual.Type == VisualType.Mesh)
			{
				var mesh = new ZenKit.Mesh(read);
				source = ZenKitMeshAdapter.FromMesh(mesh);
				materials = MaterialSetBuilder.FromMesh(mesh, textures);
				GC.KeepAlive(mesh);
			}
			else
			{
				var mesh = new MultiResolutionMesh(read);
				source = ZenKitMeshAdapter.FromMultiResolutionMesh(mesh);
				materials = MaterialSetBuilder.FromMultiResolutionMesh(mesh, textures);
				GC.KeepAlive(mesh);
			}

			var result = MeshBuilder.Build(source, entry.Path);
			GC.KeepAlive(read);
			return new StaticMeshSource(key, result.Mesh, materials);
		}
		catch (Exception exception)
		{
			progress($"Static visual '{entry.Path}' failed: {exception.Message}");
			return null;
		}
	}

	private static List<WorldWaypoint> ConvertWaypoints(ZenKit.World world)
	{
		var wayNet = world.WayNet;
		if (wayNet is null)
		{
			return new List<WorldWaypoint>();
		}

		var points = wayNet.Points;
		var result = new List<WorldWaypoint>(points.Count);
		foreach (var point in points)
		{
			var position = CoordinateSystem.ToEnginePoint(
				new Vector3(point.Position.X, point.Position.Y, point.Position.Z));
			var direction = CoordinateSystem.ToEngineDirection(
				new Vector3(point.Direction.X, point.Direction.Y, point.Direction.Z));
			if (direction.LengthSquared() > 0f)
			{
				direction = Vector3.Normalize(direction);
			}

			result.Add(new WorldWaypoint(
				point.Name ?? string.Empty,
				position,
				direction,
				point.FreePoint,
				point.UnderWater,
				point.WaterDepth));
		}

		return result;
	}

	private static byte[] ReadBytes(IGameVfs vfs, string name)
	{
		using var stream = vfs.OpenRead(name);
		using var memory = new MemoryStream();
		stream.CopyTo(memory);
		return memory.ToArray();
	}
}
