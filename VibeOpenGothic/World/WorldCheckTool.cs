using Microsoft.Xna.Framework;
using VibeOpenGothic.Data.Installs;
using VibeOpenGothic.Data.Vfs;
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
				$"{world.Stats.StaticVobCount} static instances, {world.Stats.UniqueStaticMeshCount} unique meshes");
			output(
				$"Waypoints: {world.Waypoints.Count}; start: {start}; direction: {world.StartDirection}");
			output(
				$"Bounds: min {bounds.Min}, max {bounds.Max}; " +
				$"size {bounds.Max - bounds.Min}");
			output(
				$"Skipped visuals: {world.Stats.SkippedVisualCount}; failed: {world.Stats.FailedVisualCount}; " +
				$"missing textures: {world.Stats.MissingTextureCount}");
			output(
				$"Timings: parse {world.Stats.ParseMilliseconds:0} ms, mesh {world.Stats.MeshMilliseconds:0} ms, " +
				$"materials {world.Stats.MaterialMilliseconds:0} ms, VOBs {world.Stats.VobMilliseconds:0} ms, " +
				$"waypoints {world.Stats.WaypointMilliseconds:0} ms, total {world.Stats.TotalMilliseconds:0} ms");

			return 0;
		}
		catch (Exception exception)
		{
			output($"FAIL {name}: {exception.GetType().Name}: {exception.Message}");
			return 1;
		}
	}
}
