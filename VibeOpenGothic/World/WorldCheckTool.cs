using System.Diagnostics;
using Microsoft.Xna.Framework;
using VibeOpenGothic.Data.Installs;
using VibeOpenGothic.Data.Vfs;
using VibeOpenGothic.Physics;
using ZenKit;

namespace VibeOpenGothic.World;

/// <summary>
/// Developer diagnostic for <c>--check-world</c>: loads and converts a ZEN world
/// without creating GPU resources and reports counts, bounds and timings.
/// </summary>
public static class WorldCheckTool
{
	public static int Run(
		GothicInstall install,
		IGameVfs vfs,
		string? worldName = null,
		Action<string>? output = null)
	{
		ArgumentNullException.ThrowIfNull(install);
		ArgumentNullException.ThrowIfNull(vfs);
		output ??= Console.WriteLine;

		var name = string.IsNullOrWhiteSpace(worldName) ? install.DefaultWorldName : worldName;
		var gameVersion = install.Version.IsGothic1 ? GameVersion.Gothic1 : GameVersion.Gothic2;

		try
		{
			var world = WorldLoader.Load(vfs, name, gameVersion, output);
			var bounds = world.Bounds;
			var start = world.StartPosition ?? Vector3.Zero;

			output($"World: {world.Name}");
			output(
				$"Mesh: {world.Mesh.TriangleCount} triangles, {world.Mesh.VertexCount} vertices, " +
				$"{world.Mesh.SubMeshes.Count} submeshes, {world.Materials.Materials.Count} materials");
			output(
				$"VOBs: {world.Stats.RootObjectCount} roots, {world.Stats.VobCount} total, " +
				$"{world.Stats.StaticVobCount} static instances, {world.Stats.UniqueStaticMeshCount} unique meshes, " +
				$"{world.Stats.ModelVobCount} model instances, {world.Stats.UniqueModelCount} unique models");
			var modelTriangles = world.Models.Values.Sum(model => (long)model.TriangleCount);
			output(
				$"Models: {world.Models.Count} loaded, {world.Stats.FailedModelCount} failed, " +
				$"{modelTriangles} triangles");
			var modelWarnings = world.Models.Values
				.SelectMany(model => model.Warnings)
				.ToList();
			if (modelWarnings.Count > 0)
			{
				output($"Model warnings: {modelWarnings.Count}");
				foreach (var warning in modelWarnings.Take(10))
				{
					output($"  {warning}");
				}
			}
			output(
				$"Waypoints: {world.Waypoints.Count}; start: {start}; direction: {world.StartDirection}");
			output(
				$"Bounds: min {bounds.Min}, max {bounds.Max}; " +
				$"size {bounds.Max - bounds.Min}");
			output(
				$"Skipped visuals: {world.Stats.SkippedVisualCount}; failed: {world.Stats.FailedVisualCount}; " +
				$"missing textures: {world.Stats.MissingTextureCount}");

			var collisionStart = Stopwatch.StartNew();
			using (var physics = WorldPhysicsBuilder.Build(world, output))
			{
				var collisionStats = physics.Stats;
				output(
					$"Collision: {collisionStats.MeshCount} meshes, " +
					$"{collisionStats.TriangleCount} triangles, " +
					$"{collisionStats.InstanceCount} instances, " +
					$"{collisionStats.DegenerateTriangles} degenerate, " +
					$"{collisionStats.SkippedInstances} skipped, " +
					$"{collisionStats.NonUnitScaleInstances} non-unit-scale, " +
					$"{collisionStats.BuildMilliseconds:0} ms");
				var probeEnd = start - new Vector3(0f, 1000f, 0f);
				if (physics.Raycast(
					start,
					probeEnd,
					out var hit,
					CollisionLayer.World | CollisionLayer.StaticProp))
				{
					output(
						$"Collision probe: hit {hit.Layer} at {hit.Position}, " +
						$"{hit.Distance:0} cm");
				}
				else
				{
					output("Collision probe: no hit");
				}
			}

			output(
				$"Timings: parse {world.Stats.ParseMilliseconds:0} ms, mesh {world.Stats.MeshMilliseconds:0} ms, " +
				$"materials {world.Stats.MaterialMilliseconds:0} ms, VOBs {world.Stats.VobMilliseconds:0} ms, " +
				$"waypoints {world.Stats.WaypointMilliseconds:0} ms, " +
				$"total {world.Stats.TotalMilliseconds:0} ms, " +
				$"collision round-trip {collisionStart.Elapsed.TotalMilliseconds:0} ms");

			return 0;
		}
		catch (Exception exception)
		{
			output($"FAIL {name}: {exception.GetType().Name}: {exception.Message}");
			return 1;
		}
	}
}
